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
            PurgeLegacyAiNarration();

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
            var dir = ParentNarrationDir(taleId, lang);
            return dir != null && Directory.Exists(dir)
                && Directory.GetFiles(dir, "page_*.wav").Length > 0;
        }

        /// <summary>Deletes all recorded parent pages for a tale+language.</summary>
        public static void ClearParentNarration(string taleId, string lang)
        {
            var dir = ParentNarrationDir(taleId, lang);
            if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // Path.Combine throws on a null segment, and callers legitimately ask about a
        // tale that was never set (RecordingOverlay before Activate) — answer "nothing
        // recorded" instead of blowing up the caller's call stack.
        private static string ParentNarrationDir(string taleId, string lang)
            => string.IsNullOrEmpty(taleId) || string.IsNullOrEmpty(lang)
                ? null
                : Path.Combine(Root, "parent-narration", taleId, lang);

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

        // The profile tag (name + child gender + narrator gender) is part of the PATH,
        // not just of a meta file next to it. A meta check only catches params the
        // client changed; it cannot catch audio that is wrong for params it never
        // changed — e.g. a server bug that answered a female request with male takes.
        // Separate folders make replaying another voice structurally impossible.
        public static string AiNarrationKey(string taleId, string lang, int page,
            string profileTag)
            => $"ai-narration/{taleId}/{lang}/{profileTag}/page_{page}.mp3";

        // On-demand TTS for tales with no pre-recorded narration. Keyed by the
        // profile tag (name + child gender + narrator): sharing NarrationKey with the
        // downloaded audio replayed the first take forever, whatever changed after.
        public static string TtsNarrationKey(string taleId, string lang, int page,
            string profileTag)
            => $"tts-narration/{taleId}/{lang}/{profileTag}/page_{page}.mp3";

        // Meta sits inside the tag folder, so it can only ever describe audio made
        // with those very params. It now carries what the path cannot: was the book
        // finished, and which voice id produced it.
        private static string AiNarrationMetaKey(string taleId, string lang, string profileTag)
            => $"ai-narration/{taleId}/{lang}/{profileTag}/meta.json";

        public static void SaveAiNarrationMeta(string taleId, string lang, string profileTag,
            string voice, bool complete)
        {
            var json = JsonUtility.ToJson(new AiNarrationMeta
            {
                voice = voice ?? "",
                complete = complete
            });
            Save(AiNarrationMetaKey(taleId, lang, profileTag),
                System.Text.Encoding.UTF8.GetBytes(json));
        }

        /// <summary>Params the cached AI narration was generated with (empty if none).</summary>
        public static AiNarrationMeta LoadAiNarrationMeta(string taleId, string lang,
            string profileTag)
        {
            var bytes = Load(AiNarrationMetaKey(taleId, lang, profileTag));
            return bytes == null
                ? default
                : JsonUtility.FromJson<AiNarrationMeta>(
                    System.Text.Encoding.UTF8.GetString(bytes));
        }

        /// <summary>
        /// A complete book for exactly these params. No param comparison needed —
        /// the folder IS the params.
        /// </summary>
        public static bool IsAiNarrationCached(string taleId, string lang, string profileTag)
            => HasAudio(AiNarrationKey(taleId, lang, 0, profileTag))
               && LoadAiNarrationMeta(taleId, lang, profileTag).complete;

        /// <summary>
        /// Drops every cached narrator audio for all tales, so the next play regenerates
        /// it. "narration" is included on purpose: no tale ships pre-recorded audio
        /// (hasDefaultNarration is false for all 11 and /default-narration is a 404),
        /// so that folder holds nothing but TTS an older build wrote there — and
        /// PlayDefaultNarration reads it FIRST, ahead of any voice check. If
        /// pre-recorded narration ever ships, take that folder back out: the client
        /// only downloads it while fetching a tale and would not restore it here.
        /// </summary>
        public static void ClearGeneratedNarration()
        {
            foreach (var sub in new[] { "ai-narration", "tts-narration", "narration" })
            {
                var dir = Path.Combine(Root, sub);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// Deletes cached AI narration for one language (or the whole tale when lang is null).
        /// </summary>
        public static void ClearAiNarration(string taleId, string lang = null,
            string profileTag = null)
        {
            var dir = string.IsNullOrEmpty(lang)
                ? Path.Combine(Root, "ai-narration", taleId)
                : string.IsNullOrEmpty(profileTag)
                    ? Path.Combine(Root, "ai-narration", taleId, lang)
                    : Path.Combine(Root, "ai-narration", taleId, lang, profileTag);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        /// <summary>
        /// Deletes audio written by builds that keyed AI narration by language alone
        /// (mp3 straight inside .../{lang}/). Those files carry no record of which
        /// narrator made them, and some of them ARE the male-under-female takes the
        /// server handed out before 14.08.2026 — there is no way to tell which, so
        /// they all go. Runs once; new audio lives one folder deeper.
        /// </summary>
        private static void PurgeLegacyAiNarration()
        {
            const string flag = "ft_ai_narration_v2";
            if (PlayerPrefs.GetInt(flag, 0) == 1) return;

            // Two folders, both landmines: ai-narration/{tale}/{lang}/*.mp3 is the old
            // AI layout (may contain male-under-female takes), and narration/{tale}/
            // {lang}/*.mp3 is the TTS fallback of builds that never sent narratorGender
            // — every file there is male, and PlayDefaultNarration reads it FIRST,
            // ahead of any voice check.
            foreach (var sub in new[] { "ai-narration", "narration" })
            {
                var root = Path.Combine(Root, sub);
                if (!Directory.Exists(root)) continue;
                // Exactly {tale}/{lang}/*.* — files one level deeper belong to the new
                // tag folders and must survive.
                foreach (var taleDir in Directory.GetDirectories(root))
                    foreach (var langDir in Directory.GetDirectories(taleDir))
                        foreach (var f in Directory.GetFiles(langDir))
                            File.Delete(f);
            }
            PlayerPrefs.SetInt(flag, 1);
            PlayerPrefs.Save();
        }

        [Serializable]
        public struct AiNarrationMeta
        {
            public string voice;
            // False when the run was cut short — the pages present are still valid
            // and reusable, the book just isn't fully narrated yet.
            public bool complete;
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

        /// <summary>
        /// Saves narration audio, refusing anything that is not playable. A 200 with
        /// an empty body or a JSON error page used to land in the cache as-is, and
        /// from then on that page was silent forever: the file exists, so nothing
        /// ever re-requests it. Returns false when the payload was rejected.
        /// </summary>
        public static bool SaveAudio(string key, byte[] data)
        {
            if (!LooksLikeAudio(data)) return false;
            Save(key, data);
            return true;
        }

        public static bool LooksLikeAudio(byte[] d)
        {
            if (d == null || d.Length < 512) return false;
            return (d[0] == 'O' && d[1] == 'g' && d[2] == 'g' && d[3] == 'S')   // ogg
                || (d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F')   // wav
                || (d[0] == 'I' && d[1] == 'D' && d[2] == '3')                  // mp3 tag
                || (d[0] == 0xFF && (d[1] & 0xE0) == 0xE0);                     // mp3 frame
        }

        public static byte[] Load(string key)
        {
            var path = GetPath(key);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>
        /// Cached audio, or null when the file is missing or unplayable — a truncated
        /// download or a cached error page is deleted here so the page is requested
        /// again instead of staying silent for the life of the install.
        /// </summary>
        public static byte[] LoadAudio(string key)
        {
            var data = Load(key);
            if (data == null) return null;
            if (LooksLikeAudio(data)) return data;
            Delete(key);
            return null;
        }

        /// <summary>
        /// How many voice-agnostic mp3 an old build left behind ({tale}/{lang}/*.mp3
        /// in either narration folder). Anything above 0 after a launch means the
        /// purge did not run — and that a male take can still win the race.
        /// </summary>
        public static int LegacyAudioCount()
        {
            int n = 0;
            foreach (var sub in new[] { "ai-narration", "narration" })
            {
                var root = Path.Combine(Root, sub);
                if (!Directory.Exists(root)) continue;
                foreach (var taleDir in Directory.GetDirectories(root))
                    foreach (var langDir in Directory.GetDirectories(taleDir))
                        n += Directory.GetFiles(langDir).Length;
            }
            return n;
        }

        /// <summary>Cheap "is there usable audio" check — no full read.</summary>
        public static bool HasAudio(string key)
        {
            var path = GetPath(key);
            return File.Exists(path) && new FileInfo(path).Length >= 512;
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

            foreach (var sub in new[] { "illustrations", "tales-text", "narration",
                         "ai-narration", "tts-narration", "parent-narration" })
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
