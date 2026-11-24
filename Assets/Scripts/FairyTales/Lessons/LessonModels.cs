using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace FairyTales.Lessons
{
    // Lesson format of the Nooria 2.0 spec, section 8, as the server sends it
    // (Backend-Islam-Stories: services/lessonFormat.js, docs/LESSONS.md).
    //
    // Text fields stay RAW: {childName} and {m:…|f:…} are resolved where they are
    // shown (TaleText.Resolve), exactly like tale pages. The cached lesson.json then
    // survives a change of the child's name.
    //
    // [Preserve] on every class Newtonsoft builds by reflection: IL2CPP stripping
    // dropped the members of a payload type once already (see NarratePayload).

    /// <summary>GET /api/lessons?lang= — topics and the lessons of one language.</summary>
    [Serializable, Preserve]
    public class LessonCatalog
    {
        /// <summary>Content language (ru | kz | en), or null — no lessons in this app language.</summary>
        public string lang;
        public LessonTopic[] topics;
        public LessonSummary[] lessons;
    }

    [Serializable, Preserve]
    public class LessonTopic
    {
        public string id;
        public string title;
        public int sortOrder;
    }

    [Serializable, Preserve]
    public class LessonSummary
    {
        public string id;
        public string topic;
        public int sortOrder;
        public string title;
        public string coverUrl;
        public bool free;
        /// <summary>
        /// Opens for THIS user (free, subscribed on the server, or the server does not
        /// enforce access yet). Personal, like <c>TaleSummary.unlocked</c>.
        /// </summary>
        public bool unlocked;
        /// <summary>"published" | "coming_soon".</summary>
        public string status;
        public int contentVersion;
        /// <summary>Ships in StreamingAssets/BundledLessons; the server then omits downloadSize.</summary>
        public bool bundled;
        public long downloadSize;

        public bool IsComingSoon => status == "coming_soon";

        /// <summary>The same lock formula as <c>TaleSummary.IsLocked</c>.</summary>
        public bool IsLocked(bool subscribed) => !free && !subscribed && !unlocked;
    }

    /// <summary>GET /api/lessons/:id?lang= — a full lesson.</summary>
    [Serializable, Preserve]
    public class Lesson
    {
        public string id;
        public string lang;
        public int contentVersion;
        public bool free;
        public string topic;
        public string title;
        public string parentNote;

        /// <summary>
        /// Steps the client knows, in order. A step of an unknown type is left out
        /// here (spec §8: future steps must not break old builds), but every step
        /// keeps its position in the server's list in <see cref="LessonStep.index"/> —
        /// narration keys are counted over that list.
        /// </summary>
        [JsonConverter(typeof(LessonStepListConverter))]
        public List<LessonStep> steps = new();

        /// <summary>Media key → where to download it. Keys come from the step fields.</summary>
        public Dictionary<string, LessonMedia> media = new();
    }

    [Serializable, Preserve]
    public class LessonMedia
    {
        /// <summary>Single-variant file (server-relative path).</summary>
        public string url;
        /// <summary>Gendered pair — both set, or neither (the server fills a missing half).</summary>
        public string boy;
        public string girl;
        /// <summary>Size of all variants together.</summary>
        public long bytes;

        [JsonIgnore] public bool IsGendered => !string.IsNullOrEmpty(boy) || !string.IsNullOrEmpty(girl);

        /// <summary>Server path of the variant for this child ("male" | "female").</summary>
        public string UrlFor(string gender)
        {
            if (!IsGendered) return url;
            return gender == "female" ? (girl ?? boy) : (boy ?? girl);
        }

        /// <summary>Every distinct server path — a cached lesson keeps both genders.</summary>
        public IEnumerable<string> AllUrls()
        {
            if (!IsGendered)
            {
                if (!string.IsNullOrEmpty(url)) yield return url;
                yield break;
            }
            if (!string.IsNullOrEmpty(boy)) yield return boy;
            if (!string.IsNullOrEmpty(girl) && girl != boy) yield return girl;
        }
    }

    // ─────────────────────────── steps ───────────────────────────

    public static class LessonStepType
    {
        public const string Story = "story";
        public const string Repeat = "repeat";
        public const string Quiz = "quiz";
        public const string Deed = "deed";
        public const string Reward = "reward";
    }

    [Serializable, Preserve]
    public abstract class LessonStep
    {
        public string type;

        /// <summary>Position in the server's step list, unknown types included.</summary>
        [JsonIgnore] public int index;
    }

    [Serializable, Preserve]
    public class StoryStep : LessonStep
    {
        public List<StoryPage> pages = new();
    }

    [Serializable, Preserve]
    public class StoryPage
    {
        public string text;
        /// <summary>Media key.</summary>
        public string image;
    }

    [Serializable, Preserve]
    public class RepeatStep : LessonStep
    {
        /// <summary>Media key of the friend's video.</summary>
        public string video;
        public string friendName;
        public LessonPhrase phrase = new();
    }

    [Serializable, Preserve]
    public class LessonPhrase
    {
        /// <summary>Arabic, the same in every language. Shown through ShapedText.</summary>
        public string ar;
        public string translit;
        public string meaning;
    }

    [Serializable, Preserve]
    public class QuizStep : LessonStep
    {
        public string question;
        /// <summary>In the server's order. The player shuffles a copy; narration keys follow this order.</summary>
        public List<QuizOption> options = new();

        public int CorrectIndex => options.FindIndex(o => o != null && o.correct);
    }

    [Serializable, Preserve]
    public class QuizOption
    {
        public string text;
        /// <summary>Media key, square picture.</summary>
        public string image;
        public bool correct;
    }

    [Serializable, Preserve]
    public class DeedStep : LessonStep
    {
        public string title;
        /// <summary>Caption of the leaf on the Tree once a parent marks the deed.</summary>
        public string leafLabel;
        /// <summary>Media key, optional.</summary>
        public string image;
    }

    [Serializable, Preserve]
    public class RewardStep : LessonStep { }

    /// <summary>
    /// Reads the step array, picking the class by "type" and dropping types this
    /// build does not know. Write is the plain array: the cache stores the server's
    /// JSON as received, so this converter never writes in practice.
    /// </summary>
    [Preserve]
    public class LessonStepListConverter : JsonConverter<List<LessonStep>>
    {
        public override List<LessonStep> ReadJson(JsonReader reader, Type objectType,
            List<LessonStep> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var list = new List<LessonStep>();
            if (reader.TokenType == JsonToken.Null) return list;

            var array = JArray.Load(reader);
            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] is not JObject obj) continue;
                LessonStep step = ((string)obj["type"]) switch
                {
                    LessonStepType.Story => obj.ToObject<StoryStep>(serializer),
                    LessonStepType.Repeat => obj.ToObject<RepeatStep>(serializer),
                    LessonStepType.Quiz => obj.ToObject<QuizStep>(serializer),
                    LessonStepType.Deed => obj.ToObject<DeedStep>(serializer),
                    LessonStepType.Reward => obj.ToObject<RewardStep>(serializer),
                    _ => null,
                };
                if (step == null) continue;
                step.index = i;
                list.Add(step);
            }
            return list;
        }

        public override void WriteJson(JsonWriter writer, List<LessonStep> value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            if (value != null)
                foreach (var step in value)
                    serializer.Serialize(writer, step, step.GetType());
            writer.WriteEndArray();
        }
    }

    // ─────────────────────────── narration ───────────────────────────

    /// <summary>
    /// Body of POST /api/lessons/:id/narrate. A named class on purpose, see
    /// <c>NarratePayload</c>: an anonymous type serialized to "{}" under IL2CPP.
    /// </summary>
    [Serializable, Preserve]
    public class LessonNarrateRequest
    {
        public string lang;
        public string childName;
        public string gender;
        public string narratorGender;
    }

    [Serializable, Preserve]
    public class LessonNarration
    {
        public int contentVersion;
        public string narratorGender;
        /// <summary>Narration key → server path of the mp3.</summary>
        public Dictionary<string, string> audio = new();
        /// <summary>Keys the server failed to synthesize: the player shows "Next" without sound there.</summary>
        public List<string> missing = new();
    }

    /// <summary>
    /// Keys of the texts the server narrates (spec §8). Step numbers are positions in
    /// the server's list (<see cref="LessonStep.index"/>), pages and options from zero.
    /// </summary>
    public static class NarrationKey
    {
        public static string Page(StoryStep step, int page) => $"s{step.index}.p{page}";
        public static string Question(QuizStep step) => $"s{step.index}.q";
        public static string Option(QuizStep step, int option) => $"s{step.index}.o{option}";
        public static string Deed(DeedStep step) => $"s{step.index}.deed";
    }
}
