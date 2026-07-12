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

            // Text for ALL translations — cache each language's JSON so switching
            // language later works offline and shows the correct translation. Runs
            // even if illustrations are already downloaded, so "download tale" always
            // tops up every language. Only cache a language the server actually served
            // (d.lang == l): otherwise its default-language fallback would be saved
            // under the wrong key and the tale would read in the wrong language offline.
            foreach (var l in Loc.AllLangs)
            {
                if (AssetCache.Exists(AssetCache.TaleTextKey(id, l))) continue;
                yield return _api.Get($"/api/tales/{id}?lang={l}",
                    json =>
                    {
                        var d = JsonConvert.DeserializeObject<TaleDetail>(json);
                        if (d != null && d.lang == l)
                            AssetCache.Save(AssetCache.TaleTextKey(id, l),
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

            // Progress: plain pages = 1 download, gendered pages = 2 (boy + girl).
            // (totalPages - g) + 2*g == totalPages + g, so the formula is unchanged.
            int genderedCount = detail.genderedPages?.Length ?? 0;
            int totalSteps = detail.totalPages + genderedCount;
            if (tale.hasDefaultNarration) totalSteps += detail.totalPages;
            int step = 0;

            // Illustrations — gender-agnostic: download BOTH variants for gendered
            // pages so switching the child's gender later is handled purely on-client
            // from cache. Gendered pages have no plain page_N on the server, so we
            // request boy/girl directly instead.
            for (int p = 0; p < detail.totalPages; p++)
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
            }

            // Default narration
            if (tale.hasDefaultNarration)
            {
                for (int p = 0; p < detail.totalPages; p++)
                {
                    yield return DownloadFile(
                        $"/api/tales/{id}/default-narration/{p}?lang={lang}",
                        AssetCache.NarrationKey(id, lang, p), onError);
                    onProgress?.Invoke(++step, totalSteps);
                }
            }

            AssetCache.MarkTaleReady(id);
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
