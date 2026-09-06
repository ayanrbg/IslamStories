using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Cache;
using FairyTales.Diagnostics;
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

        [Tooltip("How many pages are narrated in parallel (server used to batch by 5).")]
        [SerializeField] private int parallelRequests = 4;

        private ScreenManager _screens;
        private NarrationService _narration;
        private TaleSummary _tale;
        private TaleDetail _detail;
        private string[] _pages; // for bundled tales retry
        private string _voice;
        private string _narratorGender;
        private string _childName;
        private string _childGender;
        private string _lang;
        private Coroutine _polling;
        private readonly List<Coroutine> _workers = new();
        private int _running;
        private int _downloadedPages;
        private int _totalPages;
        private bool _generationDone;

        /// <summary>
        /// True while a book is being narrated. Read by NarrationConfig: a remote
        /// cache reset must not delete pages out from under a running job.
        /// </summary>
        public static bool IsGenerating { get; private set; }

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
            // Never stack generators: a settled transition can deliver OnShown again.
            if (_polling != null) StopCoroutine(_polling);
            StopWorkers();
            _polling = StartCoroutine(GenerateNarration());
        }

        protected override void OnHidden()
        {
            if (_polling != null)
            {
                StopCoroutine(_polling);
                _polling = null;
            }
            StopWorkers();
        }

        private void StopWorkers()
        {
            foreach (var w in _workers)
                if (w != null) StopCoroutine(w);
            _workers.Clear();
            _running = 0;
            IsGenerating = false;
        }

        /// <summary>
        /// Narrates the book page by page, sending the CLIENT's already personalized
        /// text for the CURRENT language. The old flow started a server-side
        /// narrate-all job and then pulled /narration/{page}, which carries no
        /// language — after switching languages the server replayed the previous
        /// language's audio (kz voice over ru text). Sending the text removes any
        /// server-side state: what you read is exactly what gets spoken.
        /// </summary>
        private IEnumerator GenerateNarration()
        {
            // The language of the pages we are about to send, which is not always
            // Loc.Lang — a tale without that translation shows another language.
            IsGenerating = true;
            var lang = _lang = TaleText.SpokenLang(_detail);
            var pages = _detail?.pages ?? _pages;

            if (_tale == null || pages == null || pages.Length == 0)
            {
                RemoteLog.Event("narration_generation_error",
                    $"tale={_tale?.id}; lang={lang}; detail={_detail != null}; " +
                    $"pages={pages?.Length ?? -1}");
                ShowError();
                yield break;
            }

            _totalPages = pages.Length;
            UpdateProgress();

            int batch = Mathf.Max(1, parallelRequests);
            for (int i = 0; i < _totalPages; i += batch)
            {
                int end = Mathf.Min(i + batch, _totalPages);
                for (int p = i; p < end; p++)
                {
                    _running++;
                    _workers.Add(StartCoroutine(NarrateOne(p, pages[p], lang)));
                }
                while (_running > 0) yield return null;
                _workers.Clear();
            }

            _generationDone = true;
            IsGenerating = false;
            UpdateProgress();

            // A short count still matters: those pages fall back to on-demand narration
            // during reading, with the delay that implies.
            RemoteLog.Event("narration_generation_done",
                $"tale={_tale.id}; lang={lang}; downloaded={_downloadedPages}; total={_totalPages}; " +
                $"narrator={UserProfile.NarratorGender}; tag={UserProfile.CacheTag}");

            if (_downloadedPages == 0)
            {
                ShowError();
                yield break;
            }

            // Meta records which params this audio belongs to, complete or not: a run
            // that lost two pages to the network used to be thrown away entirely and
            // paid for again from page one.
            AssetCache.SaveAiNarrationMeta(_tale.id, lang, UserProfile.CacheTag,
                _voice, complete: _downloadedPages >= _totalPages);

            if (statusText) statusText.text = Loc.Get("done");
            if (btnDone) btnDone.gameObject.SetActive(true);

            // Go straight to reading. Waiting for a tap on "Done" left the user on a
            // finished progress screen with nothing playing — which is exactly what
            // "narration does not work" looked like. The button stays as the manual
            // fallback for when the detail never loaded.
            OpenReading();
        }

        private IEnumerator NarrateOne(int page, string rawText, string lang)
        {
            var key = AssetCache.AiNarrationKey(_tale.id, lang, page, UserProfile.CacheTag);
            // HasAudio, not Exists: a zero-length file from a failed earlier run would
            // otherwise count as done and leave that page silent in the finished book.
            if (AssetCache.HasAudio(key))
            {
                _downloadedPages++;
                _running--;
                UpdateProgress();
                yield break;
            }

            yield return _narration.NarratePage(_tale.id, page,
                onSuccess: data =>
                {
                    if (AssetCache.SaveAudio(key, data)) _downloadedPages++;
                    else RemoteLog.Event("narration_page_bad_audio",
                        $"tale={_tale.id}; page={page}; bytes={data?.Length ?? 0}");
                },
                onError: e => RemoteLog.Event("narration_page_download_fail",
                    $"tale={_tale.id}; page={page}; lang={lang}; err={e}"),
                voice: _voice ?? "narrator", lang: lang,
                text: TaleText.Resolve(rawText), narratorGender: _narratorGender);

            _running--;
            UpdateProgress();
        }

        private void ShowError()
        {
            IsGenerating = false;
            // Only this narrator's folder — a failed female run must not delete the
            // male take the user already has.
            if (_tale != null)
                AssetCache.ClearAiNarration(_tale.id, _lang ?? Loc.Lang, UserProfile.CacheTag);
            if (statusText) statusText.text = Loc.Get("error");
            if (btnRetry) btnRetry.gameObject.SetActive(true);
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
            if (_tale == null) return;
            if (btnRetry) btnRetry.gameObject.SetActive(false);
            if (statusText) statusText.text = Loc.Get("loading");
            if (progressBar) progressBar.value = 0f;
            if (pagesText) pagesText.text = "";
            _downloadedPages = 0;
            _generationDone = false;

            StopWorkers();
            _polling = StartCoroutine(GenerateNarration());
        }

        private void OnDone()
        {
            if (OpenReading()) return;
            _screens.Show<TaleDetailScreen>();
        }

        /// <summary>
        /// Opens the reading screen with the freshly generated AI narration.
        /// Returns false when there is nothing to open, so the caller can fall back.
        /// </summary>
        private bool OpenReading()
        {
            var reading = _detail != null ? _screens.Get<ReadingScreen>() : null;
            if (reading == null)
            {
                RemoteLog.Event("narration_open_reading_failed",
                    $"tale={_tale?.id}; detail={_detail != null}; " +
                    $"pages={_detail?.pages?.Length ?? -1}");
                return false;
            }

            reading.SetTale(_detail, NarrationMode.AI);
            _screens.Show<ReadingScreen>();
            return true;
        }

        private void OnBack() => _screens.Show<TaleDetailScreen>();
    }
}
