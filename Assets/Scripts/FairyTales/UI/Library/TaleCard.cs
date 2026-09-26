using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Cache;
using FairyTales.Diagnostics;
using FairyTales.Models;
using FairyTales.UI.Core;

namespace FairyTales.UI.Library
{
    public class TaleCard : MonoBehaviour
    {
        [SerializeField] private Image coverImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button button;

        [Header("Overlay (covers entire card)")]
        [SerializeField] private GameObject overlay;
        [SerializeField] private GameObject lockIcon;

        [Header("Coming Soon")]
        [SerializeField] private GameObject comingSoonBadge;
        [SerializeField] private TMP_Text comingSoonText;

        [Header("Gift (invite a friend)")]
        [Tooltip("Badge shown on a gift tale while it is still locked. Replace freely — the card only toggles it on and off.")]
        [SerializeField] private GameObject giftBadge;
        [SerializeField] private TMP_Text giftBadgeText;

        [Header("Download UI (inside overlay)")]
        [SerializeField] private Image circleFill;
        [SerializeField] private Image downloadArrow;
        [SerializeField] private TMP_Text sizeText;
        [SerializeField] private TMP_Text progressText;

        [Header("Appear")]
        [SerializeField] private float appearDuration = 0.4f;

        private TaleSummary _tale;
        private Action<TaleSummary> _onClick;
        private ApiClient _api;
        private bool _isLocked;
        private bool _isDownloaded;
        private bool _isDownloading;
        private bool _downloadFailed;
        private bool _isComingSoon;
        private bool _isGift;
        private Coroutine _downloadCoroutine;
        private Coroutine _coverCoroutine;
        private CanvasGroup _group;
        private Tween _appearTween;
        private bool _appeared;

        public bool IsDownloaded => _isDownloaded;

        public void Init(TaleSummary tale, bool isLocked, bool isDownloaded,
            ApiClient api, Action<TaleSummary> onClick)
        {
            CancelDownload();

            // The same tale re-initialised in place (the grid is rebuilt every time the
            // library is shown) keeps its card on screen; a new tale in this slot
            // starts hidden and fades in once its cover is ready.
            bool sameTale = _appeared && _tale != null && _tale.id == tale.id;
            if (!sameTale) Conceal();

            _tale = tale;
            _onClick = onClick;
            _api = api;
            _isComingSoon = tale.comingSoon;
            // Coming-soon tales are never lockable/downloadable — they have no pages.
            _isLocked = isLocked && !_isComingSoon;
            // Only worth advertising while the tale is still locked — once the friend
            // has redeemed the code it is an ordinary tale.
            _isGift = tale.gift && _isLocked;
            _isDownloaded = isDownloaded;
            _isDownloading = false;
            _downloadFailed = false;

            if (button) button.onClick.RemoveAllListeners();
            if (titleText) titleText.text = tale.GetTitle(Loc.Lang);

            if (_coverCoroutine != null)
            {
                StopCoroutine(_coverCoroutine);
                _coverCoroutine = null;
            }

            var cached = CoverProvider.GetCached(tale.id);
            if (cached != null)
            {
                if (coverImage) coverImage.sprite = cached;
                Appear();
            }
            else
            {
                // Cards whose cover is still on its way (disk, or the server for tales
                // not downloaded yet) stay hidden and come in one by one as they land.
                var id = tale.id;
                _coverCoroutine = StartCoroutine(CoverProvider.GetAsync(id, s =>
                {
                    _coverCoroutine = null;
                    if (_tale == null || _tale.id != id) return;
                    if (s != null && coverImage) coverImage.sprite = s;
                    Appear();
                }, _api));
            }

            if (button) button.onClick.AddListener(OnClick);
            UpdateOverlay();
        }

        private CanvasGroup Group
        {
            get
            {
                if (!_group) _group = GetComponent<CanvasGroup>();
                if (!_group) _group = gameObject.AddComponent<CanvasGroup>();
                return _group;
            }
        }

        /// <summary>Hide the card and keep it from taking taps until it has appeared.</summary>
        private void Conceal()
        {
            _appearTween?.Kill();
            _appeared = false;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
        }

