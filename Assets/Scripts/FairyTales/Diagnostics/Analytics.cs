using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using FairyTales.Api;
#if FT_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
#endif

namespace FairyTales.Diagnostics
{
    /// <summary>
    /// GA4 analytics facade (taxonomy v1 — see AI/ANALYTICS_SPEC.md).
    ///
    /// Two sinks:
    ///  1. Firebase GA4 — compiled ONLY when the scripting define <c>FT_FIREBASE</c> is set
    ///     (i.e. after the Firebase Unity SDK is imported). Without it, GA4 calls no-op so the
    ///     project always builds. Instrumentation in the screens never changes either way.
    ///  2. Backend mirror — POST /api/analytics/event (fire-and-forget, batched). Always on for
    ///     a critical-event whitelist so we have a copy independent of Google (spec §5).
    ///
    /// Child mode (spec Rule #1): no PII is ever sent, ad personalization is disabled.
    /// Self-bootstraps like <see cref="RemoteLog"/> — no scene object required.
    /// </summary>
    public class Analytics : MonoBehaviour
    {
        private const string Endpoint = "/api/analytics/event";
        private const float FlushInterval = 15f;
        private const int MaxBatch = 25; // stay well under the server's 50 limit

        // Critical events duplicated to our backend (spec §5). Reading events (tale_open/
        // tale_page_view/tale_abandon) mirror too so the admin per-tale retention curve can be
        // built from our copy (CLIENT_TICKET_ANALYTICS_READING_MIRROR). Volume is moderate for a
        // kids app and the mirror retains only 30 days; tale_page_view is already debounced ~1s.
        private static readonly HashSet<string> Mirror = new()
        {
            "purchase_start", "purchase_success", "purchase_error", "purchase_restore",
            "paywall_view", "paywall_dismiss", "promo_redeem", "tale_complete",
            "tale_open", "tale_page_view", "tale_abandon",
            // Referral gift: the funnel is split across two devices (inviter shows/copies
            // the code, friend redeems it), so only our own backend can join the halves.
            "gift_popup_view", "gift_code_copied", "gift_earned", "promo_entered",
        };

        private static Analytics _i;
        private ApiClient _api;
        private string _session;
        private readonly List<EventRecord> _queue = new();
#if FT_FIREBASE
        private bool _fbReady;
        private readonly List<Action> _pending = new();
#endif

        // ── Bootstrap ────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_i != null) return;
            var go = new GameObject("[Analytics]");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<Analytics>();
        }

