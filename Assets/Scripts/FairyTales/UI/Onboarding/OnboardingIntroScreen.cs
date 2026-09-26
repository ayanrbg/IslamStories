using DG.Tweening;
using FairyTales.Audio;
using FairyTales.Diagnostics;
using FairyTales.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.UI.Onboarding
{
    /// <summary>
    /// A first-run intro page: a title, some copy and a "Continue" button, on the same
    /// fading background and music toggle as the other onboarding screens. Subclasses
    /// only fill the texts and say where Continue leads.
    /// </summary>
    public abstract class OnboardingIntroScreen : BaseScreen
    {
        [SerializeField] private Image background;
        [SerializeField] private float bgFadeDuration = 0.5f;
        [SerializeField] private Button btnContinue;
        [SerializeField] private Button btnMusic;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;
        [SerializeField] protected TMP_Text txtTitle;

        protected ScreenManager Screens { get; private set; }
        private BackgroundMusicManager _music;

        /// <summary>Analytics name of this page.</summary>
        protected abstract string StepName { get; }

        /// <summary>Fill every label for the current language.</summary>
        protected abstract void ApplyTexts();

        /// <summary>Navigate onwards; called once the background has faded out.</summary>
        protected abstract void GoNext();

        protected virtual void Awake()
        {
            Screens = GetComponentInParent<ScreenManager>();

            if (background) SetBgAlpha(0f);
            btnContinue.onClick.AddListener(OnContinue);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);
            _music = FindAnyObjectByType<BackgroundMusicManager>();
        }

        protected override void OnPrepare()
        {
            ApplyTexts();
            RefreshMusicIcon();
            btnContinue.interactable = true;
        }

        protected override void OnShown()
        {
            if (background) background.DOFade(1f, bgFadeDuration).SetEase(Ease.OutQuad);
            Analytics.Log("onboarding_intro", ("step_name", StepName));
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

        private void OnContinue()
        {
            // One tap is one navigation: a second tap during the fade would queue another.
            btnContinue.interactable = false;
            if (background)
            {
                background.DOFade(0f, bgFadeDuration).SetEase(Ease.InQuad)
                    .OnComplete(GoNext);
            }
            else
            {
                GoNext();
            }
        }

        private void OnMusicToggle()
        {
            if (_music) _music.SetMuted(!_music.IsMuted);
            RefreshMusicIcon();
        }

        private void RefreshMusicIcon()
        {
            if (!_music) return;
            if (selectionBtnMusicOff) selectionBtnMusicOff.SetActive(_music.IsMuted);
            if (selectionBtnMusicOn) selectionBtnMusicOn.SetActive(!_music.IsMuted);
        }
    }
}
