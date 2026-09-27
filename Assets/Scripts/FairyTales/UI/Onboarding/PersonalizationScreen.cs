using System;
using System.Collections;
using DG.Tweening;
using FairyTales.Api;
using FairyTales.Audio;
using FairyTales.Cache;
using FairyTales.Diagnostics;
using FairyTales.Models;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Core;
using FairyTales.UI.Library;

namespace FairyTales.UI.Onboarding
{
    public class PersonalizationScreen : BaseScreen
    {
        [SerializeField] private Image background;
        [SerializeField] private float bgFadeDuration = 0.5f;
        [SerializeField] private BackgroundMusicManager backgroundMusicManager;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button btnBoy;
        [SerializeField] private Button btnGirl;
        [SerializeField] private Button btnContinue;
        [SerializeField] private Button btnChangeLang;
        [SerializeField] private Button btnMusic;

        [Header("Selection visuals")]
        [SerializeField] private GameObject selectedBoy;
        [SerializeField] private GameObject selectedGirl;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;
        private string _gender = "male";
        private ScreenManager _screens;
        private AuthService _auth;
        private TalesService _tales;

        private void OnMusicToggle()
        {
            if (backgroundMusicManager) backgroundMusicManager.SetMuted(!backgroundMusicManager.IsMuted);
            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
        }

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();

            if (background) SetBgAlpha(0f);
            var api = FindAnyObjectByType<ApiClient>();
            _auth = new AuthService(api);
            _tales = new TalesService(api);

            btnBoy.onClick.AddListener(() => SelectGender("male"));
            btnGirl.onClick.AddListener(() => SelectGender("female"));
            btnContinue.onClick.AddListener(OnContinue);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);
            if (btnChangeLang)
                btnChangeLang.onClick.AddListener(OnChangeLang);
            backgroundMusicManager = FindObjectOfType<BackgroundMusicManager>();
            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
        }

        protected override void OnShown()
        {
            if (background) background.DOFade(1f, bgFadeDuration).SetEase(Ease.OutQuad);
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

        protected override void OnPrepare()
        {
            var savedGender = PlayerPrefs.GetString("ft_gender", "male");
            SelectGender(savedGender);
            nameInput.text = PlayerPrefs.GetString("ft_childName", "");
            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
        }

        private void OnChangeLang()
        {
            if (background)
            {
                background.DOFade(0f, bgFadeDuration).SetEase(Ease.InQuad)
                    .OnComplete(() => _screens.Show<LanguageSelectScreen>());
            }
            else
            {
                _screens.Show<LanguageSelectScreen>();
            }
        }

        private void SelectGender(string gender)
        {
            _gender = gender;
            if (selectedBoy) selectedBoy.SetActive(gender == "male");
            if (selectedGirl) selectedGirl.SetActive(gender == "female");
        }

        private void OnContinue()
        {
            var childName = nameInput.text.Trim();
            if (string.IsNullOrEmpty(childName))
            {
                // RELEASE: Debug.LogWarning("[Personalization] Name is empty");
                return;
            }

            PlayerPrefs.SetString("ft_childName", childName);
            PlayerPrefs.SetString("ft_gender", _gender);
            PlayerPrefs.Save();

            Analytics.RefreshUserProps();
            Analytics.Log("profile_set", ("child_gender", Analytics.Gender()));
            Analytics.Log("onboarding_step",
                ("step_index", 1), ("step_name", "profile"));

            btnContinue.interactable = false;

            // First run: the tales load in the background behind the formats intro, so
            // the tap moves on at once instead of waiting for the server.
            var formats = PlayerPrefs.GetInt("ft_onboarded", 0) == 0
                ? _screens.Get<OnboardingFormatsScreen>()
                : null;
            if (formats != null)
            {
                PreloadAndShowIntro(formats, childName);
                return;
            }

            StartCoroutine(RegisterAndRoute(childName));
        }

        private void PreloadAndShowIntro(OnboardingFormatsScreen formats, string childName)
        {
            var lang = PlayerPrefs.GetString("ft_lang", "ru");

            PlayerPrefs.SetInt("ft_onboarded", 1);
            PlayerPrefs.Save();
            Analytics.Log("onboarding_complete",
                ("child_gender", Analytics.Gender()), ("app_language", lang));

            // Runs on the screen manager: this screen stops its coroutines when it hides.
            var preload = new TalesPreloader(FindAnyObjectByType<ApiClient>());
            _screens.StartCoroutine(preload.Run(AuthService.GetOrCreateUserId(),
                childName, _gender, lang));
            formats.SetPreload(preload);

            btnContinue.interactable = true;
            FadeOutAndNavigate(() => _screens.Show<OnboardingFormatsScreen>());
        }

        private IEnumerator RegisterAndRoute(string childName)
        {
            var userId = AuthService.GetOrCreateUserId();
            var lang = PlayerPrefs.GetString("ft_lang", "ru");

            // Register / login
            yield return _auth.Register(userId, childName, _gender, lang,
                onSuccess: _ => { },
                onError: e => { } /* RELEASE: Debug.LogWarning($"[Personalization] Register: {e}") */);

            // Load tale list
            TaleSummary[] tales = null;
            yield return _tales.GetTales(lang,
                onSuccess: t => tales = t,
                onError: e => { } /* RELEASE: Debug.LogWarning($"[Personalization] Server: {e}") */);

            if (tales == null)
                yield return BundledTaleLoader.LoadManifest(lang, t => tales = t);

            btnContinue.interactable = true;

            if (PlayerPrefs.GetInt("ft_onboarded", 0) == 0)
            {
                PlayerPrefs.SetInt("ft_onboarded", 1);
                PlayerPrefs.Save();
                Analytics.Log("onboarding_complete",
                    ("child_gender", Analytics.Gender()), ("app_language", lang));
            }

            if (tales != null && HasMissingCovers(tales))
            {
                var download = _screens.Get<DownloadScreen>();
                if (download != null)
                {
                    download.SetTales(tales);
                    FadeOutAndNavigate(() => _screens.Show<DownloadScreen>());
                    yield break;
                }
            }

            _screens.Get<LibraryScreen>()?.MarkNeedsRefresh();
            FadeOutAndNavigate(() => _screens.Show<LibraryScreen>());
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

        private bool HasMissingCovers(TaleSummary[] tales)
        {
            foreach (var t in tales)
                if (!AssetCache.IsCoverDownloaded(t.id)) return true;
            return false;
        }

    }
}
