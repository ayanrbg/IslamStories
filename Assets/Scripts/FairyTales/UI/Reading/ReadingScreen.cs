using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Audio;
using FairyTales.Cache;
using FairyTales.Diagnostics;
using FairyTales.IAP;
using FairyTales.Models;
using FairyTales.UI.Core;

namespace FairyTales.UI.Reading
{
    public enum NarrationMode { None, Default, AI, Parent }

    public class ReadingScreen : BaseScreen
    {
        [SerializeField] private PageNavigator pageNavigator;
        [SerializeField] private Button btnHome;
        [SerializeField] private Button btnMusic;
        [SerializeField] private Button btnPrev;
        [SerializeField] private Button btnNext;
        [SerializeField] private RecordingOverlay recordingOverlay;
        [SerializeField] private TMP_Text pageCounter;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;

        [Header("Settings Panel")]
        [SerializeField] private Button btnSettings;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsOverlay;
        [SerializeField] private Button btnSettingsClose;
        [SerializeField] private Button btnSettingsToc;
        [SerializeField] private Button btnSettingsTextSize;

        [Header("Text Size Panel")]
        [SerializeField] private GameObject textSizePanel;
        [SerializeField] private Button btnTextSizeClose;
        [SerializeField] private Slider textSizeSlider;
        [SerializeField] private float minTextScale = 0.7f;
        [SerializeField] private float maxTextScale = 1.6f;

        private ScreenManager _screens;
        private BackgroundMusicManager _backgroundMusicManager;
        private NarrationPlayer _narrationPlayer;
        private NarrationService _narrationService;
        private DefaultNarrationProvider _defaultNarration;
        private TaleDetail _tale;
        private TaleSummary _taleSummary;
        private NarrationMode _mode = NarrationMode.Default;
        private bool _recordingMode;
        private Models.Draft _recordingDraft;
        private int _startPage;
        private bool _reachedLastPage;
        private bool _completed;
        private float _openTime;
        private int _pendingPageView;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            _narrationPlayer = FindAnyObjectByType<NarrationPlayer>();
            _backgroundMusicManager = FindAnyObjectByType<BackgroundMusicManager>();
            _defaultNarration = new DefaultNarrationProvider();
            var api = FindAnyObjectByType<ApiClient>();
            if (api) _narrationService = new NarrationService(api);

            if (btnHome) btnHome.onClick.AddListener(OnHome);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);
            if (btnPrev) btnPrev.onClick.AddListener(OnPrev);
            if (btnNext) btnNext.onClick.AddListener(OnNext);
            UpdateMusicButtons();

