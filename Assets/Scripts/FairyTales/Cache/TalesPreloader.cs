using System.Collections;
using FairyTales.Api;
using FairyTales.Models;

namespace FairyTales.Cache
{
    /// <summary>
    /// Everything the library needs on first open — registration, the tale list, every
    /// cover and the default narration — fetched in the background while the user is
    /// still going through onboarding. When it has finished cleanly the download screen
    /// has nothing left to do and is skipped; otherwise that screen picks up from here.
    /// </summary>
    public class TalesPreloader
    {
        private readonly ApiClient _api;
        private readonly AuthService _auth;
        private readonly TalesService _tales;
        private readonly TaleDownloadService _downloader;

        private bool _started;
        private int _failures;

        public TalesPreloader(ApiClient api)
        {
            _api = api;
            _auth = new AuthService(api);
            _tales = new TalesService(api);
            _downloader = new TaleDownloadService(api);
        }

        public bool IsRunning { get; private set; }

        /// <summary>The list the library will show; null until known.</summary>
        public TaleSummary[] Tales { get; private set; }

        /// <summary>True when <see cref="Tales"/> is the server's catalog, not the bundled stand-in.</summary>
        public bool FromServer { get; private set; }

        /// <summary>0…1 across covers and narration, for the download screen's bar.</summary>
        public float Progress { get; private set; }

        /// <summary>
        /// True only when the run is over, the list came from the server and nothing
        /// failed: every cover on disk and the narration fetched.
        /// </summary>
        public bool AllReady => _started && !IsRunning && Tales != null && _failures == 0
                                && !HasMissingCovers(Tales);

        public IEnumerator Run(string userId, string childName, string gender, string lang)
        {
            _started = true;
            IsRunning = true;
            Progress = 0f;

            yield return _auth.Register(userId, childName, gender, lang,
                onSuccess: _ => { },
                onError: _ => { });

            TaleSummary[] tales = null;
            yield return _tales.GetTales(lang, onSuccess: t => tales = t, onError: _ => { });
            FromServer = tales != null;
            if (tales == null)
            {
                // Offline: the bundled list lets the library open, but it is not the real
                // catalog, so the download screen still gets its turn.
                _failures++;
                yield return BundledTaleLoader.LoadManifest(lang, t => tales = t);
            }
            Tales = tales;

            if (tales != null && tales.Length > 0)
            {
                int narrationCount = 0;
                foreach (var t in tales)
                    if (t.hasDefaultNarration) narrationCount++;
                float total = tales.Length + narrationCount;
                int step = 0;

                foreach (var tale in tales)
                {
                    yield return _downloader.DownloadCover(tale, _ => _failures++);
                    Progress = ++step / total;
                }

                foreach (var tale in tales)
                {
                    if (!tale.hasDefaultNarration) continue;
                    yield return _downloader.DownloadDefaultNarration(tale.id, lang,
                        (_, __) => { }, _ => _failures++);
                    Progress = ++step / total;
                }

                // Decode the covers into memory now, one at a time, while the user is
                // still reading the intro: the library's cards then have their cover the
                // moment the screen slides in. The library keeps these anyway once shown.
                foreach (var tale in tales)
                    if (!tale.IsHidden)
                        yield return UI.Library.CoverProvider.GetAsync(tale.id, _ => { }, _api);
            }

            Progress = 1f;
            IsRunning = false;
        }

        private static bool HasMissingCovers(TaleSummary[] tales)
        {
            foreach (var t in tales)
                if (!BundledTaleLoader.IsBundled(t.id) && !AssetCache.IsCoverDownloaded(t.id))
                    return true;
            return false;
        }
    }
}
