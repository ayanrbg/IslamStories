using System;
using System.Collections;
using FairyTales.Api;
using FairyTales.Models;
using FairyTales.UI.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace FairyTales.Cache
{
    public class TaleDownloadService
    {
        private readonly ApiClient _api;
        private long _bytesDownloaded;

        public long BytesDownloaded => _bytesDownloaded;

        public TaleDownloadService(ApiClient api) => _api = api;

        public IEnumerator DownloadCover(TaleSummary tale, Action<string> onError = null)
        {
            if (BundledTaleLoader.IsBundled(tale.id)) yield break;
            if (AssetCache.IsCoverDownloaded(tale.id)) yield break;

            yield return DownloadImage(
                $"/api/tales/{tale.id}/cover",
                AssetCache.CoverKey(tale.id), onError);
        }

        /// <summary>
        /// Downloads illustrations + default narration for a tale.
        /// onProgress(step, totalSteps) reports per-file progress.
        /// BytesDownloaded tracks total bytes for display.
        /// </summary>
        public IEnumerator DownloadTaleContent(TaleSummary tale,
            Action<int, int> onProgress, Action<string> onError = null)
        {
            _bytesDownloaded = 0;

            if (BundledTaleLoader.IsBundled(tale.id))
            {
                onProgress?.Invoke(1, 1);
                yield break;
            }

            var id = tale.id;
            var lang = Loc.Lang;

            // Text for the CURRENT language only. Fetching every translation made sense
            // with three languages; with ten it is ten requests per tale for text the
            // reader almost never switches to. After a language switch the detail screen
            // fetches the new translation itself, and "download tale" tops it up here.
            // Runs even if illustrations are already downloaded. Only cache a language the
            // server actually served (d.lang == lang): otherwise its default-language fallback
            // would be saved under the wrong key and read in the wrong language offline.
            if (!AssetCache.Exists(AssetCache.TaleTextKey(id, lang)))
            {
                yield return _api.Get($"/api/tales/{id}?lang={lang}",
                    json =>
                    {
                        var d = JsonConvert.DeserializeObject<TaleDetail>(json);
                        if (d != null && d.lang == lang)
                            AssetCache.SaveTaleText(id, lang,
                                System.Text.Encoding.UTF8.GetBytes(json));
                    },
                    _ => { });
            }

            if (AssetCache.IsTaleDownloaded(id))
            {
                onProgress?.Invoke(1, 1);
                yield break;
            }

            TaleDetail detail = null;
            yield return _api.Get($"/api/tales/{id}?lang={lang}",
                json => detail = JsonConvert.DeserializeObject<TaleDetail>(json),
                e => onError?.Invoke(e));

            if (detail == null) yield break;

            // A translation may omit totalPages or disagree with its own page list —
            // trust whichever is larger so no illustration is silently skipped.
            int pageCount = Mathf.Max(detail.totalPages, detail.pages?.Length ?? 0);
            if (pageCount == 0) yield break;

            // Progress: plain pages = 1 download, gendered pages = 2 (boy + girl).
            // (pageCount - g) + 2*g == pageCount + g, so the formula is unchanged.
            int genderedCount = detail.genderedPages?.Length ?? 0;
            int totalSteps = pageCount + genderedCount;
            if (tale.hasDefaultNarration) totalSteps += pageCount;
            int step = 0;
            bool complete = true;

            // Illustrations — gender-agnostic: download BOTH variants for gendered
            // pages so switching the child's gender later is handled purely on-client
            // from cache. Gendered pages have no plain page_N on the server, so we
            // request boy/girl directly instead.
            for (int p = 0; p < pageCount; p++)
            {
                bool isGendered = detail.genderedPages != null
                    && Array.IndexOf(detail.genderedPages, p) >= 0;

                if (isGendered)
                {
                    yield return DownloadImage(
                        $"/api/tales/{id}/illustration/{p}?gender=boy",
                        AssetCache.IllustrationKey(id, p, "boy"), onError,
                        $"Illustrations/{id}/page_{p}_boy");
                    onProgress?.Invoke(++step, totalSteps);

                    yield return DownloadImage(
                        $"/api/tales/{id}/illustration/{p}?gender=girl",
                        AssetCache.IllustrationKey(id, p, "girl"), onError,
                        $"Illustrations/{id}/page_{p}_girl");
                    onProgress?.Invoke(++step, totalSteps);
                }
                else
                {
                    yield return DownloadImage(
                        $"/api/tales/{id}/illustration/{p}",
                        AssetCache.IllustrationKey(id, p), onError,
                        $"Illustrations/{id}/page_{p}");
                    onProgress?.Invoke(++step, totalSteps);
                }

                if (!HasAnyVariant(id, p)) complete = false;
            }

            // Default narration. Ask first WHICH language the server actually has:
            // a silent fallback to another one would be cached under our lang key
            // and then read the wrong language over the text forever.
            if (tale.hasDefaultNarration)
            {
                DefaultNarrationInfo info = null;
                yield return _api.Get($"/api/tales/{id}/default-narration?lang={lang}",
                    json => info = JsonConvert.DeserializeObject<DefaultNarrationInfo>(json),
                    _ => { });
                bool langOk = info != null && info.available
                    && (string.IsNullOrEmpty(info.lang) || info.lang == lang);

                for (int p = 0; p < pageCount; p++)
                {
                    if (langOk)
                        yield return DownloadFile(
                            $"/api/tales/{id}/default-narration/{p}?lang={lang}",
                            AssetCache.NarrationKey(id, lang, p), onError);
                    onProgress?.Invoke(++step, totalSteps);
                }
            }

            // Only mark ready when every page really got an illustration. A partial
            // download (server 404 for a page, connection dropped) would otherwise be
            // treated as complete forever and those slides would stay blank — instead
            // the next open re-runs the download and tops up only what's missing.
            if (complete) AssetCache.MarkTaleReady(id);
        }

        /// <summary>True if page p has an illustration in any variant — plain, boy or girl.</summary>
        private static bool HasAnyVariant(string taleId, int page)
        {
            if (AssetCache.Exists(AssetCache.IllustrationKey(taleId, page))
                || AssetCache.Exists(AssetCache.IllustrationKey(taleId, page, "boy"))
                || AssetCache.Exists(AssetCache.IllustrationKey(taleId, page, "girl")))
                return true;

            var res = $"Illustrations/{taleId}/page_{page}";
            return Resources.Load<Sprite>(res) != null
                || Resources.Load<Sprite>($"{res}_boy") != null
                || Resources.Load<Sprite>($"{res}_girl") != null;
        }

        /// Downloads default narration audio for a tale.
        /// Uses /default-narration?lang= to get available pages, then downloads each.
        public IEnumerator DownloadDefaultNarration(string taleId, string lang,
            Action<int, int> onProgress, Action<string> onError = null)
        {
            // Check which pages are available
            DefaultNarrationInfo info = null;
            yield return _api.Get($"/api/tales/{taleId}/default-narration?lang={lang}",
                json => info = JsonConvert.DeserializeObject<DefaultNarrationInfo>(json),
                e => { } /* RELEASE: Debug.LogWarning($"[Download] narration info {taleId}: {e}") */);

            if (info == null || !info.available || info.pages == null || info.pages.Length == 0)
                yield break;

            // Never file another language's audio under this one's key.
            if (!string.IsNullOrEmpty(info.lang) && info.lang != lang)
                yield break;

            for (int i = 0; i < info.pages.Length; i++)
            {
                int page = info.pages[i];
                yield return DownloadFile(
                    $"/api/tales/{taleId}/default-narration/{page}?lang={lang}",
                    AssetCache.NarrationKey(taleId, lang, page), onError);
                onProgress?.Invoke(i + 1, info.pages.Length);
            }
        }

        /// Downloads image as raw bytes and saves directly to cache.
        /// Server sends optimized JPEG — no re-encoding needed.
        private IEnumerator DownloadImage(string endpoint, string key,
            Action<string> onError, string resourcePath = null)
        {
            if (AssetCache.Exists(key)) yield break;
            if (resourcePath != null && Resources.Load<Sprite>(resourcePath) != null)
                yield break;

            yield return _api.GetBytes(endpoint,
                bytes =>
                {
                    AssetCache.Save(key, bytes);
                    _bytesDownloaded += bytes.Length;
                },
                e =>
                {
                    // RELEASE: Debug.LogWarning($"[Download] {endpoint}: {e}");
                    onError?.Invoke(e);
                });
        }

        private IEnumerator DownloadFile(string endpoint, string key,
            Action<string> onError)
        {
            if (AssetCache.Exists(key)) yield break;

            yield return _api.GetBytes(endpoint,
                bytes =>
                {
                    AssetCache.Save(key, bytes);
                    _bytesDownloaded += bytes.Length;
                },
                e =>
                {
                    // RELEASE: Debug.LogWarning($"[Download] {endpoint}: {e}");
                    onError?.Invoke(e);
                });
        }
    }
}
