using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Audio;
using FairyTales.Cache;
using FairyTales.UI.Core;

namespace FairyTales.UI.Reading
{
    /// <summary>
    /// Parent narration recorder. The parent taps Record once and simply reads the
    /// book aloud: each page is captured as its own short clip and finalized on page
    /// turn (auto-split). Tapping Stop finalizes the current page and enables the
    /// Play button, which previews every recorded page in order with auto page-flip.
    /// The "Done" button doubles as Play — exit is via the reading Home button
    /// (clips are saved incrementally, so leaving already keeps them).
    /// Nothing goes to the server — clips are stored locally per (tale, language, page)
    /// and played back verbatim during reading.
    /// </summary>
    public class RecordingOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;

        [Header("Record / Stop button")]
        [SerializeField] private Button btnRecord;
        [SerializeField] private GameObject iconRecord;   // shown when idle
        [SerializeField] private GameObject iconRerecord; // shown while recording (acts as Stop)

        [Header("Play / Stop-preview button (reuses the old Done button)")]
        [SerializeField] private Button btnDone;
        [SerializeField] private GameObject iconDoneEnabled;  // Play available (has audio, idle)
        [SerializeField] private GameObject iconDoneDisabled; // greyed (nothing recorded yet)

        // ── Segment duration cap per page (seconds) ──
        private const int MaxPageSeconds = 120;

        private MicRecorder _mic;
        private NarrationPlayer _player;
        private PageNavigator _pageNavigator;
        private string _taleId;
        private string _lang;

        private bool _recording;      // a mic segment is currently active
        private bool _micStarted;     // mic actually began capturing (past the permission prompt)
        private bool _previewing;     // playing back recorded pages
        private int _recordingPage;   // page whose audio the current segment belongs to
        private int _previewPage;

