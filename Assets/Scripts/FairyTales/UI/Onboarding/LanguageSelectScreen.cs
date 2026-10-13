using System;
using System.Collections;
using DG.Tweening;
using FairyTales.Api;
using FairyTales.Audio;
using FairyTales.Diagnostics;
using FairyTales.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.UI.Onboarding
{
    public class LanguageSelectScreen : BaseScreen
    {
        /// <summary>One flag tile; <see cref="lang"/> is the code GET /api/languages uses.</summary>
        [Serializable]
        private class LangTile
        {
            public string lang;
            public Button button;
            public GameObject selection;
        }

        [SerializeField] private Image background;
        [SerializeField] private float bgFadeDuration = 0.5f;
        [SerializeField] private BackgroundMusicManager backgroundMusicManager;
        [SerializeField] private LangTile[] tiles;
        [SerializeField] private Button btnContinue;
        [Tooltip("Hidden on first launch: there is nothing to go back to yet.")]
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnMusic;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;

        private LangTile _selected;
        private ScreenManager _screens;

        private bool IsFirstRun => PlayerPrefs.GetInt("ft_onboarded", 0) == 0;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();

            if (background) SetBgAlpha(0f);
            foreach (var tile in tiles)
                tile.button.onClick.AddListener(() => Select(tile));
            btnContinue.onClick.AddListener(OnContinue);
            if (btnClose) btnClose.onClick.AddListener(OnClose);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);
            backgroundMusicManager = FindObjectOfType<BackgroundMusicManager>();
            RefreshMusic();
        }

        protected override void OnPrepare()
        {
            // First launch — auto-detect device language
            var lang = PlayerPrefs.HasKey("ft_lang") ? Loc.Lang : Loc.DetectSystemLang();
            Select(Array.Find(tiles, t => t.lang == lang) ?? Array.Find(tiles, t => t.lang == "ru"));
            RefreshTiles();
            StartCoroutine(RefreshCatalog());

            if (btnClose) btnClose.gameObject.SetActive(!IsFirstRun);
            RefreshMusic();
        }

        protected override void OnShown()
        {
            if (background) background.DOFade(1f, bgFadeDuration).SetEase(Ease.OutQuad);
            if (IsFirstRun)
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

        private IEnumerator RefreshCatalog()
        {
            yield return LanguageCatalog.Refresh(FindAnyObjectByType<ApiClient>());
            RefreshTiles();
        }

        /// <summary>
        /// A language the server doesn't serve, or this build can't draw, keeps its place
        /// in the layout but is hidden. The current one always stays, so it can't vanish
        /// from under the user.
        /// </summary>
        private void RefreshTiles()
        {
            foreach (var t in tiles)
                t.button.gameObject.SetActive(t == _selected || LanguageCatalog.IsOffered(t.lang));
        }

        private void Select(LangTile tile)
        {
            if (tile == null) return;
            _selected = tile;
            foreach (var t in tiles)
                if (t.selection) t.selection.SetActive(t == tile);
            // Preview: the screen's own captions switch at once.
            Loc.ApplyLocale(tile.lang);
        }

        private void OnContinue()
        {
            if (_selected == null) return;
            Loc.Lang = _selected.lang;
            Analytics.Log("onboarding_step",
                ("step_index", 0), ("step_name", "language"));
            FadeOutThen(ShowNext);
        }

        /// <summary>Back to the profile with the language unchanged.</summary>
        private void OnClose()
        {
            Loc.ApplyLocale(Loc.Lang);
            FadeOutThen(() => _screens.Show<PersonalizationScreen>());
        }

        private void FadeOutThen(TweenCallback next)
        {
            if (background)
                background.DOFade(0f, bgFadeDuration).SetEase(Ease.InQuad).OnComplete(next);
            else
                next();
        }

        /// <summary>
        /// First run goes through the intro page before the profile; changing the
        /// language later (from the profile screen) goes straight back to the profile.
        /// </summary>
        private void ShowNext()
        {
            if (IsFirstRun && _screens.Get<OnboardingValuesScreen>() != null)
                _screens.Show<OnboardingValuesScreen>();
            else
                _screens.Show<PersonalizationScreen>();
        }

        private void OnMusicToggle()
        {
            if (backgroundMusicManager) backgroundMusicManager.SetMuted(!backgroundMusicManager.IsMuted);
            RefreshMusic();
        }

        private void RefreshMusic()
        {
            bool muted = backgroundMusicManager && backgroundMusicManager.IsMuted;
            if (selectionBtnMusicOff) selectionBtnMusicOff.SetActive(muted);
            if (selectionBtnMusicOn) selectionBtnMusicOn.SetActive(!muted);
        }
    }
}
