using System;
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
    /// </summary>
    public class RemoteLog : MonoBehaviour
    {
        private const string Endpoint = "/api/debug/log";

        private static RemoteLog _instance;
        private ApiClient _api;
        private string _userId;
        private string _session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[RemoteLog]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RemoteLog>();
            _instance._session = Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>Send a debug event. Safe to call from anywhere, anytime.</summary>
        public static void Event(string ev, string data = null)
        {
            try { _instance?.Send(ev, data); }
            catch { /* diagnostics must never break the app */ }
        }

        private static bool Enabled => PlayerPrefs.GetInt("ft_debug_remote", 1) == 1;

        private void Send(string ev, string data)
        {
            if (!Enabled) return;

            if (_api == null) _api = FindAnyObjectByType<ApiClient>();
            if (_api == null) return; // no transport yet — drop (early boot)

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

            var body = JsonUtility.ToJson(payload);
            StartCoroutine(_api.PostJson(Endpoint, body, _ => { }, _ => { }));
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
