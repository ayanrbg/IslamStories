using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FairyTales.Api;

namespace FairyTales.Diagnostics
{
    /// <summary>
    /// Mirrors the ENTIRE Unity log stream (Debug.Log/Warning/Error/Exception, from any
    /// thread) to the server in fire-and-forget batches, so we can diagnose the LIVE
    /// App Store build with nothing on screen.
    ///
    /// REMOTE KILL-SWITCH — the whole point: once we've caught the bug we can turn this
    /// OFF without shipping a new client. On start (and after every batch) the client
    /// reads a server flag; when the server says disabled, the client stops capturing
    /// and stops sending. See AI/SERVER_LOG_MIRROR_SPEC.md for the two endpoints.
    ///
    /// Self-bootstraps (no scene object needed). Never throws, never blocks gameplay.
    /// </summary>
    public class RemoteLogMirror : MonoBehaviour
    {
        private const string LogsEndpoint = "/api/debug/logs";   // POST batch
        private const string ConfigEndpoint = "/api/debug/config"; // GET kill-switch

        // Local cache of the remote switch, so a shipped build defaults to ON and the
        // last known server verdict survives restarts.
        private const string PrefEnabled = "ft_log_mirror";      // 1 = on (default)
        private const string PrefLevel = "ft_log_mirror_level";  // "all" | "warn"

        private static RemoteLogMirror _instance;

        private ApiClient _api;
        private string _userId;
        private string _session;

        // Tunables (overridable by server config).
        private float _flushSec = 4f;
        private int _batchMax = 40;
        private const int BufferCap = 500;      // hard cap so a flush outage can't grow unbounded
        private const int MaxMsgLen = 2000;

        private readonly object _lock = new object();
        private readonly List<Line> _buffer = new List<Line>();
        private bool _sending;

        // Cached on the main thread (PlayerPrefs is main-thread only); read from the
        // background log callback via these volatile fields.
        private volatile bool _enabled = true;
        private volatile bool _warnOnly;

        private bool Enabled => _enabled;
        private bool WarnOnly => _warnOnly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[RemoteLogMirror]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RemoteLogMirror>();
        }

        private void Awake()
        {
            _session = Guid.NewGuid().ToString("N").Substring(0, 8);
            // Seed the cached switch from the last known server verdict (main thread).
            _enabled = PlayerPrefs.GetInt(PrefEnabled, 1) == 1;
            _warnOnly = PlayerPrefs.GetString(PrefLevel, "all") == "warn";
            // Threaded variant captures logs emitted from background threads too.
            Application.logMessageReceivedThreaded += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        private void Start()
        {
            StartCoroutine(FetchConfig());
            StartCoroutine(FlushLoop());
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) StartCoroutine(Flush()); // best-effort flush before backgrounding
        }

