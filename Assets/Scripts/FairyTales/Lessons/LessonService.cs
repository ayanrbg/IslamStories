using System;
using System.Collections;
using FairyTales.Api;
using Newtonsoft.Json;

namespace FairyTales.Lessons
{
    /// <summary>Why a lesson request failed — the player reacts differently to each.</summary>
    public enum LessonFailure
    {
        None,
        /// <summary>No connection, timeout, 5xx.</summary>
        Network,
        /// <summary>403 subscription_required: paid lesson, no subscription on the server.</summary>
        Locked,
        /// <summary>404: the lesson is gone or has no translation in this language.</summary>
        NotFound,
        /// <summary>The body did not parse.</summary>
        Malformed,
    }

    /// <summary>
    /// Lesson endpoints of Backend-Islam-Stories (docs/LESSONS.md). Every call takes
    /// the APP language (Loc.Lang): the server maps en-US / en-GB to the English text
    /// but narrates it in the voice of the app's variant.
    /// </summary>
    public class LessonService
    {
        private readonly ApiClient _api;

        public LessonService(ApiClient api) => _api = api;

        public ApiClient Api => _api;

        public IEnumerator GetCatalog(string appLang,
            Action<LessonCatalog> onSuccess, Action<LessonFailure, string> onError = null)
        {
            string json = null, error = null;
            yield return _api.Get($"/api/lessons?lang={Uri.EscapeDataString(appLang ?? "")}",
                j => json = j, e => error = e);
            if (json == null) { onError?.Invoke(Classify(error), error); yield break; }

            LessonCatalog catalog = null;
            yield return JsonAsync.Parse<LessonCatalog>(json, c => catalog = c);
            if (catalog == null) { onError?.Invoke(LessonFailure.Malformed, "malformed catalog"); yield break; }
            catalog.topics ??= Array.Empty<LessonTopic>();
            catalog.lessons ??= Array.Empty<LessonSummary>();
            onSuccess?.Invoke(catalog);
        }

        /// <summary>
        /// Full lesson. <paramref name="onSuccess"/> also gets the raw JSON — the cache
        /// stores exactly what the server sent.
        /// </summary>
        public IEnumerator GetLesson(string lessonId, string appLang,
            Action<Lesson, string> onSuccess, Action<LessonFailure, string> onError = null)
        {
            string json = null, error = null;
            yield return _api.Get($"/api/lessons/{lessonId}?lang={Uri.EscapeDataString(appLang ?? "")}",
                j => json = j, e => error = e);
            if (json == null) { onError?.Invoke(Classify(error), error); yield break; }

            Lesson lesson = null;
            yield return JsonAsync.Parse<Lesson>(json, l => lesson = l);
            if (lesson == null || string.IsNullOrEmpty(lesson.id))
            {
                onError?.Invoke(LessonFailure.Malformed, "malformed lesson");
                yield break;
            }
            onSuccess?.Invoke(lesson, json);
        }

        /// <summary>
        /// Narration of every personalized text in one request. Slow on a cold server
        /// cache (TTS per phrase), so it runs alongside the media download.
        /// </summary>
        public IEnumerator Narrate(string lessonId, string appLang, string childName,
            string gender, string narratorGender,
            Action<LessonNarration> onSuccess, Action<LessonFailure, string> onError = null)
        {
            var body = JsonConvert.SerializeObject(new LessonNarrateRequest
            {
                lang = appLang,
                childName = childName,
                gender = gender,
                narratorGender = narratorGender,
            }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            string json = null, error = null;
            yield return _api.PostJson($"/api/lessons/{lessonId}/narrate", body,
                j => json = j, e => error = e);
            if (json == null) { onError?.Invoke(Classify(error), error); yield break; }

            LessonNarration narration = null;
            yield return JsonAsync.Parse<LessonNarration>(json, n => narration = n);
            if (narration == null) { onError?.Invoke(LessonFailure.Malformed, "malformed narration"); yield break; }
            narration.audio ??= new();
            narration.missing ??= new();
            onSuccess?.Invoke(narration);
        }

        /// <summary>ApiClient formats errors as "[code] error — body".</summary>
        internal static LessonFailure Classify(string error)
        {
            if (string.IsNullOrEmpty(error)) return LessonFailure.Network;
            if (error.StartsWith("[403]")) return LessonFailure.Locked;
            if (error.StartsWith("[404]")) return LessonFailure.NotFound;
            return LessonFailure.Network;
        }
    }
}
