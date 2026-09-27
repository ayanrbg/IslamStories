using DG.Tweening;
using FairyTales.Audio;
using FairyTales.Diagnostics;
using FairyTales.UI.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

namespace FairyTales.UI.Onboarding
{
    public class LanguageSelectScreen : BaseScreen
    {
        [SerializeField] private Image background;
        [SerializeField] private float bgFadeDuration = 0.5f;
        [SerializeField] private BackgroundMusicManager backgroundMusicManager;
        [SerializeField] private Button btnRussian;
        [SerializeField] private Button btnKazakh;
        [SerializeField] private Button btnEnglish;
        [SerializeField] private Button btnContinue;
        [SerializeField] private Button btnMusic;

        [Header("Selection visuals")]
        [SerializeField] private GameObject selectedRu;
        [SerializeField] private GameObject selectedKz;
        [SerializeField] private GameObject selectedEn;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;

        private string _selectedLang = "ru";
        private ScreenManager _screens;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();

            if (background) SetBgAlpha(0f);
            btnRussian.onClick.AddListener(() => SelectLang("ru"));
            btnKazakh.onClick.AddListener(() => SelectLang("kz"));
            if (btnEnglish) btnEnglish.onClick.AddListener(() => SelectLang("en"));
            btnContinue.onClick.AddListener(OnContinue);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);
            backgroundMusicManager = FindObjectOfType<BackgroundMusicManager>();
            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
        }

        protected override void OnPrepare()
        {
            // First launch — auto-detect device language
            if (!PlayerPrefs.HasKey("ft_lang"))
            {
                var detected = Loc.DetectSystemLang();
                SelectLang(detected);
            }
            else
            {
                SelectLang(Loc.Lang);
            }

            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
        }

        protected override void OnShown()
        {
            if (background) background.DOFade(1f, bgFadeDuration).SetEase(Ease.OutQuad);
            if (PlayerPrefs.GetInt("ft_onboarded", 0) == 0)
                Analytics.Log("onboarding_start");
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

        private void SelectLang(string lang)
        {
            _selectedLang = lang;
            if (selectedRu) selectedRu.SetActive(lang == "ru");
            if (selectedKz) selectedKz.SetActive(lang == "kz");
            if (selectedEn) selectedEn.SetActive(lang == "en");
            Loc.ApplyLocale(lang);
        }

        private void OnContinue()
        {
            Loc.Lang = _selectedLang;
            Analytics.Log("onboarding_step",
                ("step_index", 0), ("step_name", "language"));
            if (background)
            {
                background.DOFade(0f, bgFadeDuration).SetEase(Ease.InQuad)
                    .OnComplete(ShowNext);
            }
            else
            {
                ShowNext();
            }
        }

        /// <summary>
        /// First run goes through the intro page before the profile; changing the
        /// language later (from the profile screen) goes straight back to the profile.
        /// </summary>
        private void ShowNext()
        {
            if (PlayerPrefs.GetInt("ft_onboarded", 0) == 0 && _screens.Get<OnboardingValuesScreen>() != null)
                _screens.Show<OnboardingValuesScreen>();
            else
                _screens.Show<PersonalizationScreen>();
        }

        private void OnMusicToggle()
        {
            if (backgroundMusicManager) backgroundMusicManager.SetMuted(!backgroundMusicManager.IsMuted);
            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
        }
    }
}