        // ── Capture ─────────────────────────────────────────
        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!Enabled) return;
            bool isProblem = type == LogType.Error || type == LogType.Exception || type == LogType.Warning;
            if (WarnOnly && !isProblem) return;

            var msg = condition ?? "";
            if (msg.Length > MaxMsgLen) msg = msg.Substring(0, MaxMsgLen) + "…";

            var line = new Line
            {
                ts = DateTime.UtcNow.ToString("o"),
                level = type.ToString(),
                message = msg,
                // Stack only for real problems — keeps payloads small.
                stack = isProblem && !string.IsNullOrEmpty(stackTrace)
                    ? (stackTrace.Length > MaxMsgLen ? stackTrace.Substring(0, MaxMsgLen) : stackTrace)
                    : ""
            };

            lock (_lock)
            {
                _buffer.Add(line);
                if (_buffer.Count > BufferCap) _buffer.RemoveRange(0, _buffer.Count - BufferCap);
            }
        }

        // ── Flush loop ──────────────────────────────────────
        private IEnumerator FlushLoop()
        {
            while (true)
            {
                float wait = Mathf.Max(1f, _flushSec);
                float t = 0f;
                // Flush early if the buffer fills up, otherwise on the interval.
                while (t < wait)
                {
                    int count;
                    lock (_lock) count = _buffer.Count;
                    if (count >= _batchMax) break;
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                yield return Flush();
            }
        }

        private IEnumerator Flush()
        {
            if (_sending) yield break;
            if (!Enabled) yield break;

            if (_api == null) _api = FindAnyObjectByType<ApiClient>();
            if (_api == null) yield break; // no transport yet — keep buffering

            Line[] batch;
            lock (_lock)
            {
                if (_buffer.Count == 0) yield break;
                batch = _buffer.ToArray();
                _buffer.Clear();
            }

            _sending = true;

            if (string.IsNullOrEmpty(_userId))
                _userId = PlayerPrefs.GetString("ft_userId", "");

            var payload = new Batch
            {
                userId = _userId,
                session = _session,
                platform = Application.platform.ToString(),
                appVersion = Application.version,
                lines = batch
            };
            var body = JsonUtility.ToJson(payload);

            // NOTE: use callbacks that do NOT Debug.Log, to avoid a capture→send→capture loop.
            bool requeue = false;
            yield return _api.PostJson(LogsEndpoint, body,
                onSuccess: resp => ApplyConfig(resp),   // server may piggyback the kill-switch here
                onError: _ => requeue = true);

            if (requeue)
            {
                // Put the batch back (front) so nothing is lost on a transient failure.
                lock (_lock)
                {
                    _buffer.InsertRange(0, batch);
                    if (_buffer.Count > BufferCap) _buffer.RemoveRange(BufferCap, _buffer.Count - BufferCap);
                }
            }

            _sending = false;
        }

        // ── Remote kill-switch ──────────────────────────────
        private IEnumerator FetchConfig()
        {
            if (_api == null) _api = FindAnyObjectByType<ApiClient>();
            if (_api == null) yield break;

            var uid = PlayerPrefs.GetString("ft_userId", "");
            yield return _api.Get($"{ConfigEndpoint}?userId={UnityWebRequestEscape(uid)}",
                onSuccess: resp => ApplyConfig(resp),
                onError: _ => { /* keep last known cache */ });
        }

        private static string UnityWebRequestEscape(string s)
            => string.IsNullOrEmpty(s) ? "" : UnityEngine.Networking.UnityWebRequest.EscapeURL(s);

        private void ApplyConfig(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var cfg = JsonUtility.FromJson<Config>(json);
                if (cfg == null || !cfg.hasConfig) return; // response without a config block → ignore

                PlayerPrefs.SetInt(PrefEnabled, cfg.enabled ? 1 : 0);
                if (!string.IsNullOrEmpty(cfg.level)) PlayerPrefs.SetString(PrefLevel, cfg.level);
                PlayerPrefs.Save();

                // Update the volatile fields the background capture reads.
                _enabled = cfg.enabled;
                if (!string.IsNullOrEmpty(cfg.level)) _warnOnly = cfg.level == "warn";
                if (cfg.flushSec > 0) _flushSec = cfg.flushSec;
                if (cfg.batchMax > 0) _batchMax = cfg.batchMax;
            }
            catch { /* ignore malformed config */ }
        }

        // ── DTOs ────────────────────────────────────────────
        [Serializable]
        private class Line
        {
            public string ts;       // ISO-8601 UTC
            public string level;    // Log | Warning | Error | Exception | Assert
            public string message;
            public string stack;    // only for problems
        }

        [Serializable]
        private class Batch
        {
            public string userId;
            public string session;  // groups one app launch
            public string platform;
            public string appVersion;
            public Line[] lines;
        }

        [Serializable]
        private class Config
        {
            // Server MUST set hasConfig=true when returning a real config block so the
            // client can tell "no config in this response" from "disable".
            public bool hasConfig;
            public bool enabled;    // false = stop capturing & sending (the kill-switch)
            public string level;    // "all" | "warn"
            public float flushSec;  // optional override
            public int batchMax;    // optional override
        }
    }
}
