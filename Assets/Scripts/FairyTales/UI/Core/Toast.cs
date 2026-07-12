using DG.Tweening;
using TMPro;
using UnityEngine;

namespace FairyTales.UI.Core
{
    public class Toast : MonoBehaviour
    {
        private static Toast _instance;

        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float displayDuration = 2f;

        private Tween _tween;

        private void Awake()
        {
            _instance = this;
            if (canvasGroup) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public static void Show(string message)
        {
            if (_instance == null)
            {
                // RELEASE: Debug.Log($"[Toast] {message}");
                return;
            }
            _instance.Display(message);
        }

        private void Display(string message)
        {
            _tween?.Kill();
            if (label) label.text = message;
            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;

            _tween = DOTween.Sequence()
                .Append(canvasGroup.DOFade(1f, 0.2f))
                .AppendInterval(displayDuration)
                .Append(canvasGroup.DOFade(0f, 0.3f))
                .OnComplete(() => gameObject.SetActive(false));
        }
    }
}
