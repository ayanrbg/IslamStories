using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using FairyTales.Api;
using FairyTales.UI.Core;
#if FT_FCM
using Firebase;
using Firebase.Messaging;
using Firebase.Extensions;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace FairyTales.Diagnostics
{
    /// <summary>
    /// FCM push client (CLIENT_TICKET_PUSH). Self-bootstraps like <see cref="Analytics"/>
    /// / <see cref="RemoteLog"/> — no scene object required.
    ///
    /// Firebase Messaging calls compile ONLY with the scripting define <c>FT_FCM</c> (set
    /// AFTER importing the Firebase Messaging Unity module). Without it the project still
    /// builds and this component no-ops. The backend register/unregister HTTP and the
    /// deep-link plumbing compile unconditionally so they stay testable.
    ///
    /// Responsibilities:
    ///  1. Ask for notification permission at a sensible moment (after onboarding).
    ///  2. Register the FCM token to /api/push/register — at start, on token refresh,
    ///     and on app-language change (so pushes arrive in the user's language).
    ///  3. Route the notification <c>data</c> payload into <see cref="DeepLink"/>.
    ///  4. /api/push/unregister on logout; optional /api/push/opened for open-rate.
    ///
    /// Child mode: notifications only — no IDFA / ad identifiers are ever requested.
    /// </summary>
    public class PushService : MonoBehaviour
    {
        private const string RegisterEndpoint = "/api/push/register";
        private const string UnregisterEndpoint = "/api/push/unregister";
        private const string OpenedEndpoint = "/api/push/opened";

        /// <summary>
        /// Must stay identical to the channel id the server sends (ticket §5.5) and to
        /// the manifest's default_notification_channel_id. Three places, one string.
        /// </summary>
        private const string ChannelId = "islamstories_default";

        private static PushService _i;
        private ApiClient _api;
        private string _token;

        // ── Bootstrap ────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_i != null) return;
            var go = new GameObject("[Push]");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<PushService>();
        }

        private void Awake()
        {
            Loc.OnLanguageChanged += OnLanguageChanged;
#if FT_FCM
            StartCoroutine(Init());
#endif
        }

        private void OnDestroy() => Loc.OnLanguageChanged -= OnLanguageChanged;

        // ── Public API ───────────────────────────────────────

        /// <summary>Drop this device's token from the backend (logout / push disabled).</summary>
        public static void Unregister() => _i?.DoUnregister();

        // ── Backend register/unregister (always compiled) ────

        private void OnLanguageChanged()
        {
            if (!string.IsNullOrEmpty(_token)) Register(); // re-register with new lang
        }

        private void Register()
        {
            if (string.IsNullOrEmpty(_token)) return;
            if (_api == null) _api = FindAnyObjectByType<ApiClient>();
            if (_api == null) return;

            var userId = AuthService.GetOrCreateUserId();
            var lang = PlayerPrefs.GetString("ft_lang", "ru");
            // QA aid: copy this userId from device logs for "test push to self".
            Debug.Log($"[Push] register userId={userId} platform={Platform()} lang={lang} " +
                      $"token={_token.Substring(0, Mathf.Min(12, _token.Length))}…");

            var body = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["userId"] = userId,
                ["token"] = _token,
                ["platform"] = Platform(),
                ["appVersion"] = Application.version,
                ["lang"] = lang,
            });
            StartCoroutine(_api.PostJson(RegisterEndpoint, body, _ => { }, _ => { }));
        }

        private void DoUnregister()
        {
            if (string.IsNullOrEmpty(_token)) return;
            if (_api == null) _api = FindAnyObjectByType<ApiClient>();
            if (_api == null) return;

            var body = JsonConvert.SerializeObject(
                new Dictionary<string, object> { ["token"] = _token });
            StartCoroutine(_api.PostJson(UnregisterEndpoint, body, _ => { }, _ => { }));
        }

        private void ReportOpened(string campaignId)
        {
            if (string.IsNullOrEmpty(campaignId)) return;
            if (_api == null) _api = FindAnyObjectByType<ApiClient>();
            if (_api == null) return;

            var body = JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["campaignId"] = campaignId,
                ["userId"] = AuthService.GetOrCreateUserId(),
            });
            StartCoroutine(_api.PostJson(OpenedEndpoint, body, _ => { }, _ => { }));
        }

        private static string Platform()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return "ios";
