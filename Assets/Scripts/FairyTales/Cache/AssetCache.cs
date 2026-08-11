using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FairyTales.Cache
{
    public static class AssetCache
    {
        private const int CacheVersion = 3; // v3: images stored as JPEG (was PNG)
        // Bumped when the set of files a "downloaded" tale must contain changes.
        // v2: both gender variants (boy+girl) are cached for gendered pages.
        private const int IllustrationsVersion = 2;
        private static string Root => Path.Combine(Application.persistentDataPath, "cache");
        private static readonly Dictionary<string, Sprite> SpriteCache = new();
        // Lazy per-tale resource checks (avoids Resources.LoadAll at startup)
        private static readonly Dictionary<string, bool> ResourceCheckCache = new();
        private static readonly Dictionary<string, bool> IllustrationCheckCache = new();

        /// Call once at startup to wipe stale cache after format change.
        public static void MigrateIfNeeded()
        {
            var key = "cache_version";
            if (PlayerPrefs.GetInt(key, 0) >= CacheVersion) return;
            ClearAll();
            PlayerPrefs.SetInt(key, CacheVersion);
            PlayerPrefs.Save();
        }

        /// <summary>No-op — kept for API compatibility. Resource index is now lazy.</summary>
        public static void InitResourceIndex() { }

        /// <summary>Resource checks are lazy per-tale; load the bundled-tale set for IsBundled.</summary>
        public static IEnumerator InitResourceIndexAsync()
        {
            yield return BundledTaleLoader.InitBundledSet();
        }

        public static bool HasResourceCover(string taleId)
        {
            if (ResourceCheckCache.TryGetValue(taleId, out var cached))
                return cached;
            // Probe once per tale — Resources.Load returns null fast for missing assets
            var found = Resources.Load<Sprite>($"Covers/{taleId}") != null;
            ResourceCheckCache[taleId] = found;
            return found;
        }

        public static bool HasResourceIllustrations(string taleId)
        {
            // A bundled cover does NOT imply bundled illustrations — most tales ship
            // only a thumbnail cover for the library. Probe the first page directly.
            if (IllustrationCheckCache.TryGetValue(taleId, out var cached))
                return cached;
            var found = Resources.Load<Sprite>($"Illustrations/{taleId}/page_0") != null;
            IllustrationCheckCache[taleId] = found;
            return found;
        }

        // ── Parent narration (real parent voice, recorded per page) ──
        // Replaces the old AI voice-clone sample: the parent reads each page aloud
        // and we store one WAV per (tale, language, page). Played back verbatim
        // during reading — no server, no cloning.
        public static string ParentNarrationKey(string taleId, string lang, int page)
            => $"parent-narration/{taleId}/{lang}/page_{page}.wav";

        public static void SaveParentNarration(string taleId, string lang, int page, byte[] wav)
            => Save(ParentNarrationKey(taleId, lang, page), wav);

        public static byte[] LoadParentNarration(string taleId, string lang, int page)
            => Load(ParentNarrationKey(taleId, lang, page));

        public static bool HasParentNarrationPage(string taleId, string lang, int page)
            => Exists(ParentNarrationKey(taleId, lang, page));

        /// <summary>True if at least one page has been recorded for this tale+language.</summary>
        public static bool HasAnyParentNarration(string taleId, string lang)
        {
            // No tale/language means nothing was ever recorded — Path.Combine would throw.
            if (string.IsNullOrEmpty(taleId) || string.IsNullOrEmpty(lang)) return false;

            var dir = Path.Combine(Root, "parent-narration", taleId, lang);
            return Directory.Exists(dir) && Directory.GetFiles(dir, "page_*.wav").Length > 0;
        }

        /// <summary>Deletes all recorded parent pages for a tale+language.</summary>
        public static void ClearParentNarration(string taleId, string lang)
        {
            if (string.IsNullOrEmpty(taleId) || string.IsNullOrEmpty(lang)) return;

            var dir = Path.Combine(Root, "parent-narration", taleId, lang);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // ── Keys ─────────────────────────────────────────────
        public static string CoverKey(string taleId)
            => $"covers/{taleId}";

        public static string IllustrationKey(string taleId, int page)
            => $"illustrations/{taleId}/page_{page}";

        public static string IllustrationKey(string taleId, int page, string gender)
            => $"illustrations/{taleId}/page_{page}_{gender}";

        public static string TaleTextKey(string taleId, string lang)
            => $"tales-text/{taleId}/{lang}.json";

        public static string NarrationKey(string taleId, string lang, int page)
            => $"narration/{taleId}/{lang}/page_{page}.mp3";

        public static string AiNarrationKey(string taleId, string lang, int page)
            => $"ai-narration/{taleId}/{lang}/page_{page}.mp3";

        private static string AiNarrationMetaKey(string taleId)
            => $"ai-narration/{taleId}/meta.json";

        /// <summary>
        /// Saves the voice/child params used for AI narration so we can detect changes.
        /// </summary>
        public static void SaveAiNarrationMeta(string taleId, string voice,
            string narratorGender, string childName, string childGender)
        {
            var json = JsonUtility.ToJson(new AiNarrationMeta
            {
                voice = voice ?? "",
                narratorGender = narratorGender ?? "",
                childName = childName ?? "",
                childGender = childGender ?? ""
            });
            Save(AiNarrationMetaKey(taleId), System.Text.Encoding.UTF8.GetBytes(json));
        }

        /// <summary>
        /// Returns true if AI narration exists AND was generated with matching params.
        /// </summary>
        public static bool IsAiNarrationCached(string taleId, string lang,
            string voice, string narratorGender, string childName, string childGender)
        {
            // Must have at least page 0
            if (!Exists(AiNarrationKey(taleId, lang, 0)))
                return false;

            var metaBytes = Load(AiNarrationMetaKey(taleId));
            if (metaBytes == null) return false;

            var meta = JsonUtility.FromJson<AiNarrationMeta>(
                System.Text.Encoding.UTF8.GetString(metaBytes));

            return meta.voice == (voice ?? "")
                && meta.narratorGender == (narratorGender ?? "")
                && meta.childName == (childName ?? "")
                && meta.childGender == (childGender ?? "");
        }

        /// <summary>
        /// Deletes all cached AI narration files for a tale.
        /// </summary>
        public static void ClearAiNarration(string taleId)
        {
            var dir = Path.Combine(Root, "ai-narration", taleId);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        [Serializable]
        private struct AiNarrationMeta
        {
            public string voice;
            public string narratorGender;
            public string childName;
            public string childGender;
        }

        // ── Queries ──────────────────────────────────────────
        public static bool Exists(string key)
            => File.Exists(GetPath(key));

        public static string GetPath(string key)
            => Path.Combine(Root, key);

        public static string TaleReadyKey(string taleId)
            => $"tales/{taleId}.ready";

        public static bool IsCoverDownloaded(string taleId)
            => Exists(CoverKey(taleId)) || HasResourceCover(taleId);

        public static bool IsTaleDownloaded(string taleId)
        {
            // Available in bundled Resources — no download needed
            if (HasResourceIllustrations(taleId))
                return true;

            // Available in StreamingAssets — no download needed
            if (BundledTaleLoader.IsBundled(taleId))
                return true;

            var ready = Load(TaleReadyKey(taleId));
            if (ready == null) return false;

            // Downloaded by an older build (v1 = only the current gender's variants).
            // Treat as not-ready so the normal download flow tops up the missing
            // second-gender variants — DownloadImage skips files already on disk,
            // so only what's missing is fetched.
            if (ready.Length < 1 || ready[0] < IllustrationsVersion)
                return false;

            // Verify page 0 exists in ANY variant. Tales whose first page is
            // gendered (e.g. baursak) have no plain page_0 on the server — only
            // page_0_boy / page_0_girl — so checking plain alone is wrong.
            if (!Exists(IllustrationKey(taleId, 0))
                && !Exists(IllustrationKey(taleId, 0, "boy"))
                && !Exists(IllustrationKey(taleId, 0, "girl")))
            {
                Delete(TaleReadyKey(taleId));
                // RELEASE: Debug.LogWarning($"[AssetCache] Stale .ready for {taleId}, re-download needed");
                return false;
            }
            return true;
        }

        public static void MarkTaleReady(string taleId)
            => Save(TaleReadyKey(taleId), new byte[] { IllustrationsVersion });

        // ── Read / Write ─────────────────────────────────────
        public static void Save(string key, byte[] data)
        {
            var path = GetPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
        }

        public static byte[] Load(string key)
        {
            var path = GetPath(key);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>Async sprite load via UnityWebRequest (file I/O off main thread).</summary>
        public static IEnumerator LoadSpriteAsync(string key, Action<Sprite> cb)
        {
            if (SpriteCache.TryGetValue(key, out var cached) && cached != null)
            {
                cb?.Invoke(cached);
                yield break;
            }

            var path = GetPath(key);
            if (!File.Exists(path))
            {
                cb?.Invoke(null);
                yield break;
            }

            var url = "file:///" + path.Replace("\\", "/");
            using var req = UnityWebRequestTexture.GetTexture(url, nonReadable: true);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                cb?.Invoke(null);
                yield break;
            }

            var tex = DownloadHandlerTexture.GetContent(req);
            var sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            SpriteCache[key] = sprite;
            cb?.Invoke(sprite);
        }

        [Obsolete("Use LoadSpriteAsync for non-blocking load")]
        public static Sprite LoadSprite(string key)
        {
            if (SpriteCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var bytes = Load(key);
            if (bytes == null || bytes.Length < 8) return null;

            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(bytes))
            {
                Delete(key);
                return null;
            }

            var sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
            SpriteCache[key] = sprite;
            return sprite;
        }

        public static void Delete(string key)
        {
            var path = GetPath(key);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// Removes ALL locally cached content for a tale — images, text, cover, narration,
        /// AI narration and the ready flag. Used when the server removes a tale from the
        /// catalog (status = "removed"). Safe to call for a tale that isn't cached.
        /// </summary>
        public static void DeleteTale(string taleId)
        {
            if (string.IsNullOrEmpty(taleId)) return;

            foreach (var sub in new[] { "illustrations", "tales-text", "narration", "ai-narration", "parent-narration" })
            {
                var dir = Path.Combine(Root, sub, taleId);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            Delete(CoverKey(taleId));
            Delete(TaleReadyKey(taleId));

            // Drop any in-memory sprites belonging to this tale.
            var coverKey = CoverKey(taleId);
            var illPrefix = $"illustrations/{taleId}/";
            var stale = new List<string>();
            foreach (var k in SpriteCache.Keys)
                if (k == coverKey || k.StartsWith(illPrefix)) stale.Add(k);
            foreach (var k in stale) SpriteCache.Remove(k);
        }

        /// <summary>Tale ids that have a downloaded ".ready" marker on disk.</summary>
        public static List<string> ListDownloadedTaleIds()
        {
            var result = new List<string>();
            var dir = Path.Combine(Root, "tales");
            if (!Directory.Exists(dir)) return result;
            foreach (var f in Directory.GetFiles(dir, "*.ready"))
                result.Add(Path.GetFileNameWithoutExtension(f));
            return result;
        }

        // ── Maintenance ──────────────────────────────────────
        public static void ClearSpriteCache() => SpriteCache.Clear();

        public static void ClearAll()
        {
            ClearSpriteCache();
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}
