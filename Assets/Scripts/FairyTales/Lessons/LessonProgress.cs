using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace FairyTales.Lessons
{
    /// <summary>
    /// The child's lesson progress (spec §5), kept only on the phone in progress.json.
    /// Pure data and rules: every method takes "today" so the rules can be checked
    /// without a clock. <see cref="LessonProgressStore"/> loads and saves it.
    ///
    /// Rewards are for effort only: +1 star for the first completion of a lesson,
    /// +1 for a repeat (once a day per lesson), +1 for a deed a parent marked. A missed
    /// day silently resets the streak — no reproach, no stars lost.
    /// Days are the phone's local date.
    /// </summary>
    [Serializable, Preserve]
    public class LessonProgress
    {
        public const int CurrentSchema = 1;

        public int schemaVersion = CurrentSchema;
        public int stars;
        public List<LeafRecord> leaves = new();
        /// <summary>Lesson id → when it was completed.</summary>
        public Dictionary<string, CompletedLesson> completed = new();
        /// <summary>The lesson the child left mid-way, to resume from its step.</summary>
        public UnfinishedLesson unfinished;
        /// <summary>"Do it in life" tasks finished without a parent's mark («Ждут отметки»).</summary>
        public List<PendingDeed> pendingDeeds = new();
        /// <summary>Lesson id → day a parent last marked its deed; one deed leaf per lesson a day.</summary>
        public Dictionary<string, string> deedMarkedOn = new();
        public StreakRecord streak = new();
        /// <summary>Tales unlocked for good by the leaf reward (release 2).</summary>
        public List<string> rewardTales = new();

        /// <summary>
        /// Fields written by a newer build. Kept and written back, so going back to an
        /// older build (a test install, a staged rollout) does not erase them.
        /// </summary>
        [JsonExtensionData] public IDictionary<string, JToken> extra;

        // ─────────────────────────── queries ───────────────────────────

        public bool IsCompleted(string lessonId)
            => !string.IsNullOrEmpty(lessonId) && completed.ContainsKey(lessonId);

        public int CompletedCount => completed.Count;

        public int LeafCount => leaves.Count;

        /// <summary>Streak to show today: zero once a whole day has passed without a lesson.</summary>
        public int StreakDays(DateTime today)
        {
            if (streak == null || streak.count <= 0 || !TryParseDay(streak.lastDate, out var last)) return 0;
            var gap = (today.Date - last).Days;
            return gap is 0 or 1 ? streak.count : 0;
        }

        /// <summary>Saved step of an unfinished lesson, or -1. A step of another version is not resumed: steps may have moved.</summary>
        public int UnfinishedStep(string lessonId, int contentVersion)
            => unfinished != null && unfinished.lessonId == lessonId
               && unfinished.contentVersion == contentVersion
                ? unfinished.step
                : -1;

        public bool IsDeedPending(string lessonId) => pendingDeeds.Exists(d => d.lessonId == lessonId);

        public bool CanMarkDeedToday(string lessonId, DateTime today)
            => !deedMarkedOn.TryGetValue(lessonId ?? "", out var day) || day != Day(today);

        // ─────────────────────────── changes ───────────────────────────

        /// <summary>
        /// The reward screen is shown — the moment a lesson counts as completed (spec §3,
        /// step 5). <paramref name="title"/> is the lesson's title for the leaf caption.
        /// </summary>
        public LessonReward CompleteLesson(string lessonId, string title, DateTime today)
        {
            var reward = new LessonReward();
            if (string.IsNullOrEmpty(lessonId)) return reward;
            var day = Day(today);

            if (!completed.TryGetValue(lessonId, out var record))
            {
                completed[lessonId] = new CompletedLesson
                {
                    firstDate = day, lastDate = day, count = 1, lastStarDate = day,
                };
                stars++;
                leaves.Add(new LeafRecord { kind = LeafKind.Lesson, id = lessonId, label = title, date = day });
                reward.FirstTime = reward.Star = reward.Leaf = true;
            }
            else
            {
                record.count++;
                record.lastDate = day;
                if (record.lastStarDate != day)
                {
                    record.lastStarDate = day;
                    stars++;
                    reward.Star = true;
                }
            }

            TouchStreak(today);
            if (unfinished != null && unfinished.lessonId == lessonId) unfinished = null;
            return reward;
        }

        public void SetUnfinished(string lessonId, int step, int contentVersion, DateTime now)
        {
            unfinished = new UnfinishedLesson
            {
                lessonId = lessonId,
                step = step,
                contentVersion = contentVersion,
                at = now.ToString("s", CultureInfo.InvariantCulture),
            };
        }

        public void ClearUnfinished(string lessonId)
        {
            if (unfinished != null && (lessonId == null || unfinished.lessonId == lessonId)) unfinished = null;
        }

        /// <summary>"Done" without a parent's mark: the deed waits in «Ждут отметки». One per lesson.</summary>
        public void AddPendingDeed(string lessonId, string title, string leafLabel, DateTime today)
        {
            if (string.IsNullOrEmpty(lessonId)) return;
            pendingDeeds.RemoveAll(d => d.lessonId == lessonId);
            pendingDeeds.Add(new PendingDeed
            {
                lessonId = lessonId, title = title, leafLabel = leafLabel, addedDate = Day(today),
            });
        }

        /// <summary>
        /// A parent marked the deed (behind the parental gate), in the lesson or later
        /// from «Ждут отметки». Gives a "marked by mum" leaf and a star, once a day per
        /// lesson, so repeating a lesson does not farm leaves. Clears the pending entry.
        /// </summary>
        public bool MarkDeed(string lessonId, string leafLabel, DateTime today)
        {
            if (string.IsNullOrEmpty(lessonId)) return false;
            var pending = pendingDeeds.Find(d => d.lessonId == lessonId);
            pendingDeeds.RemoveAll(d => d.lessonId == lessonId);
            if (!CanMarkDeedToday(lessonId, today)) return false;

            var day = Day(today);
            deedMarkedOn[lessonId] = day;
            stars++;
            leaves.Add(new LeafRecord
            {
                kind = LeafKind.Deed,
                id = lessonId,
                label = string.IsNullOrEmpty(leafLabel) ? pending?.leafLabel : leafLabel,
                date = day,
            });
            return true;
        }

        private void TouchStreak(DateTime today)
        {
            streak ??= new StreakRecord();
            var day = Day(today);
            if (streak.lastDate == day) return;

            if (TryParseDay(streak.lastDate, out var last))
            {
                var gap = (today.Date - last).Days;
                // Clock set back: leave the streak as it is rather than reset it.
                if (gap < 0) return;
                streak.count = gap == 1 ? streak.count + 1 : 1;
            }
            else
            {
                streak.count = 1;
            }
            streak.lastDate = day;
        }

        /// <summary>
        /// Called after loading: fills what an older file or a hand edit left null.
        /// Schema migrations go here when <see cref="CurrentSchema"/> grows.
        /// </summary>
        public void Normalize()
        {
            leaves ??= new();
            completed ??= new();
            pendingDeeds ??= new();
            deedMarkedOn ??= new();
            streak ??= new();
            rewardTales ??= new();
            leaves.RemoveAll(l => l == null);
            pendingDeeds.RemoveAll(d => d == null);
            if (schemaVersion < CurrentSchema) schemaVersion = CurrentSchema;
        }

        // Invariant culture: on a phone set to Thai the current culture's calendar
        // would write the year 2569, and a Hijri one another date altogether.
        public static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static bool TryParseDay(string s, out DateTime day)
            => DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out day);
    }

    public static class LeafKind
    {
        /// <summary>First completion of a lesson: «Бисмиллях — новый!».</summary>
        public const string Lesson = "lesson";
        /// <summary>A parent marked the lesson's deed: «Сказали Бисмиллях вместе — отметила мама».</summary>
        public const string Deed = "deed";
        /// <summary>A good deed picked in the parents' section (release 2).</summary>
        public const string GoodDeed = "goodDeed";
    }

    [Serializable, Preserve]
    public class LeafRecord
    {
        public string kind;
        /// <summary>Lesson id, or the good deed's id.</summary>
        public string id;
        /// <summary>Caption as it was when earned (the lesson's title or the deed's leafLabel).</summary>
        public string label;
        public string date;
    }

    [Serializable, Preserve]
    public class CompletedLesson
    {
        public string firstDate;
        public string lastDate;
        public int count;
        /// <summary>Last day this lesson earned a star; a repeat earns one at most once a day.</summary>
        public string lastStarDate;
    }

    [Serializable, Preserve]
    public class UnfinishedLesson
    {
        public string lessonId;
        /// <summary>Position in <see cref="Lesson.steps"/> (the steps this build knows).</summary>
        public int step;
        public int contentVersion;
        public string at;
    }

    [Serializable, Preserve]
    public class PendingDeed
    {
        public string lessonId;
        public string title;
        public string leafLabel;
        public string addedDate;
    }

    [Serializable, Preserve]
    public class StreakRecord
    {
        public int count;
        public string lastDate;
    }

    /// <summary>What a completion earned — the reward screen's text depends on it.</summary>
    public struct LessonReward
    {
        public bool FirstTime;
        public bool Star;
        public bool Leaf;
    }
}
