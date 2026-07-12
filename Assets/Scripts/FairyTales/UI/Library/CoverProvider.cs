using System.Collections.Generic;
using FairyTales.Api;
using FairyTales.Cache;
using UnityEngine;

namespace FairyTales.UI.Library
{
    public static class CoverProvider
    {
        private static readonly Dictionary<string, Sprite> Cache = new();
        private static Sprite _default;

        /// <summary>Returns cached sprite or null. Never blocks on Resources.Load.</summary>
        public static Sprite GetCached(string taleId)
        {
            return Cache.TryGetValue(taleId, out var s) ? s : null;
        }

        /// <summary>Async load with callback. Checks memory cache → Resources → disk →
        /// StreamingAssets → server (if <paramref name="api"/> given) → default.</summary>
        public static System.Collections.IEnumerator GetAsync(
            string taleId, System.Action<Sprite> cb, ApiClient api = null)
        {
            if (Cache.TryGetValue(taleId, out var cached))
            {
                cb?.Invoke(cached);
                yield break;
            }

            // Resources (async)
            var req = Resources.LoadAsync<Sprite>($"Covers/{taleId}");
            yield return req;
            var sprite = req.asset as Sprite;
            if (sprite != null)
            {
                Cache[taleId] = sprite;
                cb?.Invoke(sprite);
                yield break;
            }

            // Disk cache (async file I/O)
            yield return AssetCache.LoadSpriteAsync(AssetCache.CoverKey(taleId), s => sprite = s);
            if (sprite != null)
            {
                Cache[taleId] = sprite;
                cb?.Invoke(sprite);
                yield break;
            }

            // StreamingAssets
            if (BundledTaleLoader.IsBundled(taleId))
            {
                yield return BundledTaleLoader.LoadCover(taleId, s => sprite = s);
                if (sprite != null)
                {
                    Cache[taleId] = sprite;
                    cb?.Invoke(sprite);
                    yield break;
                }
            }

            // Server: covers not bundled/downloaded (e.g. coming-soon tales) come from
            // the API. The server mislabels PNG bytes as image/jpeg, but Texture2D
            // detects the format from content, so it loads fine.
            if (api != null)
            {
                Sprite fromServer = null;
                yield return DownloadFromServer(taleId, api, s => fromServer = s);
                if (fromServer != null)
                {
                    Cache[taleId] = fromServer;
                    cb?.Invoke(fromServer);
                    yield break;
                }
            }

            // Fallback: default cover
            cb?.Invoke(GetDefault());
        }

        private static System.Collections.IEnumerator DownloadFromServer(
            string taleId, ApiClient api, System.Action<Sprite> cb)
        {
            byte[] data = null;
            yield return api.GetBytes($"/api/tales/{taleId}/cover",
                b => data = b, _ => { });

            if (data == null || data.Length < 8)
            {
                cb?.Invoke(null);
                yield break;
            }

            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(data))
            {
                cb?.Invoke(null);
                yield break;
            }

            // Persist to disk so it shows offline next time.
            AssetCache.Save(AssetCache.CoverKey(taleId), data);

            cb?.Invoke(Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f)));
        }

        private static Sprite GetDefault()
        {
            if (_default == null)
                _default = Resources.Load<Sprite>("Covers/default");
            return _default;
        }
    }
}
