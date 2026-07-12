using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Models;
using FairyTales.UI.Core;
using FairyTales.Cache;
using FairyTales.UI.Library;

namespace FairyTales.UI.Onboarding
{
    public class LoadingScreen : BaseScreen
    {
        [SerializeField] private Image background;
        [SerializeField] private float bgFadeDuration = 0.5f;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text statusText;

        private ScreenManager _screens;
        private ApiClient _api;
        private AuthService _auth;
        private TalesService _tales;

        public event Action OnLoadingComplete;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            _screens = GetComponentInParent<ScreenManager>();

            if (background) SetBgAlpha(0f);
            _api = FindAnyObjectByType<ApiClient>();
            _auth = new AuthService(_api);
            _tales = new TalesService(_api);
        }

        protected override void OnPrepare()
        {
            SetProgress(0f, Loc.Get("preparing_library"));
        }

        protected override void OnShown()
        {
            if (background) background.DOFade(1f, bgFadeDuration).SetEase(Ease.OutQuad);
            StartCoroutine(RunOnboarding());
        }

        protected override void OnHidden()
        {
            if (background) SetBgAlpha(0f);
        }

        private void SetBgAlpha(float a)
        {
            var c = background.color;
            c.a = a;
            background.color = c;
        }

        private IEnumerator RunOnboarding()
        {
            var lang = PlayerPrefs.GetString("ft_lang", "ru");
            var childName = PlayerPrefs.GetString("ft_childName", "");
            var gender = PlayerPrefs.GetString("ft_gender", "male");
            var userId = AuthService.GetOrCreateUserId();

            // Step 1 — Register (non-blocking)
            SetProgress(0.1f, Loc.Get("loading"));
            yield return _auth.Register(userId, childName, gender, lang,
                onSuccess: _ => { },
                onError: e => { } /* RELEASE: Debug.LogWarning($"[Loading] Register: {e}") */);

            // Step 2 — Fetch tales (server → bundled fallback)
            SetProgress(0.3f, Loc.Get("loading"));
            TaleSummary[] tales = null;
            yield return _tales.GetTales(lang,
                onSuccess: t => tales = t,
                onError: e => { } /* RELEASE: Debug.LogWarning($"[Loading] Server: {e}") */);

            if (tales == null)
                yield return BundledTaleLoader.LoadManifest(lang, t => tales = t);

            // Step 3 — Personalize non-bundled tales on server
            if (tales != null && tales.Length > 0)
            {
                float step = 0.6f / tales.Length;
                for (int i = 0; i < tales.Length; i++)
                {
                    SetProgress(0.3f + step * i, Loc.Get("loading"));

                    if (BundledTaleLoader.IsBundled(tales[i].id)) continue;

                    yield return _tales.Personalize(tales[i].id, childName, gender,
                        onSuccess: _ => { },
                        onError: e => { } /* RELEASE: Debug.LogWarning($"[Loading] Personalize {tales[i].id}: {e}") */);
                }
            }

            SetProgress(1f, Loc.Get("done_excl"));
            yield return new WaitForSeconds(0.3f);

            OnLoadingComplete?.Invoke();
            PlayerPrefs.SetInt("ft_onboarded", 1);
            PlayerPrefs.Save();

            // Download covers, then show library
            var download = _screens.Get<DownloadScreen>();
            if (download != null && tales != null)
            {
                download.SetTales(tales);
                FadeOutAndNavigate(() => _screens.Show<DownloadScreen>());
            }
            else
            {
                FadeOutAndNavigate(() => _screens.Show<LibraryScreen>());
            }
        }

        private void FadeOutAndNavigate(Action navigate)
        {
            if (background)
            {
                background.DOFade(0f, bgFadeDuration).SetEase(Ease.InQuad)
                    .OnComplete(() => navigate());
            }
            else
            {
                navigate();
            }
        }

        private void SetProgress(float value, string text)
        {
            if (progressBar) progressBar.value = value;
            if (statusText) statusText.text = text;
        }

    }
}
