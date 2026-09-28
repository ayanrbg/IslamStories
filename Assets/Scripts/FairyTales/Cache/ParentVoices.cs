using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FairyTales.Cache
{
    [Serializable]
    public class ParentVoice
    {
        public string id;
        public string speaker;     // who read it aloud ("Имя диктора")
        public string childName;   // who it was read for, as the profile had it then
        public string childGender; // "male" | "female"
        public long created;       // UTC ticks
    }

    /// <summary>
    /// The parent recordings of one tale in one language. Every "Новая запись" is its
    /// own voice with its own folder of page clips (see AssetCache.ParentNarrationDir),
    /// described in voices.json beside them. Local only — nothing goes to the server.
    /// </summary>
    public static class ParentVoices
    {
        // Recordings from before voices existed sit directly in the language folder.
        private const string LegacyId = "legacy";

        [Serializable]
        private class Index
        {
            public List<ParentVoice> voices = new();
        }

        /// <summary>Newest first. Voices that ended up with no recorded page are dropped.</summary>
        public static List<ParentVoice> List(string taleId, string lang)
        {
            var index = Load(taleId, lang);
            if (index == null) return new List<ParentVoice>();

            bool changed = MigrateLegacy(taleId, lang, index);
            changed |= AdoptOrphans(taleId, lang, index);
            // Leaving the recorder before reading a single page leaves an empty voice behind.
            changed |= index.voices.RemoveAll(v =>
                !AssetCache.HasAnyParentNarration(taleId, lang, v.id)) > 0;
            if (changed) Save(taleId, lang, index);

            var list = new List<ParentVoice>(index.voices);
            list.Sort((a, b) => b.created.CompareTo(a.created));
            return list;
        }

        public static ParentVoice Create(string taleId, string lang, string speaker)
        {
            var index = Load(taleId, lang) ?? new Index();
            var voice = new ParentVoice
            {
                id = DateTime.UtcNow.Ticks.ToString("x"),
                speaker = speaker?.Trim() ?? "",
                childName = UserProfile.ChildName,
                childGender = UserProfile.ChildGender,
                created = DateTime.UtcNow.Ticks
            };
            index.voices.Add(voice);
            Save(taleId, lang, index);
            return voice;
        }

        public static void Delete(string taleId, string lang, string voiceId)
        {
            AssetCache.ClearParentNarration(taleId, lang, voiceId);
            var index = Load(taleId, lang);
            if (index == null) return;
            if (index.voices.RemoveAll(v => v.id == voiceId) > 0) Save(taleId, lang, index);
        }

        private static bool MigrateLegacy(string taleId, string lang, Index index)
        {
            var dir = AssetCache.ParentNarrationDir(taleId, lang);
            if (dir == null || !Directory.Exists(dir)) return false;
            var pages = Directory.GetFiles(dir, "page_*.wav");
            if (pages.Length == 0) return false;

            var target = Path.Combine(dir, LegacyId);
            Directory.CreateDirectory(target);
            foreach (var f in pages)
            {
                var dest = Path.Combine(target, Path.GetFileName(f));
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(f, dest);
            }

            if (index.voices.Exists(v => v.id == LegacyId)) return true;
            index.voices.Add(new ParentVoice
            {
                id = LegacyId,
                speaker = "",
                childName = UserProfile.ChildName,
                childGender = UserProfile.ChildGender,
                created = File.GetCreationTimeUtc(pages[0]).Ticks
            });
            return true;
        }

        /// <summary>
        /// Recordings on disk that the index doesn't know — a lost or damaged voices.json —
        /// come back as nameless voices instead of vanishing from the list.
        /// </summary>
        private static bool AdoptOrphans(string taleId, string lang, Index index)
        {
            var dir = AssetCache.ParentNarrationDir(taleId, lang);
            if (dir == null || !Directory.Exists(dir)) return false;

            bool changed = false;
            foreach (var voiceDir in Directory.GetDirectories(dir))
            {
                var id = Path.GetFileName(voiceDir);
                if (index.voices.Exists(v => v.id == id)) continue;
                if (Directory.GetFiles(voiceDir, "page_*.wav").Length == 0) continue;
                index.voices.Add(new ParentVoice
                {
                    id = id,
                    speaker = "",
                    childName = UserProfile.ChildName,
                    childGender = UserProfile.ChildGender,
                    created = Directory.GetCreationTimeUtc(voiceDir).Ticks
                });
                changed = true;
            }
            return changed;
        }

        private static string IndexPath(string taleId, string lang)
        {
            var dir = AssetCache.ParentNarrationDir(taleId, lang);
            return dir == null ? null : Path.Combine(dir, "voices.json");
        }

        // null only when the tale/lang is unknown — a missing file is an empty index.
        private static Index Load(string taleId, string lang)
        {
            var path = IndexPath(taleId, lang);
            if (path == null) return null;
            if (!File.Exists(path)) return new Index();
            try
            {
                return JsonUtility.FromJson<Index>(File.ReadAllText(path)) ?? new Index();
            }
            catch (Exception)
            {
                return new Index();
            }
        }

        private static void Save(string taleId, string lang, Index index)
        {
            var path = IndexPath(taleId, lang);
            if (path == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Written aside and swapped in, so an app killed mid-write can't leave a
            // half-written index behind.
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(index));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
