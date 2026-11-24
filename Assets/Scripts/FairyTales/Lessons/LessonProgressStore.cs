using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Loads and saves <see cref="LessonProgress"/> as progress.json in persistentDataPath
    /// (spec §5 "Хранение"). Written through a temp file and a rename, the previous copy
    /// kept as progress.json.bak: a crash mid-write must not cost the child their stars.
    /// The server never sees this file. On a new phone it moves with the iCloud backup
    /// or Android auto-backup; a reinstall loses it, which the spec accepts.
    ///
    /// Main thread only. Every change goes through a method here that saves at once —
    /// the file is a few kilobytes and changes a handful of times per lesson.
    /// </summary>
    public static class LessonProgressStore
    {
        private const string FileName = "progress.json";

        private static LessonProgress _data;

        /// <summary>Raised after every saved change — counters on screen refresh from it.</summary>
        public static event Action Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Read-only use: change progress through the methods below so it gets saved.</summary>
        public static LessonProgress Data => _data ??= Load();

        // ─────────────────────────── changes ───────────────────────────

        public static LessonReward CompleteLesson(string lessonId, string title)
        {
            var reward = Data.CompleteLesson(lessonId, title, DateTime.Now);
            Save();
            return reward;
        }

        public static void SetUnfinished(string lessonId, int step, int contentVersion)
        {
            Data.SetUnfinished(lessonId, step, contentVersion, DateTime.Now);
            Save();
        }

        public static void ClearUnfinished(string lessonId)
        {
            if (Data.unfinished == null) return;
            Data.ClearUnfinished(lessonId);
            Save();
        }

        public static void AddPendingDeed(string lessonId, string title, string leafLabel)
        {
            Data.AddPendingDeed(lessonId, title, leafLabel, DateTime.Now);
            Save();
        }

        public static bool MarkDeed(string lessonId, string leafLabel)
        {
            var awarded = Data.MarkDeed(lessonId, leafLabel, DateTime.Now);
            Save();
            return awarded;
        }

        public static int StreakDays => Data.StreakDays(DateTime.Now);

        // ─────────────────────────── io ───────────────────────────

        public static void Save()
        {
            if (_data == null) return;
            try
            {
                LessonFiles.WriteAtomic(FilePath, JsonConvert.SerializeObject(_data), keepBackup: true);
            }
            catch (Exception e)
            {
                // Progress stays in memory for this session; the next change tries again.
                Debug.LogWarning($"[LessonProgress] save failed: {e.Message}");
                Diagnostics.RemoteLog.Event("lesson_progress_save_fail", e.Message);
            }
            Changed?.Invoke();
        }

        private static LessonProgress Load()
        {
            var data = TryRead(FilePath) ?? TryRead(FilePath + ".bak") ?? new LessonProgress();
            data.Normalize();
            return data;
        }

        private static LessonProgress TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonConvert.DeserializeObject<LessonProgress>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LessonProgress] {Path.GetFileName(path)} unreadable: {e.Message}");
                Diagnostics.RemoteLog.Event("lesson_progress_read_fail", e.Message);
                return null;
            }
        }

        /// <summary>Forget the loaded copy so the next access reads the file again.</summary>
        internal static void ResetForTests() => _data = null;
    }
}
