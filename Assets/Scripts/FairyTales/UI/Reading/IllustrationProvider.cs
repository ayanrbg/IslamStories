using System;
using System.Collections;
using System.Collections.Generic;
using FairyTales.Api;
using FairyTales.Cache;
using UnityEngine;

namespace FairyTales.UI.Reading
{
    public static class IllustrationProvider
    {
        private static readonly Dictionary<string, Sprite> Cache = new();
        private static ApiClient _api;

        private static ApiClient Api =>
            _api != null ? _api : (_api = UnityEngine.Object.FindAnyObjectByType<ApiClient>());

        private static string Key(string taleId, int page) => $"{taleId}/{page}";
        private static string Key(string taleId, int page, string gender)
            => $"{taleId}/{page}_{gender}";

        private static bool IsGendered(int page, int[] genderedPages)
        {
            if (genderedPages == null) return false;
            return Array.IndexOf(genderedPages, page) >= 0;
        }

        /// <summary>Maps profile gender ("male"/"female") to illustration param ("boy"/"girl").</summary>
        private static string GenderParam(string profileGender)
            => profileGender == "female" ? "girl" : "boy";

        /// <summary>Returns cached sprite or null. Never blocks.</summary>
        public static Sprite GetCached(string taleId, int page,
            string gender = null, int[] genderedPages = null)
        {
            if (IsGendered(page, genderedPages) && gender != null)
            {
                var gk = Key(taleId, page, GenderParam(gender));
                if (Cache.TryGetValue(gk, out var gs)) return gs;
            }
            return Cache.TryGetValue(Key(taleId, page), out var s) ? s : null;
        }

        /// <summary>
        /// Variants to try, in order — null means the plain "page_N" file.
        /// Whether a page is gendered comes from the current language's JSON, but
        /// the files on disk were fetched using whichever translation drove the
        /// download. A translation whose genderedPages list disagrees must not
        /// leave the page blank, so both layouts are always attempted.
        /// </summary>
        private static string[] Variants(bool gendered, string gp)
            => gendered ? new[] { gp, null } : new[] { null, gp };

        private static string DiskKey(string taleId, int page, string variant)
            => variant == null
                ? AssetCache.IllustrationKey(taleId, page)
                : AssetCache.IllustrationKey(taleId, page, variant);

        /// <summary>Async load: memory → AssetCache → Resources → StreamingAssets.</summary>
        public static IEnumerator GetPageAsync(
            string taleId, int page, Action<Sprite> cb,
            string gender = null, int[] genderedPages = null)
        {
            bool gendered = IsGendered(page, genderedPages) && gender != null;
            string gp = GenderParam(gender ?? PlayerPrefs.GetString("ft_gender", "male"));
            var k = gendered ? Key(taleId, page, gp) : Key(taleId, page);

            if (Cache.TryGetValue(k, out var cached))
            {
                cb?.Invoke(cached);
                yield break;
            }

            var variants = Variants(gendered, gp);
            Sprite sprite = null;

            // AssetCache (disk)
            foreach (var v in variants)
            {
                yield return AssetCache.LoadSpriteAsync(
                    DiskKey(taleId, page, v), s => sprite = s);
                if (sprite != null) break;
            }
            if (sprite != null)
            {
                Cache[k] = sprite;
                cb?.Invoke(sprite);
                yield break;
            }

            // Resources
            foreach (var v in variants)
            {
                var path = v == null
                    ? $"Illustrations/{taleId}/page_{page}"
                    : $"Illustrations/{taleId}/page_{page}_{v}";
                var req = Resources.LoadAsync<Sprite>(path);
                yield return req;
                sprite = req.asset as Sprite;
                if (sprite != null) break;
            }
            if (sprite != null)
            {
                Cache[k] = sprite;
                cb?.Invoke(sprite);
                yield break;
            }

            // StreamingAssets
            if (BundledTaleLoader.IsBundled(taleId))
            {
                foreach (var v in variants)
                {
                    var file = v == null
                        ? $"page_{page}.jpg"
                        : $"page_{page}_{v}.jpg";
                    yield return BundledTaleLoader.LoadSprite(taleId, file, s => sprite = s);
                    if (sprite != null) break;
                }
                if (sprite != null) Cache[k] = sprite;
                cb?.Invoke(sprite);
            }
            else
            {
                // Last resort: page missing from local cache (gender switched,
                // download interrupted, or gendered page with no plain fallback
                // on the server) — fetch it live and cache it. Self-heals.
                yield return DownloadFromServer(taleId, page, variants, k, cb);
            }
        }

        /// <summary>On-demand server fetch for an illustration absent from local cache.</summary>
        private static IEnumerator DownloadFromServer(
            string taleId, int page, string[] variants, string memKey,
            Action<Sprite> cb)
        {
            var api = Api;
            if (api == null) { cb?.Invoke(null); yield break; }

            foreach (var v in variants)
            {
                var endpoint = v == null
                    ? $"/api/tales/{taleId}/illustration/{page}"
                    : $"/api/tales/{taleId}/illustration/{page}?gender={v}";

                byte[] data = null;
                yield return api.GetBytes(endpoint, b => data = b, _ => { });
                if (data == null || data.Length < 8) continue;

                var diskKey = DiskKey(taleId, page, v);
                AssetCache.Save(diskKey, data);

                Sprite sprite = null;
                yield return AssetCache.LoadSpriteAsync(diskKey, s => sprite = s);
                if (sprite == null) continue;

                Cache[memKey] = sprite;
                cb?.Invoke(sprite);
                yield break;
            }

            cb?.Invoke(null);
        }

        /// <summary>Alias for thumbnails (no gender — always generic).</summary>
        public static IEnumerator GetThumbnailAsync(
            string taleId, int page, Action<Sprite> cb)
        {
            yield return GetPageAsync(taleId, page, cb);
        }
    }
}
