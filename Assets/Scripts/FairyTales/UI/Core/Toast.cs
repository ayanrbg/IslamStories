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
        private bool _showing;

        private void Awake()
        {
            _instance = this;
            if (canvasGroup) canvasGroup.alpha = 0f;
            // Never hide the object out from under a Show() that just activated it.
            if (!_showing) gameObject.SetActive(false);
        }

        public static void Show(string message)
        {
            var toast = Resolve();
            if (toast == null)
            {
                Debug.LogWarning($"[Toast] No Toast in scene, message dropped: {message}");
                return;
            }
            toast.Display(message);
        }

        /// <summary>
        /// Awake only runs on an active GameObject, so a Toast authored disabled in the
        /// scene would never register itself and every Show() would silently do nothing —
        /// indistinguishable, on screen, from a dead button. Find it either way.
        /// </summary>
        private static Toast Resolve()
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<Toast>(FindObjectsInactive.Include);
            return _instance;
        }

        private void Display(string message)
        {
            _tween?.Kill();
            if (label) label.text = message;

            _showing = true;
            gameObject.SetActive(true);

            if (!canvasGroup)
            {
                // No fade available — still show the message, then hide it on schedule.
                _tween = DOVirtual.DelayedCall(displayDuration, Finish, true);
                return;
            }

            canvasGroup.alpha = 0f;
            _tween = DOTween.Sequence()
                .Append(canvasGroup.DOFade(1f, 0.2f))
                .AppendInterval(displayDuration)
                .Append(canvasGroup.DOFade(0f, 0.3f))
                .OnComplete(Finish);
        }

        private void Finish()
        {
            _showing = false;
            gameObject.SetActive(false);
        }
    }
}
