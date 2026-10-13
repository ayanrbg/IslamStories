using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Cache;
using FairyTales.Diagnostics;
using FairyTales.Models;
using Newtonsoft.Json;
using FairyTales.Audio;
using FairyTales.UI.Core;
using FairyTales.UI.Narration;
using FairyTales.UI.Onboarding;
using FairyTales.UI.Reading;

namespace FairyTales.UI.Library
{
    public class TaleDetailScreen : BaseScreen
    {
        [SerializeField] private Image illustrationImage;
        [SerializeField] private CanvasGroup illustrationGroup;
        [SerializeField] private Color dimColor = new(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text pageCountText;
        [SerializeField] private Button btnRead;
        [SerializeField] private Button btnListen;
        [SerializeField] private Button btnBack;
        [SerializeField] private Button btnMusic;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;

        [Header("Voice Selection (child panel)")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject voicePanel;
        [SerializeField] private TMP_Text voiceTitleText;
        [SerializeField] private Button btnVoiceMale;
        [SerializeField] private Button btnVoiceFemale;
        [SerializeField] private Button btnVoiceParent;
        [Tooltip("One Back for the voice and parent-voice panels. Lives in the top bar, so it " +
                 "stays put while the panels slide; hidden on the main panel, which has Home.")]
        [SerializeField] private Button btnPanelBack;
        [SerializeField] private TMP_Text voiceMaleText;
        [SerializeField] private TMP_Text voiceFemaleText;
        [SerializeField] private TMP_Text voiceParentText;

        [Header("Parent voices (child panel)")]
        [SerializeField] private GameObject parentPanel;
        [SerializeField] private TMP_Text parentTitleText;
        [SerializeField] private TMP_Text newRecordingTitleText;
        [SerializeField] private TMP_InputField speakerInput;
        [SerializeField] private TMP_Text speakerPlaceholderText;
        [SerializeField] private Button btnStartRecording;
        [SerializeField] private TMP_Text startRecordingText;
        [SerializeField] private TMP_Text voicesTitleText;
        [SerializeField] private Transform voicesContent;
        [SerializeField] private ParentVoiceItem voiceItemTemplate;
        [SerializeField] private TMP_Text noVoicesText;

        private ScreenManager _screens;
        private TalesService _tales;
        private BackgroundMusicManager _musicManager;
        private TaleSummary _tale;
        private TaleDetail _detail;
        private bool _returnToParentVoices;
        private GameObject _currentPanel;
        private CanvasGroup _panelBackGroup;
        private Sequence _panelSeq;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            var api = FindAnyObjectByType<ApiClient>();
            _tales = new TalesService(api);
            _musicManager = FindAnyObjectByType<BackgroundMusicManager>();

            if (btnRead) btnRead.onClick.AddListener(OnRead);
            if (btnListen) btnListen.onClick.AddListener(OnListen);
            if (btnBack) btnBack.onClick.AddListener(OnBack);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);

            if (btnVoiceMale) btnVoiceMale.onClick.AddListener(() => OnVoiceSelected("narrator", "male"));
            if (btnVoiceFemale) btnVoiceFemale.onClick.AddListener(() => OnVoiceSelected("narrator", "female"));
            if (btnVoiceParent) btnVoiceParent.onClick.AddListener(OnParentVoice);
            if (btnPanelBack)
            {
                btnPanelBack.onClick.AddListener(OnPanelBack);
                _panelBackGroup = btnPanelBack.GetComponent<CanvasGroup>();
                if (!_panelBackGroup) _panelBackGroup = btnPanelBack.gameObject.AddComponent<CanvasGroup>();
            }
            if (btnStartRecording) btnStartRecording.onClick.AddListener(OnStartRecording);
            if (voiceItemTemplate) voiceItemTemplate.gameObject.SetActive(false);
        }

        public void SetTale(TaleSummary tale)
        {
            _tale = tale;
            _detail = null;

            // Start loading immediately so content is ready before transition animation
            if (titleText) titleText.text = _tale?.title ?? "";
            if (pageCountText) pageCountText.text = "";
            LoadIllustration();
            StartCoroutine(LoadDetailAndPersonalize());
        }

