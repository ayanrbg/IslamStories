using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Cache;
using FairyTales.Diagnostics;
using FairyTales.Models;
using FairyTales.IAP;
using Newtonsoft.Json;
using FairyTales.Audio;
using FairyTales.UI.Core;
using FairyTales.UI.Narration;
using FairyTales.UI.Onboarding;
using FairyTales.UI.Payment;
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
        [SerializeField] private Button btnNarrate;
        [SerializeField] private Button btnBack;
        [SerializeField] private Button btnMusic;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;

        [SerializeField] private TMP_Text btnNarrateText;

        [Header("Voice Selection (child panel)")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject voicePanel;
        [SerializeField] private TMP_Text voiceTitleText;
        [SerializeField] private Button btnVoiceMale;
        [SerializeField] private Button btnVoiceFemale;
        [SerializeField] private Button btnVoiceParent;
        [SerializeField] private Button btnVoiceBack;

        private ScreenManager _screens;
        private TalesService _tales;
        private NarrationService _narration;
        private BackgroundMusicManager _musicManager;
        private TaleSummary _tale;
        private TaleDetail _detail;
        private string _narrationStatus; // "done","ready","processing","error",null

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            var api = FindAnyObjectByType<ApiClient>();
            _tales = new TalesService(api);
            _narration = new NarrationService(api);
            _musicManager = FindAnyObjectByType<BackgroundMusicManager>();

            if (btnRead) btnRead.onClick.AddListener(OnRead);
            if (btnListen) btnListen.onClick.AddListener(OnListen);
            if (btnNarrate) btnNarrate.onClick.AddListener(OnNarrate);
            if (btnBack) btnBack.onClick.AddListener(OnBack);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);

            if (btnVoiceMale) btnVoiceMale.onClick.AddListener(() => OnVoiceSelected("narrator", "male"));
            if (btnVoiceFemale) btnVoiceFemale.onClick.AddListener(() => OnVoiceSelected("narrator", "female"));
            if (btnVoiceParent) btnVoiceParent.onClick.AddListener(OnParentVoice);
            if (btnVoiceBack) btnVoiceBack.onClick.AddListener(ShowMainPanel);
        }

        public void SetTale(TaleSummary tale)
        {
            _tale = tale;
            _detail = null;
            _narrationStatus = null;

            // Start loading immediately so content is ready before transition animation
            if (titleText) titleText.text = _tale?.title ?? "";
            if (pageCountText) pageCountText.text = "";
            LoadIllustration();
            StartCoroutine(LoadDetailAndPersonalize());
            StartCoroutine(LoadNarrationStatus());
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
            ShowMainPanel();
            UpdateNarrateButtonText();
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

            // 1. Bundled tales: load local JSON (has all language translations)
            if (BundledTaleLoader.IsBundled(_tale.id))
            {
                yield return BundledTaleLoader.LoadTaleJson(_tale.id, d =>
                {
                    _detail = d;
                    UpdateUI();
                });
                if (_detail != null) localText = true;
            }

            // 2. Downloaded tale: cached text for the current language (offline + correct)
            if (_detail == null)
            {
                var cached = AssetCache.Load(AssetCache.TaleTextKey(_tale.id, Loc.Lang));
                if (cached != null)
                {
                    _detail = JsonConvert.DeserializeObject<TaleDetail>(
                        System.Text.Encoding.UTF8.GetString(cached));
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
                            AssetCache.Save(AssetCache.TaleTextKey(_tale.id, Loc.Lang),
                                System.Text.Encoding.UTF8.GetBytes(
                                    JsonConvert.SerializeObject(d)));
                        UpdateUI();
                    },
                    onError: e => { } /* RELEASE: Debug.LogWarning($"[TaleDetail] Server: {e}") */,
                    lang: Loc.Lang);
            }

            // Personalize pages (replace {childName}; gender {m:|f:} is resolved at render time)
            if (_detail?.pages != null)
            {
                var childName = PlayerPrefs.GetString("ft_childName", "");
                if (localText)
                {
                    for (int i = 0; i < _detail.pages.Length; i++)
                        _detail.pages[i] = _detail.pages[i].Replace("{childName}", childName);
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

        private IEnumerator LoadNarrationStatus()
        {
            if (_tale == null) yield break;

            yield return _narration.GetNarrationStatus(_tale.id,
                onSuccess: s => _narrationStatus = s.status,
                onError: _ => _narrationStatus = null);
        }

        private void OnRead()
        {
            if (_detail == null) return;
            EnsureDownloadedThen(NarrationMode.None);
        }

        private bool IsPremium =>
            IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;

        private void OnListen()
        {
            if (_tale == null) return;

            // Always ask the user which voice they want
            ShowVoicePanel();
        }

        private void ShowVoicePanel()
        {
            if (mainPanel) mainPanel.SetActive(false);
            if (voicePanel) voicePanel.SetActive(true);
            if (voiceTitleText) voiceTitleText.text = Loc.Get("choose_voice");
            // Parent voice is a real per-page recording now — available in every language.
            if (btnVoiceParent) btnVoiceParent.gameObject.SetActive(true);
        }

        private void ShowMainPanel()
        {
            if (voicePanel) voicePanel.SetActive(false);
            if (mainPanel) mainPanel.SetActive(true);
        }

        private void OnVoiceSelected(string voice, string narratorGender)
        {
            ShowMainPanel();

            var childName = PlayerPrefs.GetString("ft_childName", "");
            var childGender = PlayerPrefs.GetString("ft_gender", "male");

            // Check if AI narration is already cached with the same params
            if (AssetCache.IsAiNarrationCached(_tale.id, Loc.Lang,
                    voice, narratorGender, childName, childGender)
                && _detail != null)
            {
                EnsureDownloadedThen(NarrationMode.AI);
                return;
            }

            // Narration is processing on server — show progress, don't restart
            if (_narrationStatus == "processing")
            {
                ShowProgressScreen();
                return;
            }

            StartCoroutine(StartNarrationWithVoice(voice, narratorGender));
        }

        private IEnumerator StartNarrationWithVoice(string voice, string narratorGender)
        {
            var childName = PlayerPrefs.GetString("ft_childName", "");
            var gender = PlayerPrefs.GetString("ft_gender", "male");

            // Clear old AI narration cache — params changed
            AssetCache.ClearAiNarration(_tale.id);

            bool bundled = BundledTaleLoader.IsBundled(_tale.id);
            string[] pages = bundled && _detail?.pages != null ? _detail.pages : null;

            bool started = false;
            yield return _narration.NarrateAll(_tale.id, childName, gender,
                onSuccess: _ => started = true,
                onError: e =>
                {
                    Debug.LogWarning($"[TaleDetail] NarrateAll failed: {e}");
                    Toast.Show(Loc.Get("narration_error"));
                },
                voice: voice, lang: Loc.Lang, pages: pages,
                narratorGender: narratorGender);

            if (!started) yield break;

            ShowProgressScreen(voice, narratorGender, childName, gender);
        }

        private void ShowProgressScreen(string voice = null, string narratorGender = null,
            string childName = null, string childGender = null)
        {
            var progress = _screens.Get<NarrationProgressScreen>();
            if (progress == null) return;

            bool bundled = BundledTaleLoader.IsBundled(_tale.id);
            string[] pages = bundled && _detail?.pages != null ? _detail.pages : null;
            progress.SetContext(_tale, _detail, pages, voice, narratorGender,
                childName, childGender);
            _screens.Show<NarrationProgressScreen>();
        }

        private void EnsureDownloadedThen(NarrationMode mode)
        {
            if (AssetCache.IsTaleDownloaded(_tale.id))
            {
                // RELEASE: Debug.Log($"[TaleDetail] Tale {_tale.id} already cached, opening");
                OpenReading(mode);
                return;
            }

            var download = _screens.Get<DownloadScreen>();
            if (download == null)
            {
                // RELEASE: Debug.LogError("[TaleDetail] DownloadScreen not found in scene!");
                OpenReading(mode);
                return;
            }

            // RELEASE: Debug.Log($"[TaleDetail] Starting download for {_tale.id}");
            download.SetSingleTale(_tale, () => OpenReading(mode));
            _screens.Show<DownloadScreen>();
        }

        private void OpenReading(NarrationMode mode)
        {
            var reading = _screens.Get<ReadingScreen>();
            if (reading == null) return;

            reading.SetTale(_detail, mode);
            if (illustrationImage && illustrationImage.sprite != null)
                reading.SetInitialIllustration(illustrationImage.sprite);
            _screens.ShowOver<ReadingScreen>();
        }

        private void UpdateNarrateButtonText()
        {
            if (btnNarrate) btnNarrate.gameObject.SetActive(true);
            if (btnNarrateText == null) return;
            btnNarrateText.text = Loc.Get("narrate");
        }

        private void OnNarrate()
        {
            if (!IsPremium)
            {
                PaymentScreen.Open(_screens, typeof(TaleDetailScreen), source: "narrate");
                return;
            }

            ChildGatePopup.Show(() =>
            {
                if (_detail == null) return;
                var reading = _screens.Get<ReadingScreen>();
                if (reading == null) return;

                reading.SetRecordingMode(_detail, _tale);
                _screens.ShowOver<ReadingScreen>();
            });
        }

        // ── Parent voice in Listen panel ────────────────────

        private void OnParentVoice()
        {
            ShowMainPanel();

            // 1. No premium → payment
            if (!IsPremium)
            {
                PaymentScreen.Open(_screens, typeof(TaleDetailScreen), source: "parent_voice");
                return;
            }

            // 2. Nothing recorded yet → prompt the parent to record via "Озвучить"
            if (!AssetCache.HasAnyParentNarration(_tale.id, Loc.Lang))
            {
                Toast.Show(Loc.Get("record_voice_first"));
                return;
            }

            if (_detail == null) return;

            // 3. Play the parent's real recorded voice, page by page.
            EnsureDownloadedThen(NarrationMode.Parent);
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