#elif UNITY_ANDROID && !UNITY_EDITOR
            return "android";
#else
            return "editor";
#endif
        }

        // ── Firebase Messaging (compiled only with FT_FCM) ───
#if FT_FCM
        private IEnumerator Init()
        {
            // Ask at a good moment, not on the first screen: wait until onboarding is
            // done (child name exists — same signal ScreenManager uses). Returning users
            // pass instantly.
            yield return new WaitUntil(() =>
                !string.IsNullOrEmpty(PlayerPrefs.GetString("ft_childName", "")));

            yield return RequestAndroidPermission(); // Android 13+ POST_NOTIFICATIONS
            EnsureAndroidChannel();

            var dep = FirebaseApp.CheckAndFixDependenciesAsync();
            yield return new WaitUntil(() => dep.IsCompleted);
            if (dep.Result != DependencyStatus.Available)
            {
                Debug.LogWarning($"[Push] Firebase unavailable: {dep.Result}");
                yield break;
            }

            FirebaseMessaging.TokenReceived += OnTokenReceived;
            FirebaseMessaging.MessageReceived += OnMessageReceived;
            FirebaseMessaging.RequestPermissionAsync(); // iOS system prompt (no-op on Android)

            // Fetch the current token explicitly too — covers the case where it already
            // existed before we subscribed.
            FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled || string.IsNullOrEmpty(t.Result)) return;
                _token = t.Result;
                Register();
            });
        }

        private IEnumerator RequestAndroidPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string perm = "android.permission.POST_NOTIFICATIONS"; // auto-granted < Android 13
            if (Permission.HasUserAuthorizedPermission(perm)) yield break;

            Permission.RequestUserPermission(perm);
            // Let the OS dialog resolve; bail after a while regardless of the answer.
            float t = 0f;
            while (!Permission.HasUserAuthorizedPermission(perm) && t < 30f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// Creates the notification channel the server addresses its pushes to. Declaring
        /// the id in the manifest is not enough: on Android 8+ a push aimed at a channel
        /// that was never created shows no banner at all, and nothing reports an error.
        /// Creating an existing channel is a no-op, so this runs on every launch.
        /// </summary>
        private void EnsureAndroidChannel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                if (version.GetStatic<int>("SDK_INT") < 26) return; // channels start at Oreo

                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                using var manager = activity.Call<AndroidJavaObject>(
                    "getSystemService", "notification");

                // IMPORTANCE_DEFAULT (3): a banner, no sound override. Importance is fixed
                // at creation — changing it later needs a new id or a reinstall.
                using var channel = new AndroidJavaObject(
                    "android.app.NotificationChannel", ChannelId, Application.productName, 3);
                manager.Call("createNotificationChannel", channel);
                Debug.Log($"[Push] notification channel ready: {ChannelId}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Push] channel creation failed: {e.Message}");
            }
#endif
        }

        private void OnTokenReceived(object sender, TokenReceivedEventArgs e)
        {
            _token = e.Token;
            Register(); // refresh → re-register (mandatory per ticket §3)
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var msg = e?.Message;
            var data = msg?.Data;
            if (data == null || data.Count == 0) return;

            // Only route when the user actually tapped the notification (background/closed
            // launch). Foreground data delivery shouldn't yank the user off their screen.
            if (!msg.NotificationOpened) return;

            data.TryGetValue("type", out var type);
            Analytics.Log("push_open", ("type", type ?? "home"));
            DeepLink.Set(data);
            if (data.TryGetValue("campaignId", out var cid)) ReportOpened(cid);
        }
#endif
    }
}
