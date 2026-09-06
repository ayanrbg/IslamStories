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
        /// Pass the language the page TEXT is in — it is not always Loc.Lang, and
        /// reading a different key than the one the caller writes would replay the
        /// wrong language forever.
        /// Note: uses sync File.ReadAllBytes — call from coroutine context
        /// so the next frame can absorb the cost.
        /// </summary>
        public byte[] GetPageBytes(string taleId, int page, string lang = null)
        {
            return AssetCache.LoadAudio(AssetCache.NarrationKey(taleId, lang ?? Loc.Lang, page));
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

        public bool HasNarration(string taleId, int page, string lang = null)
        {
            return AssetCache.HasAudio(AssetCache.NarrationKey(taleId, lang ?? Loc.Lang, page))
                || GetPage(taleId, page) != null;
        }

        public bool HasAnyNarration(string taleId, string lang = null)
        {
            return HasNarration(taleId, 0, lang);
        }
    }
}
