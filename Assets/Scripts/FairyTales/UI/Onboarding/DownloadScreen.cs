using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Api;
using FairyTales.Cache;
using FairyTales.Models;
using FairyTales.UI.Core;

namespace FairyTales.UI.Onboarding
{
    public class DownloadScreen : BaseScreen
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text statusText;

        private ScreenManager _screens;
        private ApiClient _api;
        private TaleDownloadService _downloader;

        private TaleSummary[] _tales;
        private TaleSummary _singleTale;
        private Action _onComplete;
        private TalesPreloader _preload;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            _api = FindAnyObjectByType<ApiClient>();
            _downloader = new TaleDownloadService(_api);
        }

        public void SetTales(TaleSummary[] tales)
        {
            _tales = tales;
            _singleTale = null;
            _onComplete = null;
            _preload = null;
        }

        public void SetSingleTale(TaleSummary tale, Action onComplete)
        {
            _singleTale = tale;
            _onComplete = onComplete;
            _tales = null;
            _preload = null;
        }

        /// <summary>
        /// Picks up a background load that hadn't finished (or didn't fully succeed) by
        /// the time onboarding ended: waits it out, then fetches whatever it missed.
        /// </summary>
        public void SetPreload(TalesPreloader preload)
        {
            _preload = preload;
            _tales = null;
            _singleTale = null;
            _onComplete = null;
        }

        protected override void OnPrepare()
        {
            if (progressBar) progressBar.value = 0f;
            // Single static caption for the whole download — no per-tale titles.
            if (statusText) statusText.text = Loc.Get("download.tales");
        }

        protected override void OnShown()
        {
            if (_singleTale != null)
                StartCoroutine(DownloadSingleTale());
            else if (_preload != null)
                StartCoroutine(FinishPreload());
            else
                StartCoroutine(DownloadCovers());
        }

        private IEnumerator FinishPreload()
        {
            var preload = _preload;
            _preload = null;

            while (preload.IsRunning)
            {
                if (progressBar) progressBar.value = preload.Progress;
                yield return null;
            }

            // Something failed in the background: one more pass over the same list. It
            // skips what is already on disk.
            if (!preload.AllReady && preload.Tales != null && preload.Tales.Length > 0)
            {
                _tales = preload.Tales;
                yield return DownloadAssets();
            }

            // The grid is built behind this screen, so the cards are in place when the
            // library slides in.
            var library = _screens.Get<Library.LibraryScreen>();
            if (preload.Tales != null) library?.SetCatalog(preload.Tales, preload.FromServer);
            else library?.MarkNeedsRefresh();

            if (progressBar) progressBar.value = 1f;
            yield return new WaitForSeconds(0.3f);

            _screens.Show<Library.LibraryScreen>();
        }

        private IEnumerator DownloadCovers()
        {
            yield return DownloadAssets();

            if (progressBar) progressBar.value = 1f;
            yield return new WaitForSeconds(0.3f);

            _screens.Show<Library.LibraryScreen>();
        }

        /// <summary>Covers, then default narration, then the covers decoded into memory.</summary>
        private IEnumerator DownloadAssets()
        {
            if (_tales == null || _tales.Length == 0) yield break;

            // Count total steps: covers + narration for tales that have it
            int narrationCount = 0;
            foreach (var t in _tales)
                if (t.hasDefaultNarration) narrationCount++;
            int totalSteps = _tales.Length + narrationCount;
            int step = 0;

            // Phase 1 — covers
            for (int i = 0; i < _tales.Length; i++)
            {
                var tale = _tales[i];
                if (progressBar) progressBar.value = (float)step / totalSteps;

                yield return _downloader.DownloadCover(tale);
                step++;
            }

            // Phase 2 — default narration (user's language)
            var lang = PlayerPrefs.GetString("ft_lang", "ru");
            for (int i = 0; i < _tales.Length; i++)
            {
                var tale = _tales[i];
                if (!tale.hasDefaultNarration) continue;

                if (progressBar) progressBar.value = (float)step / totalSteps;

                yield return _downloader.DownloadDefaultNarration(tale.id, lang,
                    (_, __) => { });
                step++;
            }

            // Phase 3 — covers into memory while this screen is still up, so the
            // library's cards have them the moment it slides in.
            foreach (var tale in _tales)
                if (!tale.IsHidden)
                    yield return Library.CoverProvider.GetAsync(tale.id, _ => { }, _api);
        }

        private IEnumerator DownloadSingleTale()
        {
            string error = null;
            yield return _downloader.DownloadTaleContent(_singleTale,
                (step, total) => UpdateProgress(step, total),
                e => error = e);

            // RELEASE: if (error != null) Debug.LogError($"[Download] Failed: {error}");

            if (progressBar) progressBar.value = 1f;
            yield return new WaitForSeconds(0.3f);

            _onComplete?.Invoke();
        }

        private void UpdateProgress(int step, int totalSteps)
        {
            if (progressBar)
                progressBar.value = (float)step / totalSteps;
        }
    }
}
