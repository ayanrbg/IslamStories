using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using FairyTales.Api;

namespace FairyTales.Diagnostics
{
    /// <summary>
    /// Fetches the narration controls (narrationEpoch, defaultNarratorGender,
    /// narrationQuiet — see NarrationConfig) in EVERY build.
    ///
    /// Until 1.7.10 only RemoteLogMirror fetched them, and the mirror never starts in
    /// store builds (ReleaseLogging.Silenced). So an epoch raised on the server never
    /// reached real users: in October 2026 they kept playing narration made while the
    /// TTS endpoint misread words («кОмар», «Карен» for «Коран»), with no way to drop it
    /// short of a new build. This component has no logging dependency at all.
    ///
    /// Asks on launch and again on returning from the background, at most every
    /// ResumeIntervalSec. Never writes to the log: in store builds logging is off anyway,
    /// and in debug builds the mirror would ship every line back to the server.
    /// </summary>
    public class NarrationConfigSync : MonoBehaviour
    {
        private const string ConfigEndpoint = "/api/debug/config";
        private const float ResumeIntervalSec = 600f;
        // ApiClient lives in the first scene and registration fills ft_userId a moment
        // later; on a cold start neither may be ready when this component starts.
        private const float WaitForApiSec = 30f;
        private const float WaitForUserSec = 10f;

        private static NarrationConfigSync _instance;

        private ApiClient _api;
        private bool _fetching;
        private float _lastFetch = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[NarrationConfigSync]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NarrationConfigSync>();
        }

        private void Start()
        {
            StartCoroutine(Fetch());
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && Time.realtimeSinceStartup - _lastFetch >= ResumeIntervalSec)
                StartCoroutine(Fetch());
        }

        private IEnumerator Fetch()
        {
            if (_fetching) yield break;
            _fetching = true;

            float waited = 0f;
            while (_api == null)
            {
                _api = FindAnyObjectByType<ApiClient>();
                if (_api != null || waited >= WaitForApiSec) break;
                yield return new WaitForSecondsRealtime(1f);
                waited += 1f;
            }

            // The server can raise the epoch for a single user, which it can only match
            // with an id. Without one it still answers with the global epoch.
            var uid = PlayerPrefs.GetString("ft_userId", "");
            waited = 0f;
            while (string.IsNullOrEmpty(uid) && waited < WaitForUserSec)
            {
                yield return new WaitForSecondsRealtime(1f);
                waited += 1f;
                uid = PlayerPrefs.GetString("ft_userId", "");
            }

            if (_api != null)
            {
                _lastFetch = Time.realtimeSinceStartup;
                var query = string.IsNullOrEmpty(uid) ? "" : "?userId=" + UnityWebRequest.EscapeURL(uid);
                yield return _api.Get(ConfigEndpoint + query,
                    onSuccess: NarrationConfig.Apply,
                    onError: _ => { /* keep what we have; the next launch asks again */ });
            }

            _fetching = false;
        }
    }
}