        private void LoadIllustration()
        {
            if (!illustrationImage || _tale == null) return;

            int page = ReadingState.LoadPage(_tale.id);
            var gender = PlayerPrefs.GetString("ft_gender", "male");
            var gp = _detail?.genderedPages;
            illustrationImage.color = dimColor;
            StartCoroutine(IllustrationProvider.GetPageAsync(_tale.id, page, s =>
            {
                if (s != null && illustrationImage) illustrationImage.sprite = s;
            }, gender, gp));
        }

        protected override void OnPrepare()
        {
            if (illustrationGroup) illustrationGroup.alpha = 1f;
            ApplyTexts();
            // Coming back from the recorder lands on the voices list, with the new voice in it.
            if (_returnToParentVoices) ShowParentPanel(animate: false);
            else ShowPanel(mainPanel, animate: false);
            _returnToParentVoices = false;
            UpdateMusicButtons();
        }

        protected override void OnShown()
        {
        }

        private void UpdateUI()
        {
            if (titleText) titleText.text = _tale?.title ?? "";

            // Reload illustration with gendered pages info from detail
            LoadIllustration();

            if (pageCountText && _detail != null)
                pageCountText.text = $"{_detail.totalPages} {Loc.Get("pages")}";
        }

        private IEnumerator LoadDetailAndPersonalize()
        {
            if (_tale == null) yield break;

            // True when text comes from a trusted same-language local source
            // (bundled JSON or downloaded cache) — raw templates we personalize on-client.
            bool localText = false;

            // 1. Bundled tales: local JSON for THIS language only. A bundle that ships
            // fewer translations than the app has languages (komar has ru + kz, no en)
            // must fall through to the steps below instead of serving Russian pages to
            // an English reader — step 4 is where settling for that becomes acceptable.
            bool bundled = BundledTaleLoader.IsBundled(_tale.id);
            if (bundled)
            {
                yield return BundledTaleLoader.LoadTaleJson(_tale.id, Loc.Lang, d =>
                {
                    _detail = d;
                    UpdateUI();
                });
                if (_detail != null) localText = true;
            }

            // 2. Downloaded tale: cached text for the current language (offline + correct)
            if (_detail == null)
            {
                // Read and parsed on a worker thread: this used to run inside the tap on
                // the card, before the screen had even started to move.
                var detailOf = _tale.id;
                var path = AssetCache.GetPath(AssetCache.TaleTextKey(_tale.id, Loc.Lang));
                TaleDetail cached = null;
                yield return JsonAsync.Run(() => System.IO.File.Exists(path)
                    ? JsonConvert.DeserializeObject<TaleDetail>(System.IO.File.ReadAllText(path))
                    : null, d => cached = d);
                // Another card may have been opened while the file was being read.
                if (_tale == null || _tale.id != detailOf) yield break;
                if (cached != null)
                {
                    _detail = cached;

                    // A cached translation with no usable text would stick forever
                    // (the cache is never refreshed) — drop it and ask the server.
                    if (_detail != null && (_detail.pages == null || _detail.pages.Length == 0))
                        _detail = null;

                    if (_detail != null)
                    {
                        if (_detail.totalPages == 0 && _detail.pages != null)
                            _detail.totalPages = _detail.pages.Length;
                        localText = true;
                        UpdateUI();
                    }
                }
            }

            // 3. Server (primary for non-downloaded tales)
            if (_detail == null)
            {
                yield return _tales.GetTale(_tale.id,
                    onSuccess: d =>
                    {
                        _detail = d;
                        // Cache the raw text if the server served the requested language,
                        // so it's available offline / for fast language switching next time.
                        if (d?.pages != null && d.lang == Loc.Lang)
                            AssetCache.SaveTaleText(_tale.id, Loc.Lang,
                                System.Text.Encoding.UTF8.GetBytes(
                                    JsonConvert.SerializeObject(d)));
                        UpdateUI();
                    },
                    onError: e => { } /* RELEASE: Debug.LogWarning($"[TaleDetail] Server: {e}") */,
                    lang: Loc.Lang);
            }

            // 3b. Offline, and the cached text was set aside because the server changed
            // it: the old version beats an empty book.
            if (_detail == null)
            {
                var stale = AssetCache.LoadStaleTaleText(_tale.id, Loc.Lang);
                if (stale != null)
                {
                    try { _detail = JsonConvert.DeserializeObject<TaleDetail>(stale); }
                    catch (JsonException) { }
                    if (_detail?.pages == null || _detail.pages.Length == 0) _detail = null;
                    if (_detail != null)
                    {
                        if (_detail.totalPages == 0) _detail.totalPages = _detail.pages.Length;
                        localText = true;
                        UpdateUI();
                    }
                }
            }

            // 4. Bundled tale, untranslated and offline: the default-language pages are
            // all there is. Reached only once steps 1-3 came up empty, so a reachable
            // translation always wins over this.
            if (_detail == null && bundled && !BundledTaleLoader.IsFallbackLang(Loc.Lang))
            {
                yield return BundledTaleLoader.LoadFallbackTaleJson(_tale.id, d =>
                {
                    _detail = d;
                    UpdateUI();
                });
                if (_detail != null) localText = true;
            }

            // Personalize pages (replace {childName}; gender {m:|f:} is resolved at render time)
            if (_detail?.pages != null)
            {
                var childName = PlayerPrefs.GetString("ft_childName", "");
                if (localText)
                {
                    for (int i = 0; i < _detail.pages.Length; i++)
                        _detail.pages[i] = TaleText.ResolveName(_detail.pages[i]);
                }
                else
                {
                    var gender = PlayerPrefs.GetString("ft_gender", "male");
                    yield return _tales.Personalize(_detail.id, childName, gender,
                        onSuccess: pages => _detail.pages = pages,
                        onError: _ => { }, lang: Loc.Lang);
                }
            }
        }