            if (btnSettings) btnSettings.onClick.AddListener(ToggleSettings);
            if (settingsOverlay) settingsOverlay.onClick.AddListener(CloseAllPanels);
            if (btnSettingsClose) btnSettingsClose.onClick.AddListener(CloseAllPanels);
            if (btnSettingsToc) btnSettingsToc.onClick.AddListener(OnSettingsToc);
            if (btnSettingsTextSize) btnSettingsTextSize.onClick.AddListener(OpenTextSizePanel);
            if (btnTextSizeClose) btnTextSizeClose.onClick.AddListener(CloseTextSizePanel);
            if (textSizeSlider)
            {
                textSizeSlider.minValue = minTextScale;
                textSizeSlider.maxValue = maxTextScale;
                textSizeSlider.value = PlayerPrefs.GetFloat("ft_text_scale", 1f);
                textSizeSlider.onValueChanged.AddListener(OnTextScaleChanged);
            }
            CloseAllPanels();
        }

        private Sprite _initialIllustration;

        public void SetInitialIllustration(Sprite sprite)
        {
            _initialIllustration = sprite;
        }

        public void SetTale(TaleDetail tale, NarrationMode mode = NarrationMode.Default)
        {
            _tale = tale;
            _mode = mode;
            _recordingMode = false;
            _taleSummary = null;
            _initialIllustration = null;
            _reachedLastPage = false;
        }

        public void SetRecordingMode(TaleDetail detail, TaleSummary summary,
            Models.Draft draft = null)
        {
            _tale = detail;
            _taleSummary = summary;
            _mode = NarrationMode.None;
            _recordingMode = true;
            _recordingDraft = draft;
        }

        protected override void OnPrepare()
        {
            SetRecordingUI(_recordingMode);
            UpdateMusicButtons();

            // Init page navigator early so the (likely cached) illustration
            // is already visible during the slide-in transition.
            if (_tale != null && _tale.pages != null)
            {
                _startPage = _recordingMode ? 0 : ReadingState.LoadPage(_tale.id);
                pageNavigator.Init(_tale.id, _tale.pages, _startPage, _initialIllustration,
                    _tale.genderedPages);
                _initialIllustration = null;
                UpdateNavButtons();
                UpdatePageCounter();
                ApplyTextScale(PlayerPrefs.GetFloat("ft_text_scale", 1f));
            }
        }

        protected override void OnShown()
        {
            if (_tale == null || _tale.pages == null)
            {
                // Silent dead end: the screen opens but nothing renders and nothing plays.
                RemoteLog.Event("reading_open_empty",
                    $"tale={_tale?.id}; pages={_tale?.pages?.Length ?? -1}; mode={_mode}");
                return;
            }

            if (_recordingMode && recordingOverlay)
                recordingOverlay.Activate(_tale.id, Loc.Lang,
                    pageNavigator, _narrationPlayer);
            else if (recordingOverlay)
                recordingOverlay.Deactivate();

            pageNavigator.OnPageChanged += OnPageChanged;
            if (!_recordingMode)
            {
                _completed = false;
                _openTime = Time.realtimeSinceStartup;
                if (_narrationPlayer) _narrationPlayer.OnFinished += OnNarrationFinished;

                Analytics.Log("tale_open", ("tale_id", _tale.id), ("is_free", _tale.free),
                    ("language", Loc.Lang), ("child_gender", Analytics.Gender()));
                LogPageView(pageNavigator.CurrentPage);

                if (pageNavigator.CurrentPage >= pageNavigator.TotalPages - 1)
                    _reachedLastPage = true;
                CheckComplete(pageNavigator.CurrentPage);

                // Once per opened tale, not per page: tells us whether narration was even
                // supposed to sound (mode None is silent by design) and if a player exists.
                RemoteLog.Event("narration_session",
                    $"tale={_tale.id}; mode={_mode}; lang={Loc.Lang}; " +
                    $"player={_narrationPlayer != null}; service={_narrationService != null}; " +
                    $"startPage={_startPage}");

                PlayNarration(_startPage);
            }

            // Warm the TOC chapter thumbnails so opening the contents popup shows
            // images right away instead of blank cards while sprites load.
            var toc = GetComponentInChildren<TableOfContentsPopup>(true);
            if (toc) StartCoroutine(toc.Prewarm(_tale));
        }

        protected override void OnHidden()
        {
            if (pageNavigator != null)
                pageNavigator.OnPageChanged -= OnPageChanged;

            if (!_recordingMode && _tale != null)
            {
                CancelInvoke(nameof(FlushPageView));
                if (_narrationPlayer) _narrationPlayer.OnFinished -= OnNarrationFinished;
                if (!_completed)
                {
                    int page = pageNavigator.CurrentPage;
                    Analytics.Log("tale_abandon", ("tale_id", _tale.id),
                        ("page_index", page), ("percent", Percent(page)),
                        ("duration_ms", ElapsedMs()));
                }
            }

            if (_narrationPlayer) _narrationPlayer.Stop();
            if (recordingOverlay && recordingOverlay.IsActive)
                recordingOverlay.Deactivate();
            SetRecordingUI(false);
            CloseAllPanels();
            if (_tale != null && !_recordingMode)
                ReadingState.SavePage(_tale.id, pageNavigator.CurrentPage);
        }

        private void UpdatePageCounter()
        {
            if (pageCounter)
                pageCounter.text = $"{pageNavigator.CurrentPage + 1}/{pageNavigator.TotalPages}";
        }

        private void OnPageChanged(int page)
        {
            UpdateNavButtons();
            UpdatePageCounter();

            if (_recordingMode)
            {
                if (recordingOverlay) recordingOverlay.OnPageVisited(page);
            }
            else
            {
                if (page >= pageNavigator.TotalPages - 1) _reachedLastPage = true;
                LogPageView(page);
                CheckComplete(page);
                PlayNarration(page);
                if (_tale != null) ReadingState.SavePage(_tale.id, page);
            }
        }

        // ── Analytics helpers ───────────────────────────────
        private int Percent(int page)
        {
            int total = pageNavigator.TotalPages;
            return total <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt((page + 1) * 100f / total), 0, 100);
        }

        private int ElapsedMs() => Mathf.RoundToInt((Time.realtimeSinceStartup - _openTime) * 1000f);

        // Debounce rapid page flips (~1s) so quick swiping doesn't flood tale_page_view.
        private void LogPageView(int page)
        {
            _pendingPageView = page;
            CancelInvoke(nameof(FlushPageView));
            Invoke(nameof(FlushPageView), 1f);
        }

        private void FlushPageView()
        {
            if (_tale == null) return;
            Analytics.Log("tale_page_view", ("tale_id", _tale.id),
                ("page_index", _pendingPageView), ("percent", Percent(_pendingPageView)),
                ("total_pages", pageNavigator.TotalPages));
        }

        private void CheckComplete(int page)
        {
            if (_completed || _tale == null) return;
            if (page < pageNavigator.TotalPages - 1) return;
            _completed = true;
            Analytics.Log("tale_complete", ("tale_id", _tale.id),
                ("total_pages", pageNavigator.TotalPages), ("duration_ms", ElapsedMs()));
        }

        private void OnNarrationFinished()
        {
            if (_recordingMode || _tale == null) return;
            Analytics.Log("narration_complete", ("tale_id", _tale.id));
        }

        private void PlayNarration(int page)
        {
            if (_narrationPlayer == null) return;
            if (_tale == null) return;
            _narrationPlayer.Stop();

            if (_mode == NarrationMode.None) return;

            Analytics.Log("narration_play", ("tale_id", _tale.id),
                ("voice_type", VoiceTypeTag(_mode)),
                ("page_index", page));

            if (_mode == NarrationMode.AI)
                StartCoroutine(PlayAiNarration(page));
            else if (_mode == NarrationMode.Default)
                StartCoroutine(PlayDefaultNarration(page));
            else if (_mode == NarrationMode.Parent)
                PlayParentNarration(page);
        }

        private static string VoiceTypeTag(NarrationMode mode) => mode switch
        {
            NarrationMode.AI => "cloned",
            NarrationMode.Parent => "parent",
            _ => "default"
        };

        // Play the parent's real recorded clip for this page. Pages the parent
        // didn't record simply stay silent.
        private void PlayParentNarration(int page)
        {
            var wav = AssetCache.LoadParentNarration(_tale.id, Loc.Lang, page);
            if (wav != null) _narrationPlayer.PlayFromBytes(wav);
            else RemoteLog.Event("narration_parent_missing",
                $"tale={_tale.id}; page={page}; lang={Loc.Lang}");
        }

        private IEnumerator PlayDefaultNarration(int page)
        {
            var lang = Loc.Lang;

            var bytes = _defaultNarration.GetPageBytes(_tale.id, page);
            if (bytes != null)
            {
                _narrationPlayer.PlayFromBytes(bytes);
                yield break;
            }

            // Async Resources load — avoids sync Resources.Load stall
            AudioClip clip = null;
            yield return _defaultNarration.GetPageAsync(_tale.id, page, c => clip = c);
            if (clip != null)
            {
                _narrationPlayer.PlayClip(clip);
                yield break;
            }

            // No local audio — generate TTS on server (narrator voice)
            if (_narrationService == null) yield break;

            // Pass page text for bundled tales (server may not have them)
            string pageText = _tale.pages != null && page < _tale.pages.Length
                ? _tale.pages[page] : null;

            RemoteLog.Event("narration_tts_request",
                $"tale={_tale.id}; page={page}; lang={lang}; textLen={pageText?.Length ?? -1}");

            yield return _narrationService.NarratePage(_tale.id, page,
                onSuccess: data =>
                {
                    AssetCache.Save(AssetCache.NarrationKey(_tale.id, lang, page), data);
                    _narrationPlayer.PlayFromBytes(data);
                },
                onError: e => RemoteLog.Event("narration_tts_fail",
                    $"tale={_tale.id}; page={page}; lang={lang}; err={e}"),
                voice: "narrator", lang: lang, text: pageText);
        }

        private IEnumerator PlayAiNarration(int page)
        {
            var lang = Loc.Lang;

            // Check local AI narration cache first
            var cacheKey = AssetCache.AiNarrationKey(_tale.id, lang, page);
            var cached = AssetCache.Load(cacheKey);
            if (cached != null)
            {
                RemoteLog.Event("narration_ai_cache_hit",
                    $"tale={_tale.id}; page={page}; lang={lang}; bytes={cached.Length}");
                _narrationPlayer.PlayFromBytes(cached);
                yield break;
            }

            // Fallback: download from server and cache
            if (_narrationService == null)
            {
                RemoteLog.Event("narration_ai_no_service", $"tale={_tale.id}; page={page}");
                StartCoroutine(PlayDefaultNarration(page));
                yield break;
            }

            RemoteLog.Event("narration_ai_download",
                $"tale={_tale.id}; page={page}; lang={lang}");

            yield return _narrationService.DownloadNarratedPage(_tale.id, page,
                onSuccess: bytes =>
                {
                    AssetCache.Save(cacheKey, bytes);
                    _narrationPlayer.PlayFromBytes(bytes);
                },
                onError: e =>
                {
                    RemoteLog.Event("narration_ai_download_fail",
                        $"tale={_tale.id}; page={page}; lang={lang}; err={e}");
                    StartCoroutine(PlayDefaultNarration(page));
                });
        }

        private void UpdateNavButtons()
        {
            if (btnPrev) btnPrev.interactable = pageNavigator.CurrentPage > 0;
            if (btnNext) btnNext.interactable =
                pageNavigator.CurrentPage < pageNavigator.TotalPages - 1;
        }

        private void SetRecordingUI(bool recording)
        {
            if (btnMusic) btnMusic.gameObject.SetActive(!recording);
            if (btnSettings) btnSettings.gameObject.SetActive(!recording);
        }

        private void OnPrev() => pageNavigator.PrevPage();
        private void OnNext() => pageNavigator.NextPage();

        private void OnHome()
        {
            if (_narrationPlayer) _narrationPlayer.Stop();

            // Recording session: finalize the current page, then return to the tale
            // detail so the parent can immediately listen via "Parent voice".
            if (_recordingMode)
            {
                if (recordingOverlay && recordingOverlay.IsActive)
                    recordingOverlay.Deactivate();
                if (_tale != null && AssetCache.HasAnyParentNarration(_tale.id, Loc.Lang))
                    Toast.Show(Loc.Get("voice_saved"));
                _screens.Show<Library.TaleDetailScreen>();
                return;
            }

            if (TryShowFirstReadPaywall()) return;
            _screens.Show<Library.LibraryScreen>();
        }

        private bool IsPremium =>
            IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;

        /// <summary>
        /// After the user finishes their first tale, surface the subscription
        /// offer exactly once. Skipped for premium users, recording sessions,
        /// and tales the user left before reaching the end. Returns true when
        /// the paywall took over navigation.
        /// </summary>
        private bool TryShowFirstReadPaywall()
        {
            if (_recordingMode || !_reachedLastPage) return false;
            if (IsPremium || !ReadingState.FirstPaywallPending) return false;

            var payment = _screens.Get<Payment.PaymentScreen>();
            if (payment == null) return false;

            ReadingState.MarkFirstPaywallShown();
            // Gate first; on cancel fall through to the library like a normal back.
            Payment.PaymentScreen.Open(_screens, typeof(Library.LibraryScreen),
                () => _screens.Show<Library.LibraryScreen>(), source: "first_read");
            return true;
        }

        private void OnToc()
        {
            var popup = GetComponentInChildren<TableOfContentsPopup>(true);
            if (popup) popup.Show(_tale, pageNavigator);
        }

        private void OnMusicToggle()
        {
            if (_backgroundMusicManager)
                _backgroundMusicManager.SetMuted(!_backgroundMusicManager.IsMuted);
            UpdateMusicButtons();
        }

        private void UpdateMusicButtons()
        {
            if (_backgroundMusicManager == null) return;
            bool muted = _backgroundMusicManager.IsMuted;
            if (selectionBtnMusicOff) selectionBtnMusicOff.SetActive(muted);
            if (selectionBtnMusicOn) selectionBtnMusicOn.SetActive(!muted);
        }

        private void ToggleSettings()
        {
            if (settingsPanel && settingsPanel.activeSelf)
                CloseAllPanels();
            else
                OpenSettingsPanel();
        }

        private void OpenSettingsPanel()
        {
            if (textSizePanel) textSizePanel.SetActive(false);
            if (settingsOverlay) settingsOverlay.gameObject.SetActive(true);
            if (settingsPanel) settingsPanel.SetActive(true);
        }

        private void OpenTextSizePanel()
        {
            if (settingsPanel) settingsPanel.SetActive(false);
            if (textSizePanel) textSizePanel.SetActive(true);
        }

        private void CloseTextSizePanel()
        {
            if (textSizePanel) textSizePanel.SetActive(false);
            OpenSettingsPanel();
        }

        private void CloseAllPanels()
        {
            if (settingsPanel) settingsPanel.SetActive(false);
            if (textSizePanel) textSizePanel.SetActive(false);
            if (settingsOverlay) settingsOverlay.gameObject.SetActive(false);
        }

        private void OnSettingsToc()
        {
            CloseAllPanels();
            OnToc();
        }

        private void OnTextScaleChanged(float value)
        {
            ApplyTextScale(value);
            PlayerPrefs.SetFloat("ft_text_scale", value);
        }

        private void ApplyTextScale(float scale)
        {
            pageNavigator.SetTextScale(scale);
            if (textSizeSlider && !Mathf.Approximately(textSizeSlider.value, scale))
                textSizeSlider.value = scale;
        }
    }
}
