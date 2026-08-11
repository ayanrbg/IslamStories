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

        /// <summary>
        /// Run any transition still in flight straight to its end, callbacks included, so
        /// the screens are in a settled state before a new one starts. Dropping the request
        /// instead (the old behaviour) lost the navigation and could strand the outgoing
        /// screen visible on top of the incoming one, since screens are only hidden inside
        /// a tween's OnComplete.
        /// </summary>
        private void SettleTransition()
        {
            if (!_transitioning) return;

            var seq = _slideSeq;
            _slideSeq = null;
            if (seq != null && seq.IsActive()) seq.Complete(true);

            // Independent-animation path: no shared sequence, finish each screen's own.
            foreach (var screen in _screens.Values) screen.CompleteAnimation();

            _transitioning = false;
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

        public void Show<T>() where T : BaseScreen
        {
            SettleTransition();

            if (!_screens.TryGetValue(typeof(T), out var next))
            {
                // RELEASE: Debug.LogError($"[ScreenManager] Screen not found: {typeof(T).Name}");
                return;
            }

            if (next == _current) return;

            var prev = _current;
            _current = next;
            BeginTransition(prev, next);
        }

        public void Show(System.Type screenType)
        {
            SettleTransition();

            if (!_screens.TryGetValue(screenType, out var next))
                return;

            if (next == _current) return;

            var prev = _current;
            _current = next;
            BeginTransition(prev, next);
        }

        /// <summary>
        /// Single entry point for a screen swap. Both paths mark the manager as
        /// transitioning and end by asserting that only the incoming screen is drawn.
        /// </summary>
        private void BeginTransition(BaseScreen prev, BaseScreen next)
        {
            if (prev != null && prev.SlideEnabled && next.SlideEnabled)
            {
                // Both slide-enabled → coordinated swipe-up (move as one unit)
                CoordinatedSlide(prev, next);
                return;
            }

            // Fallback: independent animations. This path used to leave _transitioning
            // unset, so a second Show() could start on top of it and strand both screens.
            _transitioning = true;
            prev?.Hide();
            next.Show(onComplete: () =>
            {
                _transitioning = false;
                HideAllExcept(next);
            });
        }

        // Gap (canvas units) kept between the two screens during a coordinated slide.
        private const float SlideGap = 40f;

        private void CoordinatedSlide(BaseScreen prev, BaseScreen next)
        {
            _transitioning = true;
            var duration = next.AnimDuration;

            prev.Rect.anchoredPosition = Vector2.zero;
            next.PrepareBelow();
            // Push incoming screen an extra SlideGap below so a small space
            // stays between them as they travel together.
            next.Rect.anchoredPosition = new Vector2(0f, -(next.Rect.rect.height + SlideGap));

            var seq = DOTween.Sequence();
            seq.Append(prev.Rect.DOAnchorPosY(prev.Rect.rect.height + SlideGap, duration).SetEase(Ease.InOutCubic));
            seq.Join(next.Rect.DOAnchorPosY(0f, duration).SetEase(Ease.InOutCubic));
            seq.OnComplete(() =>
            {
                _slideSeq = null;
                prev.FinalizeHidden();
                next.FinalizeShown();
                _transitioning = false;
                HideAllExcept(next);
            });
            _slideSeq = seq;
        }

        /// <summary>
        /// Slide next screen in on top of current, then hide current instantly.
        /// No overlap flicker — prev stays as background until next fully covers it.
        /// </summary>
        public void ShowOver<T>() where T : BaseScreen
        {
            SettleTransition();

            if (!_screens.TryGetValue(typeof(T), out var next)) return;
            if (next == _current) return;

            var prev = _current;
            _current = next;
            _transitioning = true;

            next.Show(onComplete: () =>
            {
                prev?.HideImmediate();
                _transitioning = false;
                HideAllExcept(next);
            });
        }

        public void ShowImmediate<T>() where T : BaseScreen
        {
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
