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

        protected CanvasGroup CanvasGroup =>
            _canvasGroup ??= GetComponent<CanvasGroup>();

        private Canvas Canvas =>
            _canvas ??= GetComponent<Canvas>();

        public RectTransform Rect =>
            _rect ??= GetComponent<RectTransform>();

        public bool SlideEnabled => slideFromBottom;
        public float AnimDuration => animDuration;

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
                CanvasGroup.blocksRaycasts = true;
                OnShown();
                onComplete?.Invoke();
            });
        }

        /// <summary>Fade-out (+ slide up if enabled). Used for standalone transitions.</summary>
        public virtual void Hide(Action onComplete = null)
        {
            CanvasGroup.blocksRaycasts = false;

            var seq = DOTween.Sequence();
            seq.Append(CanvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuad));

            if (slideFromBottom)
                seq.Join(Rect.DOAnchorPosY(Rect.rect.height, animDuration).SetEase(Ease.InCubic));

            seq.OnComplete(() =>
            {
                StopAllCoroutines();
                Canvas.enabled = false;
                Rect.anchoredPosition = Vector2.zero;
                OnHidden();
                onComplete?.Invoke();
            });
        }

        /// <summary>Prepare screen before coordinated slide (activate, set alpha, position).</summary>
        public void PrepareBelow()
        {
            Canvas.enabled = true;
            OnPrepare();
            CanvasGroup.alpha = 1f;
            CanvasGroup.blocksRaycasts = false;
            Rect.anchoredPosition = new Vector2(0f, -Rect.rect.height);
        }

        /// <summary>Finalize after coordinated slide-in completed.</summary>
        public void FinalizeShown()
        {
            Rect.anchoredPosition = Vector2.zero;
            CanvasGroup.blocksRaycasts = true;
            OnShown();
        }

        /// <summary>Finalize after coordinated slide-out completed.</summary>
        public void FinalizeHidden()
        {
            StopAllCoroutines();
            CanvasGroup.alpha = 0f;
            CanvasGroup.blocksRaycasts = false;
            Canvas.enabled = false;
            Rect.anchoredPosition = Vector2.zero;
            OnHidden();
        }

        public void ShowImmediate()
        {
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