        public bool IsActive => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (btnRecord) btnRecord.onClick.AddListener(OnRecordButton);
            if (btnDone) btnDone.onClick.AddListener(OnPreviewToggle);
        }

        public void Activate(string taleId, string lang,
            PageNavigator pageNavigator, NarrationPlayer player)
        {
            _taleId = taleId;
            _lang = lang;
            _pageNavigator = pageNavigator;
            _player = player;

            _mic = FindAnyObjectByType<MicRecorder>();
            if (_mic)
            {
                _mic.SetMaxDuration(MaxPageSeconds);
                _mic.OnStarted -= OnMicStarted;
                _mic.OnStarted += OnMicStarted;
            }

            _recording = false;
            _micStarted = false;
            _previewing = false;

            if (panel) panel.SetActive(true);
            if (timerText) timerText.text = "00:00";
            if (statusText) statusText.text = Loc.Get("rec_hint");
            RefreshUi();
        }

        public void Deactivate()
        {
            if (_recording) FinalizeSegment();
            _recording = false;
            StopPreview();
            if (_mic)
            {
                _mic.OnStopped -= OnSegmentStopped;
                _mic.OnStarted -= OnMicStarted;
            }
            if (panel) panel.SetActive(false);
        }

        // ── Record / Stop ───────────────────────────────────────
        private void OnRecordButton()
        {
            if (_mic == null) return;
            if (_previewing) StopPreview();

            if (_recording)
                StopRecording();
            else
                StartRecordingAt(_pageNavigator != null ? _pageNavigator.CurrentPage : 0);
        }

        private void StartRecordingAt(int page)
        {
            _recordingPage = page;
            _micStarted = false;
            _mic.OnStopped -= OnSegmentStopped;
            _mic.OnStopped += OnSegmentStopped;
            _mic.StartRecording();
            _recording = true;

            if (timerText) timerText.text = "00:00";
            if (statusText) statusText.text = Loc.Get("rec_recording");
            RefreshUi();
        }

        private void StopRecording()
        {
            FinalizeSegment();
            _recording = false;
            if (statusText)
                statusText.text = HasAnyRecording()
                    ? Loc.Get("rec_play_hint")
                    : Loc.Get("rec_hint");
            RefreshUi();
        }

        /// <summary>Stops the mic; OnSegmentStopped saves the clip for _recordingPage synchronously.</summary>
        private void FinalizeSegment()
        {
            if (_mic != null && _mic.IsRecording)
                _mic.StopRecording();
        }

        private void OnMicStarted() => _micStarted = true;

        private void OnSegmentStopped(byte[] wav)
        {
            if (wav != null && wav.Length > 0)
                AssetCache.SaveParentNarration(_taleId, _lang, _recordingPage, wav);
        }

        /// <summary>
        /// Called by ReadingScreen whenever the visible page changes. While recording,
        /// a page turn finalizes the current page's clip and immediately starts capturing
        /// the new page (auto-split). Ignored during preview playback.
        /// </summary>
        public void OnPageVisited(int page)
        {
            if (_previewing) return;
            if (!_recording) return;

            FinalizeSegment();       // save clip for the page we just left
            StartRecordingAt(page);  // begin capturing the new page
        }

        // ── Preview playback (btnDone doubles as Play/Stop) ──────
        private void OnPreviewToggle()
        {
            if (_previewing) { StopPreview(); return; }
            if (_recording) StopRecording();
            if (!HasAnyRecording()) return;

            int first = FirstRecordedPage();
            if (first < 0) return;

            _previewing = true;
            if (statusText) statusText.text = Loc.Get("rec_playing");
            RefreshUi();
            PlayPreviewPage(first);
        }

        private void PlayPreviewPage(int page)
        {
            _previewPage = page;
            if (_pageNavigator != null && _pageNavigator.CurrentPage != page)
                _pageNavigator.GoToPage(page);

            var wav = AssetCache.LoadParentNarration(_taleId, _lang, page);
            if (wav == null || _player == null)
            {
                AdvancePreview();
                return;
            }

            _player.OnFinished -= OnPreviewClipFinished;
            _player.OnFinished += OnPreviewClipFinished;
            _player.PlayFromBytes(wav);
        }

        private void OnPreviewClipFinished()
        {
            if (_player) _player.OnFinished -= OnPreviewClipFinished;
            if (!_previewing) return;
            AdvancePreview();
        }

        private void AdvancePreview()
        {
            int next = NextRecordedPage(_previewPage);
            if (next < 0) StopPreview();
            else PlayPreviewPage(next);
        }

        private void StopPreview()
        {
            if (!_previewing && (_player == null || !_player.IsPlaying)) { RefreshUi(); return; }
            _previewing = false;
            if (_player)
            {
                _player.OnFinished -= OnPreviewClipFinished;
                _player.Stop();
            }
            if (statusText && !_recording)
                statusText.text = HasAnyRecording() ? Loc.Get("rec_play_hint") : Loc.Get("rec_hint");
            RefreshUi();
        }

        // ── Helpers ──────────────────────────────────────────────
        private bool HasAnyRecording()
            => AssetCache.HasAnyParentNarration(_taleId, _lang);

        private int TotalPages => _pageNavigator != null ? _pageNavigator.TotalPages : 0;

        private int FirstRecordedPage()
        {
            for (int i = 0; i < TotalPages; i++)
                if (AssetCache.HasParentNarrationPage(_taleId, _lang, i)) return i;
            return -1;
        }

        private int NextRecordedPage(int after)
        {
            for (int i = after + 1; i < TotalPages; i++)
                if (AssetCache.HasParentNarrationPage(_taleId, _lang, i)) return i;
            return -1;
        }

        private void Update()
        {
            if (_recording)
            {
                // Reconcile with mic auto-stop (segment hit the length cap). Guard with
                // _micStarted so the Android permission prompt (mic not yet capturing)
                // isn't mistaken for a stop.
                if (_micStarted && _mic != null && !_mic.IsRecording)
                {
                    _recording = false;
                    if (statusText)
                        statusText.text = HasAnyRecording() ? Loc.Get("rec_play_hint") : Loc.Get("rec_hint");
                    RefreshUi();
                    return;
                }

                if (_mic != null && timerText)
                {
                    var e = _mic.ElapsedTime;
                    timerText.text = $"{(int)e / 60:00}:{(int)e % 60:00}";
                }
            }
        }

        private void RefreshUi()
        {
            bool hasAudio = HasAnyRecording();

            // Record / Stop button: tappable unless previewing; icon reflects state.
            if (btnRecord) btnRecord.interactable = !_previewing;
            if (iconRecord) iconRecord.SetActive(!_recording);
            if (iconRerecord) iconRerecord.SetActive(_recording);

            // Play button (reused Done): active once something is recorded and not
            // mid-recording. Tapping toggles preview play/stop.
            bool canPreview = hasAudio && !_recording;
            if (btnDone) btnDone.interactable = canPreview;
            if (iconDoneEnabled) iconDoneEnabled.SetActive(canPreview);
            if (iconDoneDisabled) iconDoneDisabled.SetActive(!canPreview);
        }
    }
}
