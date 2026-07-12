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
        private TaleDownloadService _downloader;

        private TaleSummary[] _tales;
        private TaleSummary _singleTale;
        private Action _onComplete;

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            var api = FindAnyObjectByType<ApiClient>();
            _downloader = new TaleDownloadService(api);
        }

        public void SetTales(TaleSummary[] tales)
        {
            _tales = tales;
            _singleTale = null;
            _onComplete = null;
        }

        public void SetSingleTale(TaleSummary tale, Action onComplete)
        {
            _singleTale = tale;
            _onComplete = onComplete;
            _tales = null;
        }

        protected override void OnPrepare()
        {
            if (progressBar) progressBar.value = 0f;
            if (statusText) statusText.text = Loc.Get("download.preparing");
        }

        protected override void OnShown()
        {
            if (_singleTale != null)
                StartCoroutine(DownloadSingleTale());
            else
                StartCoroutine(DownloadCovers());
        }

        private IEnumerator DownloadCovers()
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
                if (statusText) statusText.text = $"{Loc.Get("download.loading")} {tale.title}";
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

                if (statusText) statusText.text = $"{Loc.Get("download.narration")} {tale.title}";
                if (progressBar) progressBar.value = (float)step / totalSteps;

                yield return _downloader.DownloadDefaultNarration(tale.id, lang,
                    (_, __) => { });
                step++;
            }

            if (progressBar) progressBar.value = 1f;
            if (statusText) statusText.text = Loc.Get("download.done");
            yield return new WaitForSeconds(0.3f);

            _screens.Show<Library.LibraryScreen>();
        }

        private IEnumerator DownloadSingleTale()
        {
            if (statusText)
                statusText.text = $"{Loc.Get("download.loading")} {_singleTale.title}";

            string error = null;
            yield return _downloader.DownloadTaleContent(_singleTale,
                (step, total) => UpdateProgress(step, total),
                e => error = e);

            // RELEASE: if (error != null) Debug.LogError($"[Download] Failed: {error}");

            if (progressBar) progressBar.value = 1f;
            if (statusText) statusText.text = Loc.Get("download.done");
            yield return new WaitForSeconds(0.3f);

            _onComplete?.Invoke();
        }

        private void UpdateProgress(int step, int totalSteps)
        {
            if (progressBar)
                progressBar.value = (float)step / totalSteps;

            if (statusText)
            {
                var mb = _downloader.BytesDownloaded / (1024f * 1024f);
                statusText.text = $"{mb:F1} MB — {step}/{totalSteps}";
            }
        }
    }
}
