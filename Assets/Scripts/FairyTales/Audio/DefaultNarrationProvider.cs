using System.Collections;
using System.Collections.Generic;
using FairyTales.Cache;
using FairyTales.UI.Core;
using UnityEngine;

namespace FairyTales.Audio
{
    public class DefaultNarrationProvider
    {
        // Cache Resource clips to avoid repeated Resources.Load
        private static readonly Dictionary<string, AudioClip> ClipCache = new();

        /// <summary>
        /// Returns cached audio bytes for default narration, or null.
        /// Note: uses sync File.ReadAllBytes — call from coroutine context
        /// so the next frame can absorb the cost.
        /// </summary>
        public byte[] GetPageBytes(string taleId, int page)
        {
            return AssetCache.Load(AssetCache.NarrationKey(taleId, Loc.Lang, page));
        }

        /// <summary>
        /// Async load from Resources (non-blocking).
        /// </summary>
        public IEnumerator GetPageAsync(string taleId, int page, System.Action<AudioClip> cb)
        {
            var path = $"Audio/Default/{taleId}/page_{page}";
            if (ClipCache.TryGetValue(path, out var cached))
            {
                cb?.Invoke(cached);
                yield break;
            }

            var req = Resources.LoadAsync<AudioClip>(path);
            yield return req;
            var clip = req.asset as AudioClip;
            if (clip != null) ClipCache[path] = clip;
            cb?.Invoke(clip);
        }

        /// <summary>
        /// Sync method — kept for simple checks. Prefer GetPageAsync.
        /// </summary>
        public AudioClip GetPage(string taleId, int page)
        {
            var path = $"Audio/Default/{taleId}/page_{page}";
            if (ClipCache.TryGetValue(path, out var cached)) return cached;
            var clip = Resources.Load<AudioClip>(path);
            if (clip != null) ClipCache[path] = clip;
            return clip;
        }

        public bool HasNarration(string taleId, int page)
        {
            return AssetCache.Exists(AssetCache.NarrationKey(taleId, Loc.Lang, page))
                || GetPage(taleId, page) != null;
        }

        public bool HasAnyNarration(string taleId)
        {
            return HasNarration(taleId, 0);
        }
    }
}
