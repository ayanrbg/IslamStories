using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FairyTales.Api;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace FairyTales.Lessons
{
    /// <summary>What <see cref="LessonCache.Prepare"/> hands the player.</summary>
    public class LessonPrepareResult
    {
        /// <summary>Ready to play, or null when <see cref="Failure"/> says why not.</summary>
        public LessonPackage Package;
        public LessonFailure Failure;
    }

    /// <summary>A lesson on disk: the parsed text plus where its files are.</summary>
    public class LessonPackage
    {
        public Lesson Lesson;
        /// <summary>The installed copy: lessons/&lt;id&gt;/current.</summary>
        public string Dir;
        /// <summary>Content language of <see cref="Lesson"/> (ru | kz | en).</summary>
        public string Lang;
        /// <summary>Folder of the personal narration under &lt;lang&gt;/narration.</summary>
        public string VoiceTag;
        /// <summary>
        /// Opened without the server: no connection, or the server could not be reached.
        /// The copy may be of an older contentVersion and may have no personal narration.
        /// </summary>
        public bool Offline;

        /// <summary>Local file of a picture or video for this child's gender, or null.</summary>
        public string MediaPath(string key, string gender)
        {
            if (string.IsNullOrEmpty(key) || Lesson?.media == null
                || !Lesson.media.TryGetValue(key, out var m) || m == null) return null;
            var name = LessonFiles.FileName(m.UrlFor(gender));
            if (name == null) return null;
            var path = Path.Combine(Dir, LessonCache.MediaFolder, name);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Narration mp3 of one text (<see cref="NarrationKey"/>): the personal take with
        /// the child's name, else the pre-recorded take of a bundled lesson, else null —
        /// then the player shows "Next" after 5 s without sound (spec §8).
        /// </summary>
        public string NarrationPath(string key)
        {
            if (!LessonFiles.IsSafeName(key)) return null;
            foreach (var voice in new[] { VoiceTag, LessonCache.DefaultVoice })
            {
                if (string.IsNullOrEmpty(voice)) continue;
                var path = Path.Combine(Dir, Lang, LessonCache.NarrationFolder, voice, key + ".mp3");
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }

    /// <summary>
    /// Installed copy of a lesson, per spec §8 "Загрузка и кеш":
    /// <code>
    /// persistentDataPath/lessons/&lt;id&gt;/current/
    ///     manifest.json                 contentVersion + media records
    ///     media/&lt;file&gt;                  pictures and video, shared by all languages
    ///     &lt;lang&gt;/lesson.json            the server's JSON as received
    ///     &lt;lang&gt;/narration/&lt;voice&gt;/    s0.p0.mp3 …, done.json once complete
    /// </code>
    /// A lesson is downloaded whole before it starts. A new contentVersion is built in
    /// lessons/&lt;id&gt;/next/ and swapped in only once complete, so the old copy keeps
    /// working offline until then (the ReconcileTextVersion idea for tales). Media
    /// whose size did not change is copied over instead of downloaded again: a text
    /// fix also raises the version.
    /// </summary>
    public static class LessonCache
    {
        public const string MediaFolder = "media";
        public const string NarrationFolder = "narration";
        /// <summary>Narration of a bundled lesson, recorded without the child's name.</summary>
        public const string DefaultVoice = "default";

        private const string ManifestFile = "manifest.json";
        private const string LessonFile = "lesson.json";
        private const string NarrationDoneFile = "done.json";
        private const string BundledFolder = "BundledLessons";

        // Share of the progress bar the narration takes; the rest is media by bytes.
        private const float NarrationShare = 0.15f;

        private static string Root => Path.Combine(Application.persistentDataPath, "lessons");
        private static string LessonDir(string id) => Path.Combine(Root, id);
        private static string CurrentDir(string id) => Path.Combine(LessonDir(id), "current");
        private static string NextDir(string id) => Path.Combine(LessonDir(id), "next");

        /// <summary>
        /// Personal narration folder: app language (en-US and en-GB speak with different
        /// voices over one English text) plus the child's name, gender and narrator.
        /// A new name gets a new folder; the old one is removed after the next download.
        /// </summary>
        public static string VoiceTag(string appLang) => $"{appLang}-{UserProfile.CacheTag}";

        // ─────────────────────────── state queries ───────────────────────────

        /// <summary>contentVersion of the installed copy, 0 when there is none.</summary>
        public static int InstalledVersion(string id)
            => LessonFiles.IsSafeName(id) ? ReadManifest(CurrentDir(id))?.contentVersion ?? 0 : 0;

        /// <summary>The installed copy has the text in this app language's content language.</summary>
        public static bool IsInstalled(string id, string appLang)
        {
            var lang = LessonLang.ContentLang(appLang);
            return lang != null && LessonFiles.IsSafeName(id)
                && File.Exists(Path.Combine(CurrentDir(id), lang, LessonFile));
        }

        /// <summary>Installed in this language and of the version the catalog announces.</summary>
        public static bool IsUpToDate(LessonSummary lesson, string appLang)
            => lesson != null && IsInstalled(lesson.id, appLang)
               && InstalledVersion(lesson.id) == lesson.contentVersion;

        /// <summary>Personal narration for the current child is fully downloaded.</summary>
        public static bool HasPersonalNarration(string id, string appLang)
        {
            var lang = LessonLang.ContentLang(appLang);
            return lang != null && LessonFiles.IsSafeName(id) && File.Exists(Path.Combine(
                CurrentDir(id), lang, NarrationFolder, VoiceTag(appLang), NarrationDoneFile));
        }

        // ─────────────────────────── prepare ───────────────────────────

        private class Job
        {
            public float Progress;
            public bool Done;
            public LessonPrepareResult Result;
        }

        // One download per lesson and language: the player opening a lesson the Wi-Fi
        // prefetch is still fetching joins that download instead of starting another.
        private static readonly Dictionary<string, Job> Jobs = new();

        /// <summary>
        /// Gets a lesson ready to play: downloads or updates it when the server is
        /// reachable, otherwise opens the installed copy. <paramref name="knownVersion"/>
        /// is the catalog's contentVersion (0 = unknown): an up-to-date copy with its
        /// narration then opens without touching the network. The download itself runs
        /// on <see cref="LessonRunner"/>, so it finishes even if the caller goes away.
        /// </summary>
        public static IEnumerator Prepare(LessonService service, string id, int knownVersion,
            string appLang, Action<float> onProgress, Action<LessonPrepareResult> done)
        {
            var key = id + "|" + appLang;
            if (!Jobs.TryGetValue(key, out var job))
            {
                job = new Job();
                Jobs[key] = job;
                LessonRunner.Run(RunJob(service, id, knownVersion, appLang, job, key));
            }
            while (!job.Done)
            {
                onProgress?.Invoke(job.Progress);
                yield return null;
            }
            onProgress?.Invoke(1f);
            done?.Invoke(job.Result);
        }

        /// <summary>
        /// After the reward: fetch the next lesson in the background, Wi-Fi only (spec §8).
        /// Locked and coming-soon lessons are skipped — the server would refuse them.
        /// </summary>
        public static void PrefetchInBackground(LessonService service, LessonSummary next,
            string appLang, bool subscribed)
        {
            if (next == null || next.IsComingSoon || next.IsLocked(subscribed)) return;
            if (Application.internetReachability != NetworkReachability.ReachableViaLocalAreaNetwork) return;
            if (IsUpToDate(next, appLang) && HasPersonalNarration(next.id, appLang)) return;
            LessonRunner.Run(Prepare(service, next.id, next.contentVersion, appLang, null, null));
        }

        private static IEnumerator RunJob(LessonService service, string id, int knownVersion,
            string appLang, Job job, string jobKey)
        {
            var result = new LessonPrepareResult();
            yield return EnsureBundledInstalled();

            if (!LessonFiles.IsSafeName(id) || LessonLang.ContentLang(appLang) == null)
            {
                result.Failure = LessonFailure.NotFound;
                Finish(job, jobKey, result);
                yield break;
            }

            bool installed = IsInstalled(id, appLang);
            bool offline = Application.internetReachability == NetworkReachability.NotReachable;
            bool fresh = installed && knownVersion > 0 && InstalledVersion(id) == knownVersion
                         && HasPersonalNarration(id, appLang);

            if (fresh || (offline && installed))
            {
                yield return Open(id, appLang, p => result.Package = p);
                if (result.Package != null) result.Package.Offline = offline;
                else result.Failure = LessonFailure.Malformed;
                Finish(job, jobKey, result);
                yield break;
            }

            Lesson lesson = null;
            string json = null;
            var failure = LessonFailure.None;
            yield return service.GetLesson(id, appLang,
                (l, j) => { lesson = l; json = j; },
                (f, _) => failure = f);

            bool ok = false;
            if (lesson != null)
                yield return Install(service, lesson, json, appLang, job, r => ok = r);

            if (ok || (installed && (lesson != null || failure == LessonFailure.Network)))
            {
                // A failed download keeps the old copy whole, so it still plays — just
                // without the newest text or a narration we could not fetch.
                yield return Open(id, appLang, p => result.Package = p);
                if (result.Package != null) result.Package.Offline = !ok;
                else result.Failure = LessonFailure.Malformed;
            }
            else
            {
                result.Failure = lesson != null ? LessonFailure.Network : failure;
            }
            Finish(job, jobKey, result);
        }

        private static void Finish(Job job, string jobKey, LessonPrepareResult result)
        {
            job.Result = result;
            job.Progress = 1f;
            job.Done = true;
            Jobs.Remove(jobKey);
        }

        // ─────────────────────────── install ───────────────────────────

        private class NarrationJob
        {
            public bool Done;
            public int Total;
            public int Fetched;
        }

        private static IEnumerator Install(LessonService service, Lesson lesson, string json,
            string appLang, Job job, Action<bool> done)
        {
            var id = lesson.id;
            var lang = string.IsNullOrEmpty(lesson.lang) ? LessonLang.ContentLang(appLang) : lesson.lang;
            if (!LessonFiles.IsSafeName(lang)) { done(false); yield break; }

            var current = ReadManifest(CurrentDir(id));
            bool inPlace = current != null && current.contentVersion == lesson.contentVersion;
            var target = inPlace ? CurrentDir(id) : NextDir(id);
            if (!inPlace && !LessonFiles.TryDeleteDir(target)) { done(false); yield break; }

            var manifest = inPlace ? current : new LessonManifest { contentVersion = lesson.contentVersion };
            manifest.media ??= new();
            var targetMedia = Path.Combine(target, MediaFolder);
            var currentMedia = Path.Combine(CurrentDir(id), MediaFolder);

            // Plan: what is already here, what the previous version can lend, what to fetch.
            var fetch = new List<(string url, string file, long bytes)>();
            long fetchBytes = 0;
            bool ioOk = true;
            foreach (var (key, media) in lesson.media ?? new Dictionary<string, LessonMedia>())
            {
                if (media == null) continue;
                var urls = media.AllUrls().ToList();
                var files = urls.Select(LessonFiles.FileName).ToList();
                if (files.Count == 0 || files.Any(f => f == null)) continue;
                manifest.media[key] = new LessonMediaRecord { bytes = media.bytes, files = files };

                if (files.All(f => File.Exists(Path.Combine(targetMedia, f)))) continue;

                if (!inPlace && current?.media != null
                    && current.media.TryGetValue(key, out var old) && old != null
                    && old.bytes == media.bytes && old.files != null
                    && files.All(f => old.files.Contains(f) && File.Exists(Path.Combine(currentMedia, f))))
                {
                    ioOk &= TryCopy(files, currentMedia, targetMedia);
                    continue;
                }

                long share = media.bytes > 0 ? media.bytes / urls.Count : 1;
                for (int i = 0; i < urls.Count; i++)
                {
                    if (File.Exists(Path.Combine(targetMedia, files[i]))) continue;
                    fetch.Add((urls[i], files[i], share));
                    fetchBytes += share;
                }
            }

            // Narration is slow on a cold server cache: ask for it while media downloads.
            var narration = new NarrationJob();
            LessonRunner.Run(FetchNarration(service, lesson, lang, appLang, target, narration));

            long fetched = 0;
            bool mediaOk = ioOk;
            foreach (var (url, file, bytes) in fetch)
            {
                if (!mediaOk) break;
                byte[] data = null;
                yield return service.Api.GetBytes(url, d => data = d, _ => { });
                if (data == null || !TryWrite(Path.Combine(targetMedia, file), data)) mediaOk = false;
                fetched += bytes;
                job.Progress = Combine(fetched, fetchBytes, narration);
            }

            while (!narration.Done)
            {
                job.Progress = Combine(fetched, fetchBytes, narration);
                yield return null;
            }

            if (!mediaOk)
            {
                if (!inPlace) LessonFiles.TryDeleteDir(target);
                done(false);
                yield break;
            }

            bool written = TryWrite(Path.Combine(target, lang, LessonFile), System.Text.Encoding.UTF8.GetBytes(json))
                           && TryWrite(Path.Combine(target, ManifestFile),
                               System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest)));

            if (written && !inPlace)
            {
                // The swap. The old version's narration read the old text, so it goes with it.
                written = LessonFiles.TryDeleteDir(CurrentDir(id)) && TryMove(target, CurrentDir(id));
            }
            if (!written && !inPlace) LessonFiles.TryDeleteDir(target);
            done(written);
        }

        private static float Combine(long fetched, long total, NarrationJob n)
        {
            float media = total > 0 ? Mathf.Clamp01((float)fetched / total) : 1f;
            float narr = n.Done ? 1f : n.Total > 0 ? (float)n.Fetched / n.Total : 0f;
            return media * (1f - NarrationShare) + narr * NarrationShare;
        }

        /// <summary>
        /// Personal narration into &lt;target&gt;/&lt;lang&gt;/narration/&lt;voice&gt;/. Failing is not
        /// fatal: the lesson plays without it and the next Prepare tries again.
        /// done.json marks a complete set, including keys the server could not voice.
        /// </summary>
        private static IEnumerator FetchNarration(LessonService service, Lesson lesson,
            string lang, string appLang, string target, NarrationJob job)
        {
            var voice = VoiceTag(appLang);
            var narrationRoot = Path.Combine(target, lang, NarrationFolder);
            var dir = Path.Combine(narrationRoot, voice);
            if (File.Exists(Path.Combine(dir, NarrationDoneFile))) { job.Done = true; yield break; }

            LessonNarration narration = null;
            yield return service.Narrate(lesson.id, appLang, UserProfile.ChildName,
                UserProfile.ChildGender, UserProfile.NarratorGender,
                n => narration = n, (_, _) => { });

            // Text edited on the server between the two requests: this audio reads other words.
            if (narration == null || narration.contentVersion != lesson.contentVersion)
            {
                job.Done = true;
                yield break;
            }

            job.Total = narration.audio.Count;
            bool ok = true;
            foreach (var (key, url) in narration.audio)
            {
                if (!LessonFiles.IsSafeName(key)) { job.Fetched++; continue; }
                var path = Path.Combine(dir, key + ".mp3");
                if (!File.Exists(path))
                {
                    byte[] data = null;
                    yield return service.Api.GetBytes(url, d => data = d, _ => { });
                    if (data == null || !TryWrite(path, data)) { ok = false; break; }
                }
                job.Fetched++;
            }

            if (ok)
            {
                var record = new NarrationRecord
                {
                    contentVersion = narration.contentVersion,
                    missing = narration.missing ?? new List<string>(),
                };
                ok = TryWrite(Path.Combine(dir, NarrationDoneFile),
                    System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record)));
            }

            if (ok && Directory.Exists(narrationRoot))
            {
                // Takes for a previous name or narrator are never played again.
                foreach (var other in Directory.GetDirectories(narrationRoot))
                {
                    var name = Path.GetFileName(other);
                    if (name != voice && name != DefaultVoice) LessonFiles.TryDeleteDir(other);
                }
            }
            job.Done = true;
        }

        // ─────────────────────────── open ───────────────────────────

        /// <summary>The installed copy in this app language, parsed off the main thread; null if absent.</summary>
        public static IEnumerator Open(string id, string appLang, Action<LessonPackage> done)
        {
            var lang = LessonLang.ContentLang(appLang);
            if (lang == null || !LessonFiles.IsSafeName(id)) { done(null); yield break; }

            var dir = CurrentDir(id);
            var path = Path.Combine(dir, lang, LessonFile);
            string json = null;
            try { if (File.Exists(path)) json = File.ReadAllText(path); }
            catch (Exception e) { Debug.LogWarning($"[LessonCache] read {path}: {e.Message}"); }
            if (json == null) { done(null); yield break; }

            Lesson lesson = null;
            yield return JsonAsync.Parse<Lesson>(json, l => lesson = l);
            if (lesson == null) { done(null); yield break; }

            done(new LessonPackage
            {
                Lesson = lesson,
                Dir = dir,
                Lang = lang,
                VoiceTag = VoiceTag(appLang),
            });
        }

        // ─────────────────────────── housekeeping ───────────────────────────

        /// <summary>
        /// Deletes installed lessons that a FRESH server catalog no longer lists (hidden or
        /// removed). Bundled lessons stay. Never call it with an offline stand-in list.
        /// </summary>
        public static void Prune(IEnumerable<string> catalogIds)
        {
            if (!Directory.Exists(Root)) return;
            var keep = new HashSet<string>(catalogIds ?? Array.Empty<string>());
            if (_bundledIds != null) keep.UnionWith(_bundledIds);
            foreach (var dir in Directory.GetDirectories(Root))
            {
                var id = Path.GetFileName(dir);
                if (!keep.Contains(id) && !IsBusy(id)) LessonFiles.TryDeleteDir(dir);
            }
        }

        private static bool IsBusy(string id) => Jobs.Keys.Any(k => k.StartsWith(id + "|"));

        // ─────────────────────────── bundled lessons ───────────────────────────

        private static HashSet<string> _bundledIds;
        private static bool _bundledInstalling;

        /// <summary>Ids shipped in StreamingAssets/BundledLessons; empty until installed.</summary>
        public static IReadOnlyCollection<string> BundledIds
            => (IReadOnlyCollection<string>)_bundledIds ?? Array.Empty<string>();

        /// <summary>
        /// Copies the lessons shipped in StreamingAssets/BundledLessons into the cache, so
        /// the player reads every lesson from one place. On Android StreamingAssets is
        /// inside the APK and cannot be listed, hence the file list in manifest.json:
        /// <code>[{ "id": "bismillah", "contentVersion": 3, "files": ["manifest.json",
        ///   "media/story_1_boy.webp", "ru/lesson.json", "ru/narration/default/s0.p0.mp3", …] }]</code>
        /// A bundle older than the installed copy is skipped; of the same version, only
        /// missing files are added (another language, the default narration).
        /// Runs once per launch; Prepare calls it, so nothing at startup has to.
        /// </summary>
        public static IEnumerator EnsureBundledInstalled()
        {
            while (_bundledInstalling) yield return null;
            if (_bundledIds != null) yield break;
            _bundledInstalling = true;

            var ids = new HashSet<string>();
            string text = null;
            yield return ReadStreaming(BundledFolder + "/manifest.json", b => text = b == null ? null : System.Text.Encoding.UTF8.GetString(b));

            BundledLessonEntry[] entries = null;
            if (text != null)
            {
                try { entries = JsonConvert.DeserializeObject<BundledLessonEntry[]>(text); }
                catch (Exception e) { Debug.LogWarning($"[LessonCache] bundled manifest: {e.Message}"); }
            }

            foreach (var entry in entries ?? Array.Empty<BundledLessonEntry>())
            {
                if (entry == null || !LessonFiles.IsSafeName(entry.id) || entry.files == null) continue;
                ids.Add(entry.id);

                int installed = InstalledVersion(entry.id);
                if (installed > entry.contentVersion) continue;
                bool merge = installed == entry.contentVersion;
                var target = merge ? CurrentDir(entry.id) : NextDir(entry.id);
                if (!merge) LessonFiles.TryDeleteDir(target);

                bool ok = true;
                // Text and manifest last: when merging into the live copy, a lesson.json
                // that is there must mean its media are there too.
                foreach (var rel in entry.files.OrderBy(f => f != null
                             && (f.EndsWith("/" + LessonFile) || f == ManifestFile) ? 1 : 0))
                {
                    var parts = (rel ?? "").Split('/');
                    if (parts.Length == 0 || !parts.All(LessonFiles.IsSafeName)) continue;
                    var dest = Path.Combine(target, Path.Combine(parts));
                    if (File.Exists(dest)) continue;

                    byte[] data = null;
                    yield return ReadStreaming($"{BundledFolder}/{entry.id}/{rel}", b => data = b);
                    if (data == null || !TryWrite(dest, data)) { ok = false; break; }
                }

                if (!merge)
                {
                    if (ok && LessonFiles.TryDeleteDir(CurrentDir(entry.id)) && TryMove(target, CurrentDir(entry.id))) continue;
                    LessonFiles.TryDeleteDir(target);
                }
            }

            _bundledIds = ids;
            _bundledInstalling = false;
        }

        private static IEnumerator ReadStreaming(string relative, Action<byte[]> done)
        {
            var path = Path.Combine(Application.streamingAssetsPath, relative);
#if UNITY_ANDROID && !UNITY_EDITOR
            var url = path;
#else
            var url = "file://" + path;
#endif
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();
            done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null);
        }

        // ─────────────────────────── io ───────────────────────────

        private static LessonManifest ReadManifest(string dir)
        {
            var path = Path.Combine(dir, ManifestFile);
            try
            {
                return File.Exists(path)
                    ? JsonConvert.DeserializeObject<LessonManifest>(File.ReadAllText(path))
                    : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LessonCache] manifest {path}: {e.Message}");
                return null;
            }
        }

        private static bool TryWrite(string path, byte[] data)
        {
            try
            {
                LessonFiles.WriteAtomic(path, data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LessonCache] write {path}: {e.Message}");
                return false;
            }
        }

        private static bool TryCopy(IEnumerable<string> files, string fromDir, string toDir)
        {
            try
            {
                Directory.CreateDirectory(toDir);
                foreach (var f in files) File.Copy(Path.Combine(fromDir, f), Path.Combine(toDir, f), true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LessonCache] copy media: {e.Message}");
                return false;
            }
        }

        private static bool TryMove(string from, string to)
        {
            try
            {
                Directory.Move(from, to);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LessonCache] move {from}: {e.Message}");
                return false;
            }
        }
    }

    // ─────────────────────────── on-disk records ───────────────────────────

    [Serializable, Preserve]
    public class LessonManifest
    {
        public int contentVersion;
        /// <summary>Media key → its files and the server's size, to spot a replaced file on update.</summary>
        public Dictionary<string, LessonMediaRecord> media = new();
    }

    [Serializable, Preserve]
    public class LessonMediaRecord
    {
        public long bytes;
        public List<string> files = new();
    }

    [Serializable, Preserve]
    public class NarrationRecord
    {
        public int contentVersion;
        public List<string> missing = new();
    }

    [Serializable, Preserve]
    public class BundledLessonEntry
    {
        public string id;
        public int contentVersion;
        public List<string> files;
    }
}