        private void OnRead()
        {
            if (_detail == null) return;
            EnsureDownloadedThen(NarrationMode.None);
        }

        private void OnListen()
        {
            if (_tale == null) return;

            // Always ask the user which voice they want
            ShowVoicePanel();
        }

        private void ApplyTexts()
        {
            if (voiceTitleText) voiceTitleText.text = Loc.Get("choose_voice");
            if (voiceMaleText) voiceMaleText.text = Loc.Get("voice_male");
            if (voiceFemaleText) voiceFemaleText.text = Loc.Get("voice_female");
            if (voiceParentText) voiceParentText.text = Loc.Get("voice_parent");
            if (parentTitleText) parentTitleText.text = Loc.Get("parent_voices_title");
            if (newRecordingTitleText) newRecordingTitleText.text = Loc.Get("new_recording");
            if (speakerPlaceholderText) speakerPlaceholderText.text = Loc.Get("speaker_name");
            if (startRecordingText) startRecordingText.text = Loc.Get("start");
            if (voicesTitleText) voicesTitleText.text = Loc.Get("voices");
            if (noVoicesText) noVoicesText.text = Loc.Get("no_voices");
        }

        // Same gap ScreenManager keeps between two screens during a coordinated slide.
        private const float PanelGap = 40f;

        /// <summary>
        /// Swaps panels with the same swipe-up as a screen change (see ScreenManager.CoordinatedSlide):
        /// the current panel leaves upward while the next one rises from below. The tale's
        /// illustration and the music button stay put. Not animated when the screen is being
        /// prepared — it isn't visible yet, and the screen's own transition covers it.
        /// </summary>
        private void ShowPanel(GameObject panel, bool animate = true)
        {
            FinishPanelSlide();

            var prev = _currentPanel;
            _currentPanel = panel;
            bool showBack = panel != mainPanel;

            if (!animate || prev == null || prev == panel || panel == null)
            {
                foreach (var p in new[] { mainPanel, voicePanel, parentPanel })
                {
                    if (!p) continue;
                    p.SetActive(p == panel);
                    ((RectTransform)p.transform).anchoredPosition = Vector2.zero;
                }
                SetPanelBackVisible(showBack);
                return;
            }

            var prevRt = (RectTransform)prev.transform;
            var nextRt = (RectTransform)panel.transform;
            float height = Rect.rect.height;

            panel.SetActive(true);
            prevRt.anchoredPosition = Vector2.zero;
            nextRt.anchoredPosition = new Vector2(0f, -(height + PanelGap));
            SetInputEnabled(false);

            // Settled in OnKill, which runs whether the slide finishes or is cut short:
            // a slide killed mid-way must not leave two panels up and the screen deaf.
            _panelSeq = DOTween.Sequence()
                .Append(prevRt.DOAnchorPosY(height + PanelGap, AnimDuration).SetEase(Ease.InOutCubic))
                .Join(nextRt.DOAnchorPosY(0f, AnimDuration).SetEase(Ease.InOutCubic))
                .OnKill(() =>
                {
                    _panelSeq = null;
                    if (this == null) return; // killed by the link while the scene unloads
                    prev.SetActive(false);
                    prevRt.anchoredPosition = Vector2.zero;
                    nextRt.anchoredPosition = Vector2.zero;
                    SetPanelBackVisible(showBack);
                    SetInputEnabled(true);
                })
                .SetLink(gameObject);

            // The shared Back stays put, so it fades with the slide instead of travelling:
            // in when leaving the main panel, out when returning to it.
            if (_panelBackGroup && _panelBackGroup.alpha != (showBack ? 1f : 0f))
                _panelSeq.Join(_panelBackGroup.DOFade(showBack ? 1f : 0f, AnimDuration).SetEase(Ease.InOutCubic));
        }