        private void Appear()
        {
            if (_appeared) return;
            _appeared = true;
            Group.blocksRaycasts = true;
            _appearTween?.Kill();
            _appearTween = Group.DOFade(1f, appearDuration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        /// <summary>
        /// Refresh download state without full re-init (called when returning to library).
        /// </summary>
        public void RefreshDownloadState()
        {
            if (_isDownloading || _isComingSoon) return;
            _isDownloaded = _tale != null && AssetCache.IsTaleDownloaded(_tale.id);
            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            if (giftBadge) giftBadge.SetActive(_isGift && !_isComingSoon);
            if (giftBadgeText && _isGift) giftBadgeText.text = Loc.Get("gift_badge");

            // Coming soon: badge only, no lock/download UI.
            if (comingSoonBadge) comingSoonBadge.SetActive(_isComingSoon);
            if (_isComingSoon)
            {
                if (comingSoonText) comingSoonText.text = Loc.Get("coming_soon");
                if (overlay) overlay.SetActive(false);
                if (lockIcon) lockIcon.SetActive(false);
                return;
            }

            bool needsDownload = !_isDownloaded && !_isLocked;
            // A gift tale is locked but must not look locked: the badge is the invitation,
            // and a dimmed cover with a padlock reads as "you can't have this".
            bool showLock = _isLocked && !_isGift;
            bool showOverlay = showLock || needsDownload;

            if (overlay) overlay.SetActive(showOverlay);

            // Lock
            if (lockIcon) lockIcon.SetActive(showLock);

            // Download: idle (circle full + arrow + size)
            bool idle = needsDownload && !_isDownloading;

            if (downloadArrow)
                downloadArrow.gameObject.SetActive(idle);

            if (sizeText)
            {
                sizeText.gameObject.SetActive(idle);
                if (idle && _tale != null && _tale.downloadSize > 0)
                {
                    float mb = _tale.downloadSize / (1024f * 1024f);
                    sizeText.text = mb >= 1f ? $"{mb:F1} MB" : $"{_tale.downloadSize / 1024f:F0} KB";
                }
            }

            // Download: in progress (circle fills + percentage)
            if (progressText)
                progressText.gameObject.SetActive(_isDownloading);

            if (circleFill)
            {
                circleFill.gameObject.SetActive(needsDownload);
                circleFill.fillAmount = idle ? 1f : 0f;
            }
        }

        private void OnClick()
        {
            Analytics.Log("tale_card_click", ("tale_id", _tale.id),
                ("is_free", _tale.free), ("coming_soon", _isComingSoon), ("is_gift", _isGift));

            // Coming soon → non-interactive (no open, no download, no narrate).
            if (_isComingSoon)
            {
                Analytics.Log("tale_coming_soon_click", ("tale_id", _tale.id));
                return;
            }

            // Downloading → cancel
            if (_isDownloading)
            {
                CancelDownload();
                _downloadFailed = false;
                UpdateOverlay();
                return;
            }

            // Locked → payment
            if (_isLocked)
            {
                _onClick?.Invoke(_tale);
                return;
            }

            // Not downloaded → start download (skip if last attempt just failed)
            if (!_isDownloaded)
            {
                if (_downloadFailed)
                {
                    _downloadFailed = false;
                    Toast.Show(Loc.Get("error"));
                    return;
                }
                BeginDownload();
                return;
            }

            // Downloaded → open
            _onClick?.Invoke(_tale);
        }

        private void BeginDownload()
        {
            if (_api == null) return;

            // Double-check: maybe it was downloaded since Init
            if (AssetCache.IsTaleDownloaded(_tale.id))
            {
                _isDownloaded = true;
                UpdateOverlay();
                _onClick?.Invoke(_tale);
                return;
            }

            _isDownloading = true;
            _downloadFailed = false;
            UpdateOverlay();
            _downloadCoroutine = StartCoroutine(DownloadRoutine());
        }

        private IEnumerator DownloadRoutine()
        {
            var downloader = new TaleDownloadService(_api);
            bool gotProgress = false;
            string error = null;
            float startTime = Time.realtimeSinceStartup;

            float sizeMb = _tale.downloadSize / (1024f * 1024f);
            Analytics.Log("tale_download_start",
                ("tale_id", _tale.id), ("download_size_mb", sizeMb));

            yield return downloader.DownloadTaleContent(_tale,
                (step, total) =>
                {
                    gotProgress = true;
                    float progress = (float)step / total;
                    if (progressText) progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
                    if (circleFill) circleFill.fillAmount = progress;
                },
                e => error = e ?? "unknown");

            _downloadCoroutine = null;
            _isDownloading = false;
            _isDownloaded = AssetCache.IsTaleDownloaded(_tale.id);

            int durationMs = Mathf.RoundToInt((Time.realtimeSinceStartup - startTime) * 1000f);
            if (_isDownloaded)
            {
                Analytics.Log("tale_download_complete",
                    ("tale_id", _tale.id), ("duration_ms", durationMs));
            }
            else
            {
                _downloadFailed = true;
                Analytics.Log("tale_download_error", ("tale_id", _tale.id),
                    ("error_code", gotProgress ? "incomplete" : "no_data"),
                    ("error_message", error ?? ""));
                // No progress at all = server didn't return tale data
                if (!gotProgress)
                    Toast.Show(Loc.Get("error"));
            }

            UpdateOverlay();
        }

        private void CancelDownload()
        {
            if (_downloadCoroutine != null)
            {
                StopCoroutine(_downloadCoroutine);
                _downloadCoroutine = null;
            }
            _isDownloading = false;
        }

        private void OnDestroy()
        {
            CancelDownload();
            if (button) button.onClick.RemoveListener(OnClick);
        }
    }
}
