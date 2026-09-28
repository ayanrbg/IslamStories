using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FairyTales.UI.Library;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace FairyTales.UI.Core
{
    public class ScreenManager : MonoBehaviour
    {
        [SerializeField] private BaseScreen initialScreen;
        [SerializeField] private BaseScreen onboardedScreen;
        [SerializeField] private Image sharedBackground;

        private AspectRatioFitter _bgArf;
        private readonly Dictionary<System.Type, BaseScreen> _screens = new();
        private BaseScreen _current;
        private bool _transitioning;
        private Sequence _slideSeq;

        // Latest navigation asked for while a transition was in flight. Replayed once
        // that transition finishes on its own, so no animation is ever cut short.
        private System.Type _pendingType;
        private bool _pendingOver;

        // Wall-clock guard: a tween killed from outside never fires its OnComplete, which
        // would leave _transitioning stuck and dead-end every later navigation.
        private float _transitionDeadline;

        private void Awake()
        {
            Cache.AssetCache.MigrateIfNeeded();

            // Activate all screens once (WarmUp keeps GameObject active,
            // disables Canvas.enabled) — avoids expensive SetActive rebuild later.
            var all = GetComponentsInChildren<BaseScreen>(true);
            foreach (var screen in all)
            {
                _screens[screen.GetType()] = screen;
                screen.WarmUp();
            }

            if (sharedBackground != null)
            {
                sharedBackground.gameObject.SetActive(true);
                _bgArf = sharedBackground.GetComponent<AspectRatioFitter>();
            }
        }

        private void Start()
        {
            var start = initialScreen;

            if (!string.IsNullOrEmpty(PlayerPrefs.GetString("ft_childName", "")))
            {
                // Skip loading screen — LibraryScreen handles register + tales fetch
                if (_screens.TryGetValue(typeof(LibraryScreen), out var lib))
                    start = lib;
                else if (onboardedScreen != null)
                    start = onboardedScreen;
            }

            if (start != null) start.ShowImmediate();
            _current = start;

            StartCoroutine(StartupRoutine());
        }

        private IEnumerator StartupRoutine()
        {
            // Build resource index after first frame so it doesn't block startup
            yield return Cache.AssetCache.InitResourceIndexAsync();
            yield return ApplyLocaleOnReady();
        }

        private IEnumerator ApplyLocaleOnReady()
        {
            yield return LocalizationSettings.InitializationOperation;
            Loc.ApplyLocale(Loc.Lang);
        }

        public void Show<T>() where T : BaseScreen => Show(typeof(T));

        public void Show(System.Type screenType)
        {
            if (IsBusy())
            {
                _pendingType = screenType;
                _pendingOver = false;
                return;
            }

            if (!_screens.TryGetValue(screenType, out var next))
            {
                // RELEASE: Debug.LogError($"[ScreenManager] Screen not found: {screenType.Name}");
                return;
            }

            if (next == _current) return;

            var prev = _current;
            _current = next;
            BeginTransition(prev, next);
        }

        /// <summary>
        /// True while a transition is still playing and should be left alone. Past its
        /// deadline the transition is force-settled instead, so a tween that was killed
        /// without running its callbacks can't freeze navigation for good.
        /// </summary>
        private bool IsBusy()
        {
            if (!_transitioning) return false;
            if (Time.unscaledTime < _transitionDeadline) return true;

            SettleTransition();
            return false;
        }

        /// <summary>Marks a transition as started and blocks input on every screen for its duration.</summary>
        private void MarkTransitionStarted(float duration)
        {
            _transitioning = true;
            _transitionDeadline = Time.unscaledTime + duration + 0.5f;
            foreach (var screen in _screens.Values) screen.SetInputEnabled(false);
        }

        /// <summary>Runs the navigation that was requested mid-transition, now that the screens are settled.</summary>
        private void FlushPending()
        {
            var type = _pendingType;
            if (type == null) return;

            _pendingType = null;
            if (_pendingOver) ShowOver(type);
            else Show(type);
        }

        /// <summary>
        /// Single entry point for a screen swap. Both paths mark the manager as
        /// transitioning and end by asserting that only the incoming screen is drawn.
        /// </summary>
        private void BeginTransition(BaseScreen prev, BaseScreen next)
        {
            // Both slide-enabled → coordinated swipe-up (move as one unit)
            if (prev != null && prev.SlideEnabled && next.SlideEnabled)
            {
                CoordinatedSlide(prev, next);
                return;
            }

            // Fallback: independent animations. This path used to leave _transitioning
            // unset, so a second Show() could start on top of it and strand both screens.
            MarkTransitionStarted(Mathf.Max(next.AnimDuration, prev != null ? prev.AnimDuration : 0f));
            if (prev != null) prev.Hide();
            next.Show(onComplete: () =>
            {
                _transitioning = false;
                HideAllExcept(next);
                FlushPending();
            });
        }

        /// <summary>
        /// Run any transition still in flight straight to its end, callbacks included, so
        /// the screens are in a settled state before a new one starts. Dropping the request
        /// instead (the old behaviour) lost the navigation and could strand the outgoing
        /// screen visible on top of the incoming one, since screens are only hidden inside
        /// a tween's OnComplete.
        ///
        /// This snaps the animation, so it is only for paths that are immediate by contract
        /// or for the stuck-transition guard — never for ordinary navigation, which queues
        /// itself and waits instead.
        /// </summary>
        private void SettleTransition()
        {
            if (!_transitioning) return;

            // Drop the queued navigation before snapping: Complete(true) runs the tween's
            // OnComplete synchronously, and a FlushPending() from in there would start a new
            // transition that the rest of this method then tears down. Every caller of this
            // has a newer destination in hand anyway.
            _pendingType = null;

            var seq = _slideSeq;
            _slideSeq = null;
            if (seq != null && seq.IsActive()) seq.Complete(true);

            // Independent-animation path: no shared sequence, finish each screen's own.
            foreach (var screen in _screens.Values) screen.CompleteAnimation();

            _transitioning = false;

            // A tween killed from outside never runs its OnComplete, so the input block it
            // was meant to lift can outlive it. Restore the visible screen by hand.
            if (_current != null)
            {
                _current.SetInputEnabled(true);
                HideAllExcept(_current);
            }
        }

        /// <summary>
        /// Last-resort invariant: exactly one screen is drawn. Guards against a screen left
        /// visible by a path that never completed.
        /// </summary>
        private void HideAllExcept(BaseScreen keep)
        {
            foreach (var screen in _screens.Values)
                if (screen != keep && screen.IsVisible) screen.HideImmediate();
        }

        // Gap (canvas units) kept between the two screens during a coordinated slide.
        private const float SlideGap = 40f;

        private void CoordinatedSlide(BaseScreen prev, BaseScreen next)
        {
            var duration = next.AnimDuration;
            MarkTransitionStarted(duration);

            prev.Rect.anchoredPosition = Vector2.zero;
            next.PrepareBelow();
            // Push incoming screen an extra SlideGap below so a small space
            // stays between them as they travel together. Screens that let content
            // hang past their edge while moving (the library finishes the card row the
            // screen edge cuts through) get that much more room, so nothing overlaps.
            var gap = SlideGap + prev.BeginSlideOut();
            next.Rect.anchoredPosition = new Vector2(0f, -(next.Rect.rect.height + gap));

            var seq = DOTween.Sequence();
            seq.Append(prev.Rect.DOAnchorPosY(prev.Rect.rect.height + gap, duration).SetEase(Ease.InOutCubic));
            seq.Join(next.Rect.DOAnchorPosY(0f, duration).SetEase(Ease.InOutCubic));
            seq.OnComplete(() =>
            {
                _slideSeq = null;
                prev.FinalizeHidden();
                next.FinalizeShown();
                _transitioning = false;
                HideAllExcept(next);
                FlushPending();
            });
            _slideSeq = seq;
        }

        /// <summary>
        /// Slide next screen in on top of current, then hide current instantly.
        /// No overlap flicker — prev stays as background until next fully covers it.
        /// </summary>
        public void ShowOver<T>() where T : BaseScreen => ShowOver(typeof(T));

        public void ShowOver(System.Type screenType)
        {
            if (IsBusy())
            {
                _pendingType = screenType;
                _pendingOver = true;
                return;
            }

            if (!_screens.TryGetValue(screenType, out var next)) return;
            if (next == _current) return;

            var prev = _current;
            _current = next;
            MarkTransitionStarted(next.AnimDuration);

            next.Show(onComplete: () =>
            {
                prev?.HideImmediate();
                _transitioning = false;
                HideAllExcept(next);
                FlushPending();
            });
        }

        public void ShowImmediate<T>() where T : BaseScreen
        {
            // Immediate by contract: snaps whatever is in flight instead of waiting for it.
            SettleTransition();
            if (!_screens.TryGetValue(typeof(T), out var next)) return;
            if (next == _current) return;

            _current?.HideImmediate();
            _current = next;
            next.ShowImmediate();
            HideAllExcept(next);
        }

        public T Get<T>() where T : BaseScreen
        {
            _screens.TryGetValue(typeof(T), out var screen);
            return screen as T;
        }

        public BaseScreen Current => _current;

        /// <summary>
        /// Sets shared background sprite with proper aspect ratio scaling.
        /// Pass null to clear (reverts to solid color).
        /// </summary>
        public void SetBackground(Sprite sprite)
        {
            if (sharedBackground == null) return;

            sharedBackground.sprite = sprite;
            sharedBackground.color = sprite != null ? Color.white : new Color(0.12f, 0.08f, 0.18f);

            if (_bgArf && sprite != null)
                _bgArf.aspectRatio = (float)sprite.texture.width / sprite.texture.height;
        }
    }
}
