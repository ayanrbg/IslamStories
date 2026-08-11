using System;
using DG.Tweening;
using UnityEngine;

namespace FairyTales.UI.Core
{
    [RequireComponent(typeof(CanvasGroup), typeof(Canvas))]
    public abstract class BaseScreen : MonoBehaviour
    {
        [SerializeField] private float animDuration = 0.4f;
        [SerializeField] protected bool slideFromBottom = true;

        private CanvasGroup _canvasGroup;
        private Canvas _canvas;
        private RectTransform _rect;
        private Sequence _seq;

        protected CanvasGroup CanvasGroup =>
            _canvasGroup ??= GetComponent<CanvasGroup>();

        private Canvas Canvas =>
            _canvas ??= GetComponent<Canvas>();

        public RectTransform Rect =>
            _rect ??= GetComponent<RectTransform>();

        public bool SlideEnabled => slideFromBottom;
        public float AnimDuration => animDuration;

        /// <summary>True while this screen is actually drawn.</summary>
        public bool IsVisible => Canvas.enabled;

        /// <summary>
        /// Run any in-flight show/hide animation straight to its end, callbacks included.
        /// Screens are only hidden inside a tween's OnComplete, so an interrupted tween
        /// would otherwise leave this screen visible on top of the next one forever.
        /// </summary>
        public void CompleteAnimation()
        {
            var seq = _seq;
            _seq = null;
            if (seq != null && seq.IsActive()) seq.Complete(true);
        }

        /// <summary>Drop an in-flight animation without running its callbacks — for the
        /// immediate/finalize paths that set the end state themselves.</summary>
        private void KillAnimation()
        {
            var seq = _seq;
            _seq = null;
            if (seq != null && seq.IsActive()) seq.Kill();
        }

        /// <summary>
        /// Activate the GameObject once so coroutines work.
        /// All further show/hide toggles Canvas.enabled (cheap, no rebuild).
        /// </summary>
        public void WarmUp()
        {
            gameObject.SetActive(true);
            Canvas.enabled = false;
            CanvasGroup.alpha = 0f;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = false;
        }

        /// <summary>Fade-in (+ slide up if enabled). Used for standalone transitions.</summary>
        public virtual void Show(Action onComplete = null)
        {
            CompleteAnimation();
            Canvas.enabled = true;
            OnPrepare();
            CanvasGroup.alpha = 0f;
            CanvasGroup.blocksRaycasts = false;

            if (slideFromBottom)
                Rect.anchoredPosition = new Vector2(0f, -Rect.rect.height);

            var seq = DOTween.Sequence();
            seq.Append(CanvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuad));

            if (slideFromBottom)
                seq.Join(Rect.DOAnchorPosY(0f, animDuration).SetEase(Ease.OutCubic));

            seq.OnComplete(() =>
            {
                _seq = null;
                CanvasGroup.blocksRaycasts = true;
                OnShown();
                onComplete?.Invoke();
            });
            _seq = seq;
        }

        /// <summary>Fade-out (+ slide up if enabled). Used for standalone transitions.</summary>
        public virtual void Hide(Action onComplete = null)
        {
            CompleteAnimation();
            CanvasGroup.blocksRaycasts = false;

            var seq = DOTween.Sequence();
            seq.Append(CanvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuad));

            if (slideFromBottom)
                seq.Join(Rect.DOAnchorPosY(Rect.rect.height, animDuration).SetEase(Ease.InCubic));

            seq.OnComplete(() =>
            {
                _seq = null;
                StopAllCoroutines();
                Canvas.enabled = false;
                Rect.anchoredPosition = Vector2.zero;
                OnHidden();
                onComplete?.Invoke();
            });
            _seq = seq;
        }

        /// <summary>Prepare screen before coordinated slide (activate, set alpha, position).</summary>
        public void PrepareBelow()
        {
            KillAnimation();
            Canvas.enabled = true;
            OnPrepare();
            CanvasGroup.alpha = 1f;
            CanvasGroup.blocksRaycasts = false;
            Rect.anchoredPosition = new Vector2(0f, -Rect.rect.height);
        }

        /// <summary>Finalize after coordinated slide-in completed.</summary>
        public void FinalizeShown()
        {
            KillAnimation();
            Rect.anchoredPosition = Vector2.zero;
            CanvasGroup.blocksRaycasts = true;
            OnShown();
        }

        /// <summary>Finalize after coordinated slide-out completed.</summary>
        public void FinalizeHidden()
        {
            KillAnimation();
            StopAllCoroutines();
            CanvasGroup.alpha = 0f;
            CanvasGroup.blocksRaycasts = false;
            Canvas.enabled = false;
            Rect.anchoredPosition = Vector2.zero;
            OnHidden();
        }

        public void ShowImmediate()
        {
            KillAnimation();
            gameObject.SetActive(true);
            Canvas.enabled = true;
            OnPrepare();
            Rect.anchoredPosition = Vector2.zero;
            CanvasGroup.alpha = 1f;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
            OnShown();
        }

        public void HideImmediate()
        {
            KillAnimation();
            StopAllCoroutines();
            CanvasGroup.alpha = 0f;
            CanvasGroup.blocksRaycasts = false;
            Canvas.enabled = false;
            Rect.anchoredPosition = Vector2.zero;
            OnHidden();
        }

        /// <summary>Called before animation starts. Populate UI here so it's visible during transition.</summary>
        protected virtual void OnPrepare() { }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