        private void SetPanelBackVisible(bool visible)
        {
            if (!_panelBackGroup) return;
            _panelBackGroup.alpha = visible ? 1f : 0f;
            _panelBackGroup.interactable = visible;
            _panelBackGroup.blocksRaycasts = visible;
        }

        private void OnPanelBack()
        {
            if (_currentPanel == parentPanel) ShowVoicePanel();
            else ShowMainPanel();
        }

        private void FinishPanelSlide()
        {
            var seq = _panelSeq;
            _panelSeq = null;
            if (seq != null && seq.IsActive()) seq.Complete(true);
        }

        private void ShowVoicePanel() => ShowPanel(voicePanel);

        private void ShowMainPanel() => ShowPanel(mainPanel);

        private void ShowParentPanel(bool animate = true)
        {
            // Filled before the slide so the list rises already populated.
            RefreshVoices();
            ShowPanel(parentPanel, animate);
        }

        /// <summary>The recorder hands the parent back to the voices list instead of the tale's start panel.</summary>
        public void ReturnToParentVoices() => _returnToParentVoices = true;

        private void OnVoiceSelected(string voice, string narratorGender)
        {
            // Remember the narrator: reading later re-narrates missing pages and the
            // default mode does TTS of its own — both must use the same voice.
            UserProfile.NarratorGender = narratorGender;

            if (_detail?.pages == null)
            {
                RemoteLog.Event("narration_start_fail",
                    $"tale={_tale?.id}; voice={voice}; reason=detail_not_loaded");
                Toast.Show(Loc.Get("loading"));
                return;
            }

            var childName = UserProfile.ChildName;
            var childGender = UserProfile.ChildGender;

            // Keyed by the language of the TEXT we are about to narrate, which is
            // not always Loc.Lang (a missing translation falls back).
            var lang = TaleText.SpokenLang(_detail);

            // NarratorGender is already stored above, so the tag reflects this choice.
            bool cached = AssetCache.IsAiNarrationCached(_tale.id, lang, UserProfile.CacheTag);

            RemoteLog.Event("narration_start",
                $"tale={_tale.id}; voice={voice}; narratorGender={narratorGender}; " +
                $"lang={lang}; pages={_detail.pages.Length}; cached={cached}; " +
                $"tag={UserProfile.CacheTag}; name={UserProfile.ChildName}; " +
                $"childGender={childGender}; epoch={PlayerPrefs.GetInt("ft_narrationEpoch", 0)}; " +
                $"purged={PlayerPrefs.GetInt("ft_ai_narration_v2", 0)}; " +
                $"legacyFiles={AssetCache.LegacyAudioCount()}");

            // Check if AI narration is already cached with the same params
            if (cached)
            {
                EnsureDownloadedThen(NarrationMode.AI);
                return;
            }

            // Nothing to clear: audio for other params lives under its own tag folder,
            // so switching the narrator no longer throws away the take you may switch
            // back to. Pages from an interrupted run with THESE params are reused —
            // the progress screen skips what it already has.
            ShowProgressScreen(voice, narratorGender, childName, childGender);
        }

        private void ShowProgressScreen(string voice, string narratorGender,
            string childName, string childGender)
        {
            var progress = _screens.Get<NarrationProgressScreen>();
            if (progress == null)
            {
                RemoteLog.Event("narration_start_fail",
                    $"tale={_tale?.id}; reason=no_progress_screen");
                return;
            }

            progress.SetContext(_tale, _detail, _detail?.pages, voice, narratorGender,
                childName, childGender);
            _screens.Show<NarrationProgressScreen>();
        }

