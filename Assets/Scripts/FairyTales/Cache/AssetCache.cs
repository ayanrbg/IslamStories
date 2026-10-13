using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FairyTales.Cache
{
    public static partial class AssetCache
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
            // First, before anything below may clear the cache. Startup must survive an
            // IO failure here: the recordings then stay where they were (ClearAll spares
            // that folder) and the move is simply tried again next launch.
            try { MoveParentVoicesOutOfCache(); }
            catch (Exception e) { Diagnostics.RemoteLog.Event("parent_voices_move_fail", e.Message); }
            PurgeLegacyAiNarration();
            try { PurgeOctober2026Changes(); }
            catch (Exception e) { Diagnostics.RemoteLog.Event("cache_purge_2026_10_fail", e.Message); }

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
        // and we store one WAV per (tale, language, voice, page). Played back verbatim
        // during reading — no server, no cloning. Voices are listed in ParentVoices.
        //
        // These recordings are the family's own work, not cache: nothing can download
        // them again. They live beside the cache rather than inside it, so ClearAll —
        // run whenever CacheVersion is bumped — can never take them.
        private static string VoicesRoot => Path.Combine(Application.persistentDataPath, "parent-voices");

        // Where builds before VoicesRoot kept them (inside the cache).
        private static string LegacyVoicesRoot => Path.Combine(Root, "parent-narration");

        private static string ParentNarrationPath(string taleId, string lang, string voiceId, int page)
        {
            var dir = ParentVoiceDir(taleId, lang, voiceId);
            return dir == null ? null : Path.Combine(dir, $"page_{page}.wav");
        }

        public static void SaveParentNarration(string taleId, string lang, string voiceId, int page, byte[] wav)
        {
            var path = ParentNarrationPath(taleId, lang, voiceId, page);
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, wav);
        }

        public static byte[] LoadParentNarration(string taleId, string lang, string voiceId, int page)
        {
            var path = ParentNarrationPath(taleId, lang, voiceId, page);
            return path != null && File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public static bool HasParentNarrationPage(string taleId, string lang, string voiceId, int page)
        {
            var path = ParentNarrationPath(taleId, lang, voiceId, page);
            return path != null && File.Exists(path);
        }

        /// <summary>True if at least one page has been recorded in this voice.</summary>
        public static bool HasAnyParentNarration(string taleId, string lang, string voiceId)
        {
            var dir = ParentVoiceDir(taleId, lang, voiceId);
            return dir != null && Directory.Exists(dir)
                && Directory.GetFiles(dir, "page_*.wav").Length > 0;
        }

        /// <summary>Deletes all recorded pages of one voice.</summary>
        public static void ClearParentNarration(string taleId, string lang, string voiceId)
        {
            var dir = ParentVoiceDir(taleId, lang, voiceId);
            if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // Path.Combine throws on a null segment, and callers legitimately ask about a
        // tale that was never set (RecordingOverlay before Activate) — answer "nothing
        // recorded" instead of blowing up the caller's call stack.
        internal static string ParentNarrationDir(string taleId, string lang)
            => string.IsNullOrEmpty(taleId) || string.IsNullOrEmpty(lang)
                ? null
                : Path.Combine(VoicesRoot, taleId, lang);

        private static string ParentVoiceDir(string taleId, string lang, string voiceId)
        {
            var dir = ParentNarrationDir(taleId, lang);
            return dir == null || string.IsNullOrEmpty(voiceId) ? null : Path.Combine(dir, voiceId);
        }

        /// <summary>
        /// Moves recordings made by older builds out of the cache. Runs before anything
        /// that may clear the cache. Moves whatever the new place doesn't have yet, entry
        /// by entry; the old folder is removed only once nothing is left in it, so a clash
        /// never costs a recording (ClearAll spares what stays behind).
        /// </summary>
        private static void MoveParentVoicesOutOfCache()
        {
            var legacy = LegacyVoicesRoot;
            if (!Directory.Exists(legacy)) return;

            MergeInto(legacy, VoicesRoot);
            if (Directory.GetFileSystemEntries(legacy).Length == 0)
                Directory.Delete(legacy);
        }

        // Moves every entry of src that dest lacks; recurses into folders both have, and
        // drops folders it emptied. Files present in both stay where they are.
        private static void MergeInto(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var dir in Directory.GetDirectories(src))
            {
                var target = Path.Combine(dest, Path.GetFileName(dir));
                if (!Directory.Exists(target)) Directory.Move(dir, target);
                else
                {
                    MergeInto(dir, target);
                    if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
                }
            }
            foreach (var file in Directory.GetFiles(src))
            {
                var target = Path.Combine(dest, Path.GetFileName(file));
                if (!File.Exists(target)) File.Move(file, target);
            }
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
        /// (hasDefaultNarration is false everywhere and /default-narration is a 404), so
        /// that folder holds nothing but TTS an older build wrote there. If pre-recorded
        /// narration ever ships, take that folder back out — the client only downloads it
        /// while fetching a tale and would not restore it here.
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
        /// Deletes every narrator mp3 an older build could have written without
        /// recording WHICH narrator made it. Two places, both of them landmines:
        ///
        ///   ai-narration/{tale}/{lang}/*.mp3  — the old AI layout, keyed by language
        ///       alone. Some of these ARE the male-under-female takes the server handed
        ///       out before 14.08.2026; which ones, nobody can tell.
        ///   narration/{tale}/{lang}/*.mp3     — the TTS fallback of builds that did not
        ///       send narratorGender at all, so every file here is male. This folder is
        ///       read FIRST in PlayDefaultNarration, ahead of any voice check, so a
        ///       leftover here plays male no matter what the user picked. Safe to wipe:
        ///       no tale ships pre-recorded audio (hasDefaultNarration is false for every
        ///       tale in both apps, /default-narration is a 404).
        ///
        /// Runs once. New audio lives one folder deeper, under a profile tag.
        /// </summary>
        private static void PurgeLegacyAiNarration()
        {
            const string flag = "ft_ai_narration_v2";
            if (PlayerPrefs.GetInt(flag, 0) == 1) return;

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
            public bool complete; // every page generated
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

        // The library asks this for every card each time it is shown, and each answer was
        // a file read plus up to three existence checks on the main thread. Only
        // MarkTaleReady, DeleteTale and ClearAll change it, and they drop the entry.
        private static readonly Dictionary<string, bool> DownloadedMemo = new();

        public static bool IsTaleDownloaded(string taleId)
        {
            if (DownloadedMemo.TryGetValue(taleId, out var known)) return known;
            var downloaded = CheckTaleDownloaded(taleId);
            // Before the bundled set has loaded, a bundled tale reads as not downloaded —
            // an answer that must not be kept.
            if (downloaded || BundledTaleLoader.IsReady) DownloadedMemo[taleId] = downloaded;
            return downloaded;
        }

        private static bool CheckTaleDownloaded(string taleId)
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
        {
            Save(TaleReadyKey(taleId), new byte[] { IllustrationsVersion });
            DownloadedMemo.Remove(taleId);
        }

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

        /// <summary>Async sprite load via UnityWebRequest (file I/O off main thread).
        /// <paramref name="cache"/> false leaves the sprite to the caller: illustrations
        /// are owned by IllustrationProvider, which frees them — a second reference here
        /// would keep every page ever read in memory.</summary>
        public static IEnumerator LoadSpriteAsync(string key, Action<Sprite> cb, bool cache = true)
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
            if (cache) SpriteCache[key] = sprite;
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

            foreach (var sub in new[] { "illustrations", "tales-text", "narration", "ai-narration", "tts-narration" })
            {
                var dir = Path.Combine(Root, sub, taleId);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            var voices = Path.Combine(VoicesRoot, taleId);
            if (Directory.Exists(voices)) Directory.Delete(voices, true);
            Delete(CoverKey(taleId));
            Delete(TaleReadyKey(taleId));
            DownloadedMemo.Remove(taleId);

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
            DownloadedMemo.Clear();
            if (!Directory.Exists(Root)) return;

            // Everything in the cache can be fetched again — except parent recordings that
            // an older build left here and that could not be moved out yet.
            var legacyVoices = Path.GetFullPath(LegacyVoicesRoot);
            foreach (var dir in Directory.GetDirectories(Root))
                if (Path.GetFullPath(dir) != legacyVoices) Directory.Delete(dir, true);
            foreach (var file in Directory.GetFiles(Root))
                File.Delete(file);
        }
    }
}
