using FairyTales.UI.Core;
using UnityEngine;

namespace FairyTales.Api
{
    /// <summary>
    /// Pushes the app language to the user's server profile whenever it changes.
    /// Self-bootstrapping like <see cref="Diagnostics.PushService"/> — no scene object.
    ///
    /// Register() already carries the language, but only on the paths that call it:
    /// onboarding, and the FIRST library load of a session. A returning user who
    /// switches language mid-session hits neither — LibraryScreen has already set its
    /// _registered flag — so users.lang keeps the old value until the next cold start.
    ///
    /// That outlives the text on screen: push language is
    /// COALESCE(users.lang, push_tokens.lang), so a stale users.lang silently wins over
    /// the freshly re-registered token PushService posts on this very same event.
    /// </summary>
    public class ProfileLanguageSync : MonoBehaviour
    {
        private static ProfileLanguageSync _i;
        private ApiClient _api;
        private AuthService _auth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_i != null) return;
            var go = new GameObject("[LangSync]");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<ProfileLanguageSync>();
        }

        private void Awake() => Loc.OnLanguageChanged += OnLanguageChanged;

        private void OnDestroy() => Loc.OnLanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged()
        {
            if (_api == null) _api = FindAnyObjectByType<ApiClient>();

            // No session yet — this is the onboarding path, where the language reaches
            // the server as the lang argument of the Register() that follows. Posting
            // an unauthenticated profile update here would only earn a 401.
            if (_api == null || !_api.HasToken) return;

            _auth ??= new AuthService(_api);

            // Language only: UpdateProfile omits the null name/gender, so this never
            // overwrites the profile the child was registered with.
            StartCoroutine(_auth.UpdateProfile(lang: Loc.Lang));
        }
    }
}
