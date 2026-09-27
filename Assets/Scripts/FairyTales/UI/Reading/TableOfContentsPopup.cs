using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Models;

namespace FairyTales.UI.Reading
{
    public class TableOfContentsPopup : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button btnClose;
        [SerializeField] private Transform thumbnailContainer;
        [SerializeField] private GameObject thumbnailPrefab;
        [SerializeField] private int maxChapters = 12;

        private static readonly Color SelectedBorder = new(0f, 0.8f, 0.9f);
        private static readonly Color NormalBorder = new(0.3f, 0.2f, 0.4f);

        private PageNavigator _navigator;
        private TocThumbnail[] _pool;
        private int[] _genderedPages;
        private string _gender;
        private Coroutine _loading;

        private void Awake()
        {
            if (btnClose) btnClose.onClick.AddListener(Hide);
        }

        private void EnsurePool()
        {
            if (_pool != null) return;
            _pool = new TocThumbnail[maxChapters];
            for (int i = 0; i < maxChapters; i++)
            {
                var go = Instantiate(thumbnailPrefab, thumbnailContainer);
                go.SetActive(false);
                _pool[i] = go.GetComponent<TocThumbnail>();
            }
        }

        public void Show(TaleDetail tale, PageNavigator navigator)
        {
            _navigator = navigator;
            _genderedPages = tale.genderedPages;
            _gender = PlayerPrefs.GetString("ft_gender", "male");
            if (titleText) titleText.text = tale.title;

            gameObject.SetActive(true);
            EnsurePool();
            Bind(tale, navigator.CurrentPage);

            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.2f);
        }

        public void Hide()
        {
            canvasGroup.DOFade(0f, 0.2f)
                .OnComplete(() => gameObject.SetActive(false));
        }

        /// <summary>
        /// Warm the thumbnail cache for the TOC chapter cards ahead of time
        /// (e.g. when the reading screen opens) so the popup shows images immediately
        /// instead of blank cards while sprites load/download.
        /// </summary>
        public IEnumerator Prewarm(TaleDetail tale)
        {
            if (tale == null) yield break;
            _genderedPages = tale.genderedPages;
            _gender = PlayerPrefs.GetString("ft_gender", "male");

            int chapters = ChapterLayout(tale.totalPages, out int pagesPerChapter);
            for (int i = 0; i < chapters; i++)
            {
                int startPage = i * pagesPerChapter;
                yield return LoadThumbnail(tale.id, startPage, null);
            }
        }

        /// <summary>
        /// Number of chapter cards and pages per chapter. Re-derives the real chapter
        /// count: ceil-rounding pagesPerChapter can leave trailing chapters whose
        /// startPage is past the last page (phantom "51-49" cards).
        /// </summary>
        private int ChapterLayout(int total, out int pagesPerChapter)
        {
            int chapters = Mathf.Min(maxChapters, total);
            pagesPerChapter = Mathf.CeilToInt((float)total / chapters);
            return Mathf.CeilToInt((float)total / pagesPerChapter);
        }

        private void Bind(TaleDetail tale, int currentPage)
        {
            int total = tale.totalPages;
            int chapters = ChapterLayout(total, out int pagesPerChapter);
            var pages = new List<int>();
            var covers = new List<Image>();

            for (int i = 0; i < _pool.Length; i++)
            {
                var thumb = _pool[i];
                if (i >= chapters)
                {
                    thumb.gameObject.SetActive(false);
                    continue;
                }

                int startPage = i * pagesPerChapter;
                int endPage = Mathf.Min(startPage + pagesPerChapter - 1, total - 1);
                bool selected = currentPage >= startPage && currentPage <= endPage;

                if (thumb.Cover)
                {
                    thumb.Cover.sprite = null;
                    pages.Add(startPage);
                    covers.Add(thumb.Cover);
                }
                if (thumb.Label) thumb.Label.text = $"{startPage + 1}-{endPage + 1}";
                if (thumb.Border) thumb.Border.color =
                    selected ? SelectedBorder : NormalBorder;

                thumb.Button?.onClick.RemoveAllListeners();
                if (thumb.Button)
                {
                    int p = startPage;
                    thumb.Button.onClick.AddListener(() =>
                    {
                        _navigator.GoToPage(p);
                        Hide();
                    });
                }

                thumb.gameObject.SetActive(true);
            }

            if (_loading != null) StopCoroutine(_loading);
            _loading = StartCoroutine(LoadThumbnails(tale.id, pages, covers));
        }

        /// <summary>
        /// One card after another rather than all at once: each missing card means
        /// decoding a full page before it is shrunk, and a dozen of those in parallel
        /// is exactly the memory spike and stall the small copies are meant to avoid.
        /// </summary>
        private IEnumerator LoadThumbnails(string taleId, List<int> pages, List<Image> covers)
        {
            for (int i = 0; i < pages.Count; i++)
                yield return LoadThumbnail(taleId, pages[i], covers[i]);
            _loading = null;
        }

        private IEnumerator LoadThumbnail(string taleId, int page, Image target)
        {
            yield return IllustrationProvider.GetThumbnailAsync(taleId, page, sprite =>
            {
                if (target) target.sprite = sprite;
            }, _gender, _genderedPages);
        }
    }
}
