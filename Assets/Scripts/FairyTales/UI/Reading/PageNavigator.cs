using System;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FairyTales.UI.Reading
{
    public class PageNavigator : MonoBehaviour
    {
        [SerializeField] private Image illustrationImage;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private float fadeDuration = 0.35f;

        private AspectRatioFitter _arf;
        private Image _bufferImage;
        private AspectRatioFitter _bufferArf;

        private static readonly Regex GenderTag =
            new(@"\{m:([^|]*)\|f:([^}]*)\}", RegexOptions.Compiled);

        private string _taleId;
        private string[] _pages;
        private string _gender;
        private int[] _genderedPages;
        private int _currentPage;
        private bool _transitioning;
        private Tween _activeTween;

        private float _baseFontSize;
        private RectTransform _pageTextRect;
        private bool _basesCaptured;

        [SerializeField] private RectTransform textPanel;
        private Vector2 _basePanelSizeDelta;
        private Vector2 _basePanelPosition;
        private float _currentScale = 1f;

        public int CurrentPage => _currentPage;
        public int TotalPages => _pages?.Length ?? 0;
        public event Action<int> OnPageChanged;

        public void Init(string taleId, string[] pages, int startPage = 0,
            Sprite initialSprite = null, int[] genderedPages = null)
        {
            _taleId = taleId;
            _pages = pages;
            _gender = PlayerPrefs.GetString("ft_gender", "male");
            _genderedPages = genderedPages;
            _currentPage = Mathf.Clamp(startPage, 0, pages.Length - 1);
            if (illustrationImage)
            {
                _arf = illustrationImage.GetComponent<AspectRatioFitter>();
                if (_arf)
                {
                    _arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    _arf.aspectRatio = 2f;
                }
            }

            if (pageText)
            {
                _pageTextRect = pageText.GetComponent<RectTransform>();
                if (!_basesCaptured)
                {
                    _baseFontSize = pageText.fontSize;
                    if (textPanel != null)
                    {
                        _basePanelSizeDelta = textPanel.sizeDelta;
                        _basePanelPosition = textPanel.anchoredPosition;
                    }

                    // Restructure: TextContent and PageText stretch to fill TextPanel
                    var textContentRect = _pageTextRect.parent as RectTransform;
                    if (textContentRect != null)
                    {
                        // Remove ContentSizeFitter — we control size via TextPanel
                        var csf = textContentRect.GetComponent<ContentSizeFitter>();
                        if (csf) Destroy(csf);

                        // Stretch TextContent to fill TextPanel with padding
                        textContentRect.anchorMin = new Vector2(0.05f, 0.05f);
                        textContentRect.anchorMax = new Vector2(0.95f, 0.95f);
                        textContentRect.offsetMin = Vector2.zero;
                        textContentRect.offsetMax = Vector2.zero;
                    }

                    // Stretch PageText to fill TextContent
                    _pageTextRect.anchorMin = Vector2.zero;
                    _pageTextRect.anchorMax = Vector2.one;
                    _pageTextRect.offsetMin = Vector2.zero;
                    _pageTextRect.offsetMax = Vector2.zero;

                    _basesCaptured = true;
                }
            }

            // Show passed sprite instantly before async load
            if (initialSprite != null && illustrationImage)
                SetIllustration(initialSprite);

            EnsureBuffer();
            ApplyPage(_currentPage);
        }

        public void NextPage()
        {
            if (_transitioning || _currentPage >= TotalPages - 1) return;
            GoToPage(_currentPage + 1);
        }

        public void PrevPage()
        {
            if (_transitioning || _currentPage <= 0) return;
            GoToPage(_currentPage - 1);
        }

        public void GoToPage(int page)
        {
            page = Mathf.Clamp(page, 0, TotalPages - 1);
            if (page == _currentPage) return;

            _activeTween?.Kill();
            _transitioning = true;

            // Buffer keeps old illustration at full opacity behind
            if (illustrationImage && _bufferImage)
            {
                _bufferImage.sprite = illustrationImage.sprite;
                _bufferImage.color = Color.white;
                _bufferImage.enabled = true;
                if (_bufferArf && _arf)
                    _bufferArf.aspectRatio = _arf.aspectRatio;
            }

            // Apply new page content immediately
            ApplyPage(page);

            // New illustration starts transparent, fades IN over the old one
            // Old stays at alpha=1 underneath → no darkening
            if (illustrationImage)
                illustrationImage.color = new Color(1, 1, 1, 0);

            _activeTween = illustrationImage
                ? illustrationImage.DOFade(1f, fadeDuration).OnComplete(FinishTransition)
                : DOVirtual.DelayedCall(fadeDuration, FinishTransition);
        }

        private void FinishTransition()
        {
            if (_bufferImage) _bufferImage.enabled = false;
            _transitioning = false;
            _activeTween = null;
        }

        private void EnsureBuffer()
        {
            if (_bufferImage || !illustrationImage) return;

            var go = new GameObject("IllustrationBuffer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(illustrationImage.transform.parent, false);

            var src = illustrationImage.rectTransform;
            var dst = go.GetComponent<RectTransform>();
            dst.anchorMin = src.anchorMin;
            dst.anchorMax = src.anchorMax;
            dst.anchoredPosition = src.anchoredPosition;
            dst.sizeDelta = src.sizeDelta;
            dst.pivot = src.pivot;

            // Place behind main illustration
            go.transform.SetSiblingIndex(illustrationImage.transform.GetSiblingIndex());

            _bufferImage = go.GetComponent<Image>();
            _bufferImage.enabled = false;

            if (_arf)
            {
                _bufferArf = go.AddComponent<AspectRatioFitter>();
                _bufferArf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }
        }

        private void ApplyPage(int page)
        {
            _currentPage = page;

            if (pageText)
            {
                pageText.text = ResolveGender(_pages[page]);
                pageText.alpha = 1f;
            }

            // Use cached sprite if available, otherwise load async
            var cached = IllustrationProvider.GetCached(_taleId, page, _gender, _genderedPages);
            if (cached != null)
                SetIllustration(cached);
            else
                StartCoroutine(LoadIllustrationAsync(page));

            OnPageChanged?.Invoke(page);
            PrefetchAdjacent(page);
        }

        private void PrefetchAdjacent(int page)
        {
            if (page > 0 && IllustrationProvider.GetCached(_taleId, page - 1, _gender, _genderedPages) == null)
                StartCoroutine(IllustrationProvider.GetPageAsync(_taleId, page - 1, _ => { },
                    _gender, _genderedPages));
            if (page < TotalPages - 1 && IllustrationProvider.GetCached(_taleId, page + 1, _gender, _genderedPages) == null)
                StartCoroutine(IllustrationProvider.GetPageAsync(_taleId, page + 1, _ => { },
                    _gender, _genderedPages));
        }

        private System.Collections.IEnumerator LoadIllustrationAsync(int page)
        {
            yield return IllustrationProvider.GetPageAsync(_taleId, page, s =>
            {
                if (s != null && _currentPage == page)
                    SetIllustration(s);
            }, _gender, _genderedPages);
        }

        private void SetIllustration(Sprite sprite)
        {
            if (!illustrationImage) return;
            illustrationImage.sprite = sprite;
            illustrationImage.color = Color.white;
        }

        /// <summary>
        /// Масштабирует шрифт и панель.
        /// TextContent и PageText растягиваются вместе с TextPanel.
        /// enableAutoSizing подбирает максимальный шрифт, помещающийся в доступное пространство.
        /// </summary>
        public void SetTextScale(float scale)
        {
            if (!pageText) return;

            _currentScale = scale;

            // Grow TextPanel upward — keep bottom edge fixed
            if (textPanel != null)
            {
                float h = Mathf.Max(_basePanelSizeDelta.y, _basePanelSizeDelta.y * scale);
                textPanel.sizeDelta = new Vector2(_basePanelSizeDelta.x, h);
                float dy = (h - _basePanelSizeDelta.y) * textPanel.pivot.y;
                textPanel.anchoredPosition = _basePanelPosition + new Vector2(0, dy);
            }

            // Auto-size font to fill available space
            pageText.enableAutoSizing = true;
            pageText.fontSizeMin = _baseFontSize * 0.3f;
            pageText.fontSizeMax = _baseFontSize * scale;
        }

        private string ResolveGender(string text)
        {
            bool isMale = _gender == "male";
            return GenderTag.Replace(text, m => isMale ? m.Groups[1].Value : m.Groups[2].Value);
        }
    }
}
