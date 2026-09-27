using System;
using System.Collections;
using FairyTales.Api;
using FairyTales.Cache;
using UnityEngine;
using UnityEngine.Rendering;

namespace FairyTales.UI.Reading
{
    public static class IllustrationProvider
    {
        // A page is up to 2048×1143 — 7–12 MB as a texture. This used to be an unbounded
        // dictionary, so every page ever opened stayed in memory for the session. Eight
        // covers what can be on screen at once (detail card, reading page and its
        // cross-fade buffer, the prefetched neighbours) with room to page back.
        private static readonly SpriteLru Pages = new(8);

        // Table-of-contents cards get their own small copies instead of full pages.
        private static readonly SpriteLru Thumbs = new(24);
        private const int ThumbWidth = 400;

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
                if (Pages.TryGet(gk, out var gs)) return gs;
            }
            return Pages.TryGet(Key(taleId, page), out var s) ? s : null;
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

        /// <summary>Async load: memory → AssetCache → Resources → StreamingAssets → server.</summary>
        public static IEnumerator GetPageAsync(
            string taleId, int page, Action<Sprite> cb,
            string gender = null, int[] genderedPages = null)
        {
            bool gendered = IsGendered(page, genderedPages) && gender != null;
            string gp = GenderParam(gender ?? PlayerPrefs.GetString("ft_gender", "male"));
            var k = gendered ? Key(taleId, page, gp) : Key(taleId, page);

            if (Pages.TryGet(k, out var cached))
            {
                cb?.Invoke(cached);
                yield break;
            }

            Sprite sprite = null;
            bool owned = false;
            yield return LoadPage(taleId, page, Variants(gendered, gp), (s, o) => { sprite = s; owned = o; });
            cb?.Invoke(Pages.Put(k, sprite, owned));
        }

        /// <summary>
        /// A small copy of a page for the table of contents. Made from the page already
        /// in memory when there is one; otherwise the page is loaded only to be shrunk
        /// and freed straight away, so opening the contents no longer pulls a dozen
        /// full-size pages into memory.
        /// </summary>
        public static IEnumerator GetThumbnailAsync(
            string taleId, int page, Action<Sprite> cb,
            string gender = null, int[] genderedPages = null)
        {
            bool gendered = IsGendered(page, genderedPages) && gender != null;
            string gp = GenderParam(gender ?? PlayerPrefs.GetString("ft_gender", "male"));
            var k = gendered ? Key(taleId, page, gp) : Key(taleId, page);

            if (Thumbs.TryGet(k, out var thumb))
            {
                cb?.Invoke(thumb);
                yield break;
            }

            bool inMemory = Pages.TryGet(k, out var full);
            bool owned = false;
            if (!inMemory)
                yield return LoadPage(taleId, page, Variants(gendered, gp), (s, o) => { full = s; owned = o; });

            if (full == null)
            {
                cb?.Invoke(null);
                yield break;
            }

            var small = Shrink(full);
            if (small == null)
            {
                // No GPU copy on this device: fall back to the page itself, kept by the
                // bounded page cache like any other.
                cb?.Invoke(inMemory ? full : Pages.Put(k, full, owned));
                yield break;
            }

            if (!inMemory && owned) Free(full);
            cb?.Invoke(Thumbs.Put(k, small, true));
        }

        /// <summary>
        /// Loads one page from the first source that has it, without caching it.
        /// <c>owned</c> tells whether the texture was created here and may be destroyed.
        /// </summary>
        private static IEnumerator LoadPage(string taleId, int page, string[] variants,
            Action<Sprite, bool> done)
        {
            Sprite sprite = null;

            // AssetCache (disk)
            foreach (var v in variants)
            {
                yield return AssetCache.LoadSpriteAsync(
                    DiskKey(taleId, page, v), s => sprite = s, cache: false);
                if (sprite != null)
                {
                    done(sprite, true);
                    yield break;
                }
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
                if (sprite != null)
                {
                    done(sprite, false);
                    yield break;
                }
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
                done(sprite, sprite != null);
                yield break;
            }

            // Last resort: page missing from local cache (gender switched,
            // download interrupted, or gendered page with no plain fallback
            // on the server) — fetch it live and cache it on disk. Self-heals.
            yield return DownloadFromServer(taleId, page, variants, s => sprite = s);
            done(sprite, sprite != null);
        }

        /// <summary>On-demand server fetch for an illustration absent from local cache.</summary>
        private static IEnumerator DownloadFromServer(
            string taleId, int page, string[] variants, Action<Sprite> cb)
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
                yield return AssetCache.LoadSpriteAsync(diskKey, s => sprite = s, cache: false);
                if (sprite == null) continue;

                cb?.Invoke(sprite);
                yield break;
            }

            cb?.Invoke(null);
        }

        /// <summary>
        /// Scales a page down on the GPU. CopyTexture keeps it GPU-side — no readback
        /// stall. Null where the device can't copy a render texture into a texture.
        /// </summary>
        private static Sprite Shrink(Sprite source)
        {
            var src = source.texture;
            if (src == null) return null;
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) == 0) return null;

            int w = Mathf.Min(ThumbWidth, src.width);
            int h = Mathf.Max(1, Mathf.RoundToInt(src.height * (w / (float)src.width)));

            // Loaded pages carry a mip chain, so one blit samples the right level and the
            // small copy comes out smooth.
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Graphics.CopyTexture(rt, tex);
            RenderTexture.ReleaseTemporary(rt);

            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
        }

        private static void Free(Sprite sprite)
        {
            var tex = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }
}