        private void EnsureDownloadedThen(NarrationMode mode, string parentVoiceId = null)
        {
            if (AssetCache.IsTaleDownloaded(_tale.id))
            {
                // RELEASE: Debug.Log($"[TaleDetail] Tale {_tale.id} already cached, opening");
                OpenReading(mode, parentVoiceId);
                return;
            }

            var download = _screens.Get<DownloadScreen>();
            if (download == null)
            {
                // RELEASE: Debug.LogError("[TaleDetail] DownloadScreen not found in scene!");
                OpenReading(mode, parentVoiceId);
                return;
            }

            // RELEASE: Debug.Log($"[TaleDetail] Starting download for {_tale.id}");
            download.SetSingleTale(_tale, () => OpenReading(mode, parentVoiceId));
            _screens.Show<DownloadScreen>();
        }

        private void OpenReading(NarrationMode mode, string parentVoiceId = null)
        {
            var reading = _screens.Get<ReadingScreen>();
            if (reading == null) return;

            reading.SetTale(_detail, mode, parentVoiceId);
            if (illustrationImage && illustrationImage.sprite != null)
                reading.SetInitialIllustration(illustrationImage.sprite);
            _screens.ShowOver<ReadingScreen>();
        }

        // ── Parent voices ───────────────────────────────────

        // Narration is free in every voice — only the tales themselves sit behind premium.
        private void OnParentVoice() => ShowParentPanel();

        private string VoicesLang => TaleText.SpokenLang(_detail);

        private void RefreshVoices()
        {
            if (voicesContent == null || voiceItemTemplate == null || _tale == null) return;

            foreach (Transform child in voicesContent)
                if (child != voiceItemTemplate.transform) Destroy(child.gameObject);

            var voices = ParentVoices.List(_tale.id, VoicesLang);
            foreach (var voice in voices)
            {
                var item = Instantiate(voiceItemTemplate, voicesContent);
                item.gameObject.SetActive(true);
                var v = voice;
                item.Bind(v, () => OnPlayParentVoice(v), () => OnDeleteParentVoice(v));
            }
            if (noVoicesText) noVoicesText.gameObject.SetActive(voices.Count == 0);
        }

        private void OnPlayParentVoice(ParentVoice voice)
        {
            if (_detail == null)
            {
                Toast.Show(Loc.Get("loading"));
                return;
            }
            EnsureDownloadedThen(NarrationMode.Parent, voice.id);
        }

        private void OnDeleteParentVoice(ParentVoice voice)
        {
            ConfirmPopup.Show(Loc.Get("delete_voice_confirm"), () =>
                ChildGatePopup.Show(() =>
                {
                    ParentVoices.Delete(_tale.id, VoicesLang, voice.id);
                    RefreshVoices();
                }));
        }

        private void OnStartRecording()
        {
            var speaker = speakerInput ? speakerInput.text.Trim() : "";
            if (speaker.Length == 0)
            {
                Toast.Show(Loc.Get("enter_speaker_name"));
                if (speakerInput)
                    speakerInput.transform.DOShakePosition(0.4f, new Vector3(12f, 0f, 0f), 20)
                        .SetLink(speakerInput.gameObject);
                return;
            }
            if (_detail == null)
            {
                Toast.Show(Loc.Get("loading"));
                return;
            }

            ChildGatePopup.Show(() =>
            {
                var reading = _screens.Get<ReadingScreen>();
                if (reading == null || _detail == null) return;

                var voice = ParentVoices.Create(_tale.id, VoicesLang, speaker);
                if (speakerInput) speakerInput.text = "";
                reading.SetRecordingMode(_detail, _tale, parentVoiceId: voice.id);
                _screens.ShowOver<ReadingScreen>();
            });
        }

        private void OnMusicToggle()
        {
            if (_musicManager) _musicManager.SetMuted(!_musicManager.IsMuted);
            UpdateMusicButtons();
        }

        private void UpdateMusicButtons()
        {
            if (_musicManager == null) return;
            bool muted = _musicManager.IsMuted;
            if (selectionBtnMusicOff) selectionBtnMusicOff.SetActive(muted);
            if (selectionBtnMusicOn) selectionBtnMusicOn.SetActive(!muted);
        }

        private void OnBack()
        {
            _screens.Show<LibraryScreen>();
        }
    }
}
