using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>
    /// A demo lesson «Бисмиллях» installed straight into the lesson cache, built from
    /// the design stand-in art — to try the player without content on the server:
    ///
    ///   Nooria ▸ Lessons ▸ Install demo lesson, then in Play Mode
    ///   LessonPlayerScreen.Open(new LessonLaunch { Id = "demo_bismillah", InstalledOnly = true })
    ///   (or Nooria ▸ Lessons ▸ Play demo lesson).
    /// </summary>
    public static class LessonDemo
    {
        public const string Id = "demo_bismillah";
        private const string ArtDir = "Assets/UI/Lessons/Art";

        // media key → art file
        private static readonly Dictionary<string, string> Media = new()
        {
            ["cover"] = "cover_bismillah.jpg",
            ["story_1"] = "story_dinner.jpg",
            ["story_2"] = "story_shukr.jpg",
            ["quiz_tv"] = "quiz_tv.jpg",
            ["quiz_plate"] = "quiz_plate.jpg",
            ["quiz_brush"] = "quiz_brush.jpg",
            ["deed"] = "cover_bismillah.jpg",
        };

        private static readonly Dictionary<string, (string[] pages, string friend, string translit, string meaning,
            string question, string[] options, string deed, string leaf, string title)> Texts = new()
        {
            ["ru"] = (new[]
                {
                    "{childName} {m:сел|f:села} обедать. Мама улыбается: «Что мы говорим перед едой?»",
                    "«Бисмиллях!» — {m:сказал|f:сказала} {childName}. Так мы начинаем всё доброе с именем Аллаха.",
                },
                "Юсуф", "Бисмиллях", "Во имя Аллаха",
                "Что говорит {childName} перед едой?",
                new[] { "Смотрит мультик", "Говорит «Бисмиллях»", "Чистит зубы" },
                "Скажите «Бисмиллях» вместе перед обедом", "Бисмиллях перед едой", "Бисмиллях"),
            ["kz"] = (new[]
                {
                    "{childName} түскі асқа отырды. Анасы күлімсіреп: «Тамақ алдында не айтамыз?» — деді.",
                    "«Бисмиллә!» — деді {childName}. Әр жақсы істі Алланың атымен бастаймыз.",
                },
                "Жүсіп", "Бисмиллә", "Алланың атымен",
                "{childName} тамақ алдында не айтады?",
                new[] { "Мультфильм көреді", "«Бисмиллә» дейді", "Тісін тазалайды" },
                "Түскі ас алдында бірге «Бисмиллә» деңіздер", "Тамақ алдында Бисмиллә", "Бисмиллә"),
            ["en"] = (new[]
                {
                    "{childName} sat down for lunch. Mum smiled: \"What do we say before we eat?\"",
                    "\"Bismillah!\" said {childName}. We begin every good thing with the name of Allah.",
                },
                "Yusuf", "Bismillah", "In the name of Allah",
                "What does {childName} say before eating?",
                new[] { "Watches cartoons", "Says \"Bismillah\"", "Brushes teeth" },
                "Say \"Bismillah\" together before lunch", "Bismillah before meals", "Bismillah"),
        };

        [MenuItem("Nooria/Lessons/Install demo lesson")]
        public static string Install()
        {
            var dir = Path.Combine(Application.persistentDataPath, "lessons", Id, "current");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(Path.Combine(dir, LessonCache.MediaFolder));

            var media = new Dictionary<string, object>();
            var records = new Dictionary<string, object>();
            foreach (var (key, file) in Media)
            {
                var src = Path.Combine(ArtDir, file);
                File.Copy(src, Path.Combine(dir, LessonCache.MediaFolder, file), true);
                long bytes = new FileInfo(src).Length;
                media[key] = new { url = $"/api/lessons/{Id}/media/{file}", bytes };
                records[key] = new { bytes, files = new[] { file } };
            }
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                JsonConvert.SerializeObject(new { contentVersion = 1, media = records }, Formatting.Indented));

            foreach (var (lang, t) in Texts)
            {
                var lesson = new
                {
                    id = Id, lang, contentVersion = 1, free = true, topic = "food", title = t.title,
                    parentNote = "",
                    steps = new object[]
                    {
                        new { type = "story", pages = new[]
                        {
                            new { text = t.pages[0], image = "story_1" },
                            new { text = t.pages[1], image = "story_2" },
                        } },
                        new { type = "repeat", video = "friend", friendName = t.friend,
                              phrase = new { ar = "بِسْمِ ٱللَّٰهِ", translit = t.translit, meaning = t.meaning } },
                        new { type = "quiz", question = t.question, options = new[]
                        {
                            new { text = t.options[0], image = "quiz_tv", correct = false },
                            new { text = t.options[1], image = "quiz_plate", correct = true },
                            new { text = t.options[2], image = "quiz_brush", correct = false },
                        } },
                        new { type = "deed", title = t.deed, leafLabel = t.leaf, image = "deed" },
                        new { type = "reward" },
                    },
                    media,
                };
                Directory.CreateDirectory(Path.Combine(dir, lang));
                File.WriteAllText(Path.Combine(dir, lang, "lesson.json"), JsonConvert.SerializeObject(lesson, Formatting.Indented));
            }
            var msg = $"[LessonDemo] installed {Id} into {dir}";
            Debug.Log(msg);
            return msg;
        }

        [MenuItem("Nooria/Lessons/Play demo lesson")]
        public static void Play()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[LessonDemo] enter Play Mode first"); return; }
            LessonPlayerScreen.Open(new LessonLaunch
            {
                Id = Id, Title = "Бисмиллях", InstalledOnly = true,
                Cover = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/cover_bismillah.jpg"),
            });
        }
    }
}
