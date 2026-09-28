using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// Yes / No question over the current screen. Kept disabled in the scene so it
    /// doesn't cover the layout while editing — found on first use instead of in Awake.
    /// </summary>
    public class ConfirmPopup : MonoBehaviour
    {
        private static ConfirmPopup _instance;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text yesText;
        [SerializeField] private TMP_Text noText;
        [SerializeField] private Button btnYes;
        [SerializeField] private Button btnNo;
        [SerializeField] private Button btnOverlay;

        private Action _onYes;

        private void Awake()
        {
            if (btnYes) btnYes.onClick.AddListener(OnYes);
            if (btnNo) btnNo.onClick.AddListener(OnNo);
            if (btnOverlay) btnOverlay.onClick.AddListener(OnNo);
        }

        public static void Show(string message, Action onYes)
        {
            if (_instance == null)
                _instance = FindAnyObjectByType<ConfirmPopup>(FindObjectsInactive.Include);
            if (_instance == null)
            {
                onYes?.Invoke();
                return;
            }
            _instance.Present(message, onYes);
        }

        private void Present(string message, Action onYes)
        {
            _onYes = onYes;
            if (messageText) messageText.text = message;
            if (yesText) yesText.text = Loc.Get("yes");
            if (noText) noText.text = Loc.Get("no");

            gameObject.SetActive(true);
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        private void OnYes()
        {
            var cb = _onYes;
            _onYes = null;
            Close(cb);
        }

        private void OnNo()
        {
            _onYes = null;
            Close(null);
        }

        private void Close(Action onComplete)
        {
            canvasGroup.interactable = false;
            canvasGroup.DOKill();
            canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }
    }
}
