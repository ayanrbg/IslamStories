using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Cache;
using FairyTales.Models;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using FairyTales.UI.Reading;

namespace FairyTales.UI.Narration
{
    public class NarrationProgressScreen : BaseScreen
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text pagesText;
        [SerializeField] private Button btnDone;
        [SerializeField] private Button btnBack;
        [SerializeField] private Button btnRetry;

        [SerializeField] private float pollInterval = 3f;

        private ScreenManager _screens;
        private NarrationService _narration;
        private TaleSummary _tale;
        private TaleDetail _detail;
        private string[] _pages; // for bundled tales retry
        private string _voice;
        private string _narratorGender;
        private string _childName;
        private string _childGender;
        private Coroutine _polling;
        private Coroutine _downloading;
        private int _downloadedPages;
        private int _totalPages;
        private bool _generationDone;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            var api = FindAnyObjectByType<ApiClient>();
            _narration = new NarrationService(api);

            if (btnDone) btnDone.onClick.AddListener(OnDone);
            if (btnBack) btnBack.onClick.AddListener(OnBack);
            if (btnRetry) btnRetry.onClick.AddListener(OnRetry);
        }

        public void SetContext(TaleSummary tale, TaleDetail detail = null,
            string[] pages = null, string voice = null, string narratorGender = null,
            string childName = null, string childGender = null)
        {
            _tale = tale;
            _detail = detail;
            _pages = pages;
            _voice = voice;
            _narratorGender = narratorGender;
            _childName = childName;
            _childGender = childGender;
        }

        protected override void OnPrepare()
        {
            if (btnDone) btnDone.gameObject.SetActive(false);
            if (btnRetry) btnRetry.gameObject.SetActive(false);
            if (progressBar) progressBar.value = 0f;
            if (statusText) statusText.text = Loc.Get("loading");
            if (pagesText) pagesText.text = "";
            _downloadedPages = 0;
            _totalPages = 0;
            _generationDone = false;
        }

        protected override void OnShown()
        {
            _polling = StartCoroutine(PollAndDownload());
        }

        protected override void OnHidden()
        {
            if (_polling != null)
            {
                StopCoroutine(_polling);
                _polling = null;
            }
            if (_downloading != null)
            {
                StopCoroutine(_downloading);
                _downloading = null;
            }
        }

        private IEnumerator PollAndDownload()
        {
            int pagesReady = 0;

            while (true)
            {
                bool done = false;
                bool error = false;

                yield return _narration.GetNarrationStatus(_tale.id,
                    onSuccess: s =>
                    {
                        _totalPages = s.totalPages;
                        pagesReady = s.pagesReady;

                        if (s.status == "done")
                            done = true;
                        else if (s.status == "error")
                            error = true;
                    },
                    onError: e =>
                    {
                        Debug.LogWarning($"[NarrationProgress] Poll error: {e}");
                        error = true;
                    });

                if (error)
                {
                    // Clear partially downloaded files so stale audio isn't reused
                    if (_tale != null) AssetCache.ClearAiNarration(_tale.id);

                    if (statusText) statusText.text = Loc.Get("error");
                    if (btnRetry) btnRetry.gameObject.SetActive(true);
                    yield break;
                }

                // Download pages that are ready but not yet cached
                yield return DownloadReadyPages(pagesReady);

                UpdateProgress();

                if (done)
                {
                    _generationDone = true;
                    // Download any remaining pages
                    yield return DownloadReadyPages(_totalPages);
                    UpdateProgress();

                    // Save meta only after all pages downloaded successfully
                    if (_downloadedPages >= _totalPages && _tale != null)
                        AssetCache.SaveAiNarrationMeta(_tale.id, _voice,
                            _narratorGender, _childName, _childGender);

                    if (statusText) statusText.text = Loc.Get("done");
                    if (btnDone) btnDone.gameObject.SetActive(true);
                    yield break;
                }

                yield return new WaitForSeconds(pollInterval);
            }
        }

        private IEnumerator DownloadReadyPages(int readyCount)
        {
            var lang = Loc.Lang;

            for (int p = _downloadedPages; p < readyCount; p++)
            {
                var key = AssetCache.AiNarrationKey(_tale.id, lang, p);
                if (AssetCache.Exists(key))
                {
                    _downloadedPages = p + 1;
                    continue;
                }

                bool pageDone = false;
                yield return _narration.DownloadNarratedPage(_tale.id, p,
                    onSuccess: data =>
                    {
                        AssetCache.Save(key, data);
                        pageDone = true;
                    },
                    onError: e =>
                    {
                        Debug.LogWarning($"[NarrationProgress] Download page {p} failed: {e}");
                        pageDone = true; // skip, will retry next poll
                    });

                if (pageDone)
                    _downloadedPages = p + 1;

                UpdateProgress();
            }
        }

        private void UpdateProgress()
        {
            float progress = _totalPages > 0
                ? (float)_downloadedPages / _totalPages
                : 0f;

            if (progressBar) progressBar.value = progress;
            if (pagesText) pagesText.text = $"{_downloadedPages} / {_totalPages}";
            if (statusText && !_generationDone)
                statusText.text = Loc.Get("download.narration");
        }

        private void OnRetry()
        {
            if (btnRetry) btnRetry.gameObject.SetActive(false);
            StartCoroutine(RetryNarration());
        }

        private IEnumerator RetryNarration()
        {
            if (_tale == null) yield break;

            var childName = PlayerPrefs.GetString("ft_childName", "");
            var gender = PlayerPrefs.GetString("ft_gender", "male");
            bool hasClone = PlayerPrefs.GetInt("ft_voiceCloned", 0) == 1;
            string voice = hasClone ? null : "narrator";

            if (statusText) statusText.text = Loc.Get("loading");
            if (progressBar) progressBar.value = 0f;
            if (pagesText) pagesText.text = "";
            _downloadedPages = 0;
            _generationDone = false;

            bool started = false;
            yield return _narration.NarrateAll(_tale.id, childName, gender,
                onSuccess: _ => started = true,
                onError: e =>
                {
                    Debug.LogWarning($"[NarrationProgress] Retry failed: {e}");
                    if (statusText) statusText.text = Loc.Get("error");
                    if (btnRetry) btnRetry.gameObject.SetActive(true);
                },
                voice: voice, lang: Loc.Lang, pages: _pages);

            if (started)
                _polling = StartCoroutine(PollAndDownload());
        }

        private void OnDone()
        {
            if (_detail != null)
            {
                var reading = _screens.Get<ReadingScreen>();
                if (reading != null)
                {
                    reading.SetTale(_detail, NarrationMode.AI);
                    _screens.Show<ReadingScreen>();
                    return;
                }
            }
            _screens.Show<TaleDetailScreen>();
        }

        private void OnBack() => _screens.Show<TaleDetailScreen>();
    }
}
