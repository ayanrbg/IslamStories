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
        private float _transitionStart;

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

        public void Show<T>() where T : BaseScreen
        {
            // Safety: reset stuck _transitioning flag after 3 seconds
            if (_transitioning && Time.unscaledTime - _transitionStart > 3f)
            {
                Debug.LogWarning("[ScreenManager] _transitioning stuck — force-resetting");
                _transitioning = false;
            }
            if (_transitioning) return;

            if (!_screens.TryGetValue(typeof(T), out var next))
            {
                // RELEASE: Debug.LogError($"[ScreenManager] Screen not found: {typeof(T).Name}");
                return;
            }

            if (next == _current) return;

            var prev = _current;
            _current = next;

            // Both slide-enabled → coordinated swipe-up (move as one unit)
            if (prev != null && prev.SlideEnabled && next.SlideEnabled)
            {
                CoordinatedSlide(prev, next);
                return;
            }

            // Fallback: independent animations
            if (prev != null) prev.Hide();
            next.Show();
        }

        public void Show(System.Type screenType)
        {
            if (_transitioning && Time.unscaledTime - _transitionStart > 3f)
            {
                Debug.LogWarning("[ScreenManager] _transitioning stuck — force-resetting");
                _transitioning = false;
            }
            if (_transitioning) return;

            if (!_screens.TryGetValue(screenType, out var next))
                return;

            if (next == _current) return;

            var prev = _current;
            _current = next;

            if (prev != null && prev.SlideEnabled && next.SlideEnabled)
            {
                CoordinatedSlide(prev, next);
                return;
            }

            if (prev != null) prev.Hide();
            next.Show();
        }

        // Gap (canvas units) kept between the two screens during a coordinated slide.
        private const float SlideGap = 40f;

        private void CoordinatedSlide(BaseScreen prev, BaseScreen next)
        {
            _transitioning = true;
            _transitionStart = Time.unscaledTime;
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
                prev.FinalizeHidden();
                next.FinalizeShown();
                _transitioning = false;
            });
        }

        /// <summary>
        /// Slide next screen in on top of current, then hide current instantly.
        /// No overlap flicker — prev stays as background until next fully covers it.
        /// </summary>
        public void ShowOver<T>() where T : BaseScreen
        {
            if (_transitioning && Time.unscaledTime - _transitionStart > 3f)
            {
                Debug.LogWarning("[ScreenManager] _transitioning stuck (ShowOver) — force-resetting");
                _transitioning = false;
            }
            if (_transitioning) return;
            if (!_screens.TryGetValue(typeof(T), out var next)) return;
            if (next == _current) return;

            var prev = _current;
            _current = next;
            _transitioning = true;
            _transitionStart = Time.unscaledTime;

            next.Show(onComplete: () =>
            {
                prev?.HideImmediate();
                _transitioning = false;
            });
        }

        public void ShowImmediate<T>() where T : BaseScreen
        {
            if (!_screens.TryGetValue(typeof(T), out var next)) return;
            if (next == _current) return;

            _current?.HideImmediate();
            _current = next;
            next.ShowImmediate();
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
