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

        /// <summary>Async load: memory → AssetCache → Resources → StreamingAssets.</summary>
        public static IEnumerator GetPageAsync(
            string taleId, int page, Action<Sprite> cb,
            string gender = null, int[] genderedPages = null)
        {
            bool gendered = IsGendered(page, genderedPages) && gender != null;
            string gp = gendered ? GenderParam(gender) : null;
            var k = gendered ? Key(taleId, page, gp) : Key(taleId, page);

            if (Cache.TryGetValue(k, out var cached))
            {
                cb?.Invoke(cached);
                yield break;
            }

            // AssetCache (disk — gendered key first, then fallback)
            Sprite sprite = null;
            if (gendered)
            {
                yield return AssetCache.LoadSpriteAsync(
                    AssetCache.IllustrationKey(taleId, page, gp), s => sprite = s);
            }
            if (sprite == null)
            {
                yield return AssetCache.LoadSpriteAsync(
                    AssetCache.IllustrationKey(taleId, page), s => sprite = s);
            }
            if (sprite != null)
            {
                Cache[k] = sprite;
                cb?.Invoke(sprite);
                yield break;
            }

            // Resources (gendered first, then fallback)
            if (gendered)
            {
                var req = Resources.LoadAsync<Sprite>(
                    $"Illustrations/{taleId}/page_{page}_{gp}");
                yield return req;
                sprite = req.asset as Sprite;
            }
            if (sprite == null)
            {
                var req = Resources.LoadAsync<Sprite>(
                    $"Illustrations/{taleId}/page_{page}");
                yield return req;
                sprite = req.asset as Sprite;
            }
            if (sprite != null)
            {
                Cache[k] = sprite;
                cb?.Invoke(sprite);
                yield break;
            }

            // StreamingAssets (gendered with fallback inside)
            if (BundledTaleLoader.IsBundled(taleId))
            {
                if (gendered)
                {
                    yield return BundledTaleLoader.LoadPageGendered(
                        taleId, page, gp, s =>
                        {
                            if (s != null) Cache[k] = s;
                            cb?.Invoke(s);
                        });
                }
                else
                {
                    yield return BundledTaleLoader.LoadPage(taleId, page, s =>
                    {
                        if (s != null) Cache[k] = s;
                        cb?.Invoke(s);
                    });
                }
            }
            else
            {
                // Last resort: page missing from local cache (gender switched,
                // download interrupted, or gendered page with no plain fallback
                // on the server) — fetch it live and cache it. Self-heals.
                yield return DownloadFromServer(taleId, page, gendered, gp, k, cb);
            }
        }

        /// <summary>On-demand server fetch for an illustration absent from local cache.</summary>
        private static IEnumerator DownloadFromServer(
            string taleId, int page, bool gendered, string gp, string memKey,
            Action<Sprite> cb)
        {
            var api = Api;
            if (api == null) { cb?.Invoke(null); yield break; }

            var endpoint = gendered
                ? $"/api/tales/{taleId}/illustration/{page}?gender={gp}"
                : $"/api/tales/{taleId}/illustration/{page}";
            var diskKey = gendered
                ? AssetCache.IllustrationKey(taleId, page, gp)
                : AssetCache.IllustrationKey(taleId, page);

            byte[] data = null;
            yield return api.GetBytes(endpoint, b => data = b, _ => { });
            if (data == null || data.Length < 8) { cb?.Invoke(null); yield break; }

            AssetCache.Save(diskKey, data);
            Sprite sprite = null;
            yield return AssetCache.LoadSpriteAsync(diskKey, s => sprite = s);
            if (sprite != null) Cache[memKey] = sprite;
            cb?.Invoke(sprite);
        }

        /// <summary>Alias for thumbnails (no gender — always generic).</summary>
        public static IEnumerator GetThumbnailAsync(
            string taleId, int page, Action<Sprite> cb)
        {
            yield return GetPageAsync(taleId, page, cb);
        }
    }
}
