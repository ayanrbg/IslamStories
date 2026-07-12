using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Core;

namespace FairyTales.UI.Library
{
    public class VoiceSelectScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button btnOverlay;
        [SerializeField] private Button btnVoiceMale;
        [SerializeField] private Button btnVoiceFemale;
        [SerializeField] private Button btnVoiceParent;
        [SerializeField] private float animDuration = 0.4f;

        private static VoiceSelectScreen _instance;
        private Action<string, string> _onSelected;
        private RectTransform _rect;

        private void Awake()
        {
            _instance = this;
            _rect = GetComponent<RectTransform>();

            if (btnOverlay) btnOverlay.onClick.AddListener(Hide);
            if (btnVoiceMale) btnVoiceMale.onClick.AddListener(() => Select("narrator", "male"));
            if (btnVoiceFemale) btnVoiceFemale.onClick.AddListener(() => Select("narrator", "female"));
            if (btnVoiceParent) btnVoiceParent.onClick.AddListener(() => Select(null, null));

            // Start hidden but keep GameObject active so Awake runs
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            _rect.anchoredPosition = new Vector2(0f, -Screen.height);
        }

        public static void Show(Action<string, string> onSelected)
        {
            if (_instance == null) return;
            _instance.Present(onSelected);
        }

        private void Present(Action<string, string> onSelected)
        {
            _onSelected = onSelected;

            if (titleText) titleText.text = Loc.Get("choose_voice");

            bool hasClone = PlayerPrefs.GetInt("ft_voiceCloned", 0) == 1;
            if (btnVoiceParent) btnVoiceParent.interactable = hasClone;

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            _rect.anchoredPosition = new Vector2(0f, -Screen.height);

            var seq = DOTween.Sequence();
            seq.Append(_rect.DOAnchorPosY(0f, animDuration).SetEase(Ease.OutCubic));
            seq.OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        private void Select(string voice, string narratorGender)
        {
            var cb = _onSelected;
            _onSelected = null;
            SlideOut(() => cb?.Invoke(voice, narratorGender));
        }

        private void SlideOut(Action onComplete = null)
        {
            canvasGroup.interactable = false;

            var seq = DOTween.Sequence();
            seq.Append(_rect.DOAnchorPosY(Screen.height, animDuration).SetEase(Ease.InCubic));
            seq.OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
                _rect.anchoredPosition = new Vector2(0f, -Screen.height);
                onComplete?.Invoke();
            });
        }

        private void Hide() => SlideOut(null);
    }
}
