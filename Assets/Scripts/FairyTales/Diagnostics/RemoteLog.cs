using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FairyTales.Api;

namespace FairyTales.Diagnostics
{
    /// <summary>
    /// Fire-and-forget remote debug logger. Posts key IAP / entitlement events to the
    /// server so we can diagnose purchases WITHOUT a Mac / device console.
    ///
    /// Usage: <c>RemoteLog.Event("purchase_start", $"productId={id}");</c>
    ///
    /// Self-bootstraps (no scene object needed). Never throws, never blocks gameplay.
    /// Toggle at runtime via PlayerPrefs key "ft_debug_remote" (1 = on, default on).
    /// Endpoint: POST /api/debug/log  (see SERVER_ENTITLEMENT_SPEC — Debug log section).
    ///
    /// Events raised before <see cref="ApiClient"/> exists are queued, not dropped: the
    /// most valuable ones (store init) fire during the first seconds of boot, exactly
    /// when the transport is not up yet.
    /// </summary>
    public class RemoteLog : MonoBehaviour
    {
        private const string Endpoint = "/api/debug/log";

        /// <summary>Cap on queued-but-unsent events. Old ones are dropped first — a
        /// diagnostics buffer must never grow without bound on a broken network.</summary>
        private const int MaxQueued = 200;

        /// <summary>Unity error/exception lines forwarded per session. Enough to catch a
        /// crash loop, low enough that a per-frame error cannot flood the server.</summary>
        private const int MaxCapturedErrors = 60;

        private static RemoteLog _instance;
        private ApiClient _api;
        private string _userId;
        private string _session;

        private readonly Queue<string> _pending = new();
        private readonly HashSet<string> _seenErrors = new();
        private int _capturedErrors;
        private bool _flushing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[RemoteLog]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RemoteLog>();
            _instance._session = Guid.NewGuid().ToString("N").Substring(0, 8);
            _instance.LogEnvironment();
        }

        private void OnEnable() => Application.logMessageReceived += OnUnityLog;
        private void OnDisable() => Application.logMessageReceived -= OnUnityLog;

        /// <summary>Send a debug event. Safe to call from anywhere, anytime.</summary>
        public static void Event(string ev, string data = null)
        {
            try { _instance?.Enqueue(ev, data); }
            catch { /* diagnostics must never break the app */ }
        }

        private static bool Enabled => PlayerPrefs.GetInt("ft_debug_remote", 1) == 1;

        /// <summary>
        /// One-time snapshot of everything that shapes store behaviour. Bundle id, build
        /// and OS decide which App Store Connect record and sandbox the device talks to,
        /// so a report without them is not actionable.
        /// </summary>
        private void LogEnvironment()
        {
            Enqueue("session_start",
                $"bundle={Application.identifier}; version={Application.version}; " +
                $"platform={Application.platform}; os={SystemInfo.operatingSystem}; " +
                $"device={SystemInfo.deviceModel}; lang={Application.systemLanguage}; " +
                $"installMode={Application.installMode}; genuine={Application.genuineCheckAvailable}; " +
                $"internet={Application.internetReachability}");
        }

        /// <summary>
        /// Mirror Unity's own errors and exceptions. The paywall bugs that matter most are
        /// the ones nobody thought to instrument — a null-ref inside a tween callback never
        /// reaches an explicit <see cref="Event"/> call. Deduped by message so a per-frame
        /// error costs one upload, not thousands.
        /// </summary>
        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (_capturedErrors >= MaxCapturedErrors) return;

            // Our own [IAP-DBG] error lines are already reported through Event() with
            // structured data — forwarding them again would just double the volume.
            if (condition != null && condition.StartsWith("[IAP-DBG]")) return;

            var key = condition ?? "";
            if (key.Length > 160) key = key.Substring(0, 160);
            if (!_seenErrors.Add(key)) return;

            _capturedErrors++;
            var frame = FirstProjectFrame(stackTrace);
            Enqueue("unity_error", $"type={type}; msg={key}{(frame != null ? $"; at={frame}" : "")}");
        }

        /// <summary>First stack frame from our own namespace — the rest is engine noise.</summary>
        private static string FirstProjectFrame(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return null;
            foreach (var line in stackTrace.Split('\n'))
            {
                if (line.Contains("FairyTales.")) return line.Trim();
            }
            return null;
        }

        private void Enqueue(string ev, string data)
        {
            if (!Enabled) return;

            if (string.IsNullOrEmpty(_userId))
                _userId = PlayerPrefs.GetString("ft_userId", "");

            var payload = new LogPayload
            {
                userId = _userId,
                session = _session,
                platform = Application.platform.ToString(),
                appVersion = Application.version,
                ev = ev,
                data = data ?? "",
                ts = DateTime.UtcNow.ToString("o")
            };

            // Mirror to local console too (harmless if nobody's watching).
            Debug.Log($"[REMOTE-LOG] {ev} | {data}");

            if (_pending.Count >= MaxQueued) _pending.Dequeue();
            _pending.Enqueue(JsonUtility.ToJson(payload));

            if (!_flushing) StartCoroutine(Flush());
        }

        /// <summary>
        /// Drain the queue, waiting for the transport to appear. Runs at most once at a
        /// time so events keep their order and a slow server cannot fan out requests.
        /// </summary>
        private IEnumerator Flush()
        {
            _flushing = true;

            while (_pending.Count > 0)
            {
                if (_api == null) _api = FindAnyObjectByType<ApiClient>();
                if (_api == null)
                {
                    // Early boot: hold the events rather than lose them.
                    yield return new WaitForSecondsRealtime(1f);
                    continue;
                }

                var body = _pending.Peek();
                var done = false;
                var failed = false;
                yield return _api.PostJson(Endpoint, body,
                    _ => done = true,
                    _ => { done = true; failed = true; });

                if (!done) yield return null;

                // Drop on failure instead of retrying forever: diagnostics must never
                // become the reason the device keeps hammering a dead endpoint.
                _pending.Dequeue();
                if (failed) yield return new WaitForSecondsRealtime(2f);
            }

            _flushing = false;
        }

        [Serializable]
        private class LogPayload
        {
            public string userId;
            public string session;   // groups events from one app launch
            public string platform;
            public string appVersion;
            public string ev;
            public string data;
            public string ts;        // ISO-8601 UTC
        }
    }
}