        private void Awake()
        {
            _session = Guid.NewGuid().ToString("N").Substring(0, 12);
            InitFirebase();
            SetUserId(AuthService.GetOrCreateUserId());
            RefreshUserProps();
            Log("app_open", ("is_premium", IsPremium));
            InvokeRepeating(nameof(Flush), FlushInterval, FlushInterval);
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>Send an event to GA4 (+ mirror if whitelisted). Safe from anywhere.</summary>
        public static void Log(string name, params (string key, object value)[] p)
        {
            try { _i?.Send(name, p); } catch { /* analytics must never break the app */ }
        }

        public static void SetUserProperty(string name, string value)
        {
            try { _i?.SetProp(name, value); } catch { }
        }

        public static void SetUserId(string id)
        {
#if FT_FIREBASE
            _i?.RunOnFirebase(() => FirebaseAnalytics.SetUserId(id));
#endif
        }

        /// <summary>Re-read user state (premium, language, gender…) and push as user properties.</summary>
        public static void RefreshUserProps()
        {
            bool premium = IsPremium;
            SetUserProperty("is_premium", premium ? "true" : "false");
            SetUserProperty("premium_source",
                premium ? Pref("ft_premium_source", "none") : "none");
            SetUserProperty("app_language", Pref("ft_lang", "ru"));
            SetUserProperty("child_gender", Gender());
            SetUserProperty("has_cloned_voice",
                PlayerPrefs.GetInt("ft_has_cloned_voice", 0) == 1 ? "true" : "false");
        }

        /// <summary>ft_gender ("male"/"female") → spec enum boy/girl/unset.</summary>
        public static string Gender()
        {
            return PlayerPrefs.GetString("ft_gender", "") switch
            {
                "male" => "boy",
                "female" => "girl",
                _ => "unset",
            };
        }

        // ── Internals ────────────────────────────────────────

        private static bool IsPremium => PlayerPrefs.GetInt("ft_premium", 0) == 1;
        private static string Pref(string k, string d)
        {
            var v = PlayerPrefs.GetString(k, d);
            return string.IsNullOrEmpty(v) ? d : v;
        }

        private void Send(string name, (string key, object value)[] p)
        {
            Debug.Log($"[Analytics] {name} | {Describe(p)}");

#if FT_FIREBASE
            RunOnFirebase(() => FirebaseAnalytics.LogEvent(name, ToParams(p)));
#endif
            if (Mirror.Contains(name))
            {
                _queue.Add(new EventRecord
                {
                    name = name,
                    ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    prms = ToDict(p),
                });
                if (_queue.Count >= MaxBatch) Flush();
            }
        }

        private void SetProp(string name, string value)
        {
#if FT_FIREBASE
            RunOnFirebase(() => FirebaseAnalytics.SetUserProperty(name, value));
#endif
        }

        private static Dictionary<string, object> ToDict((string key, object value)[] p)
        {
            var d = new Dictionary<string, object>();
            if (p != null)
                foreach (var (k, v) in p) d[k] = v;
            return d;
        }

        private static string Describe((string key, object value)[] p)
        {
            if (p == null || p.Length == 0) return "";
            var parts = new string[p.Length];
            for (int i = 0; i < p.Length; i++) parts[i] = $"{p[i].key}={p[i].value}";
            return string.Join("; ", parts);
        }

        // ── Backend mirror ───────────────────────────────────
        private void Flush()
        {
            if (_queue.Count == 0) return;
            if (_api == null) _api = FindAnyObjectByType<ApiClient>();
            if (_api == null) return; // no transport yet — keep buffered

            var batch = _queue.GetRange(0, Math.Min(_queue.Count, 50));
            _queue.RemoveRange(0, batch.Count);

            var body = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["session"] = _session,
                ["platform"] = Platform(),
                ["appVersion"] = Application.version,
                ["events"] = batch,
            });
            StartCoroutine(_api.PostJson(Endpoint, body, _ => { }, _ => { }));
        }

        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit() => Flush();

        private static string Platform()
        {
#if UNITY_EDITOR
            return "editor";
#elif UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#else
            return "editor";
#endif
        }

        [Serializable]
        private class EventRecord
        {
            public string name;
            public long ts;
            [JsonProperty("params")] public Dictionary<string, object> prms;
        }

        // ── Firebase (compiled only with FT_FIREBASE) ────────
        private void InitFirebase()
        {
#if FT_FIREBASE
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning($"[Analytics] Firebase unavailable: {task.Result}");
                    return;
                }
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                // Child mode (spec Rule #1): never personalize ads, never link an ads SDK.
                FirebaseAnalytics.SetUserProperty("allow_personalized_ads", "false");
                _fbReady = true;
                foreach (var a in _pending) { try { a(); } catch { } }
                _pending.Clear();
                Debug.Log("[Analytics] Firebase ready");
            });
#endif
        }

#if FT_FIREBASE
        private void RunOnFirebase(Action a)
        {
            if (_fbReady) a();
            else _pending.Add(a);
        }

        private static Parameter[] ToParams((string key, object value)[] p)
        {
            if (p == null) return Array.Empty<Parameter>();
            var list = new List<Parameter>(p.Length);
            foreach (var (k, v) in p)
            {
                switch (v)
                {
                    case null: break;
                    case bool b: list.Add(new Parameter(k, b ? "true" : "false")); break;
                    case string s: list.Add(new Parameter(k, s)); break;
                    case int i: list.Add(new Parameter(k, (long)i)); break;
                    case long l: list.Add(new Parameter(k, l)); break;
                    case float f: list.Add(new Parameter(k, (double)f)); break;
                    case double d: list.Add(new Parameter(k, d)); break;
                    default: list.Add(new Parameter(k, v.ToString())); break;
                }
            }
            return list.ToArray();
        }
#endif
    }
}
