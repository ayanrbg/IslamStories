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
            string question, string[] options, string deed, string leaf, string title, string trace)> Texts = new()
        {
            ["ru"] = (new[]
                {
                    "{childName} {m:сел|f:села} обедать. Мама улыбается: «Что мы говорим перед едой?»",
                    "«Бисмиллях!» — {m:сказал|f:сказала} {childName}. Так мы начинаем всё доброе с именем Аллаха.",
                },
                "Юсуф", "Бисмиллях", "Во имя Аллаха",
                "Что говорит {childName} перед едой?",
                new[] { "Смотрит мультик", "Говорит «Бисмиллях»", "Чистит зубы" },
                "Скажите «Бисмиллях» вместе перед обедом", "Бисмиллях перед едой", "Бисмиллях",
                "Обведи фонарик пальчиком — и он засияет!"),
            ["kz"] = (new[]
                {
                    "{childName} түскі асқа отырды. Анасы күлімсіреп: «Тамақ алдында не айтамыз?» — деді.",
                    "«Бисмиллә!» — деді {childName}. Әр жақсы істі Алланың атымен бастаймыз.",
                },
                "Жүсіп", "Бисмиллә", "Алланың атымен",
                "{childName} тамақ алдында не айтады?",
                new[] { "Мультфильм көреді", "«Бисмиллә» дейді", "Тісін тазалайды" },
                "Түскі ас алдында бірге «Бисмиллә» деңіздер", "Тамақ алдында Бисмиллә", "Бисмиллә",
                "Шамның шетін саусағыңмен сыз — ол жарқырайды!"),
            ["en"] = (new[]
                {
                    "{childName} sat down for lunch. Mum smiled: \"What do we say before we eat?\"",
                    "\"Bismillah!\" said {childName}. We begin every good thing with the name of Allah.",
                },
                "Yusuf", "Bismillah", "In the name of Allah",
                "What does {childName} say before eating?",
                new[] { "Watches cartoons", "Says \"Bismillah\"", "Brushes teeth" },
                "Say \"Bismillah\" together before lunch", "Bismillah before meals", "Bismillah",
                "Trace the lantern with your finger, and it will light up!"),
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
            // The friend's real head, cut from the stand-in photo the way content will be made.
            var headFile = "friend_head.png";
            var headPath = Path.Combine(dir, LessonCache.MediaFolder, headFile);
            Debug.Log(FriendHeadTool.Make(Path.Combine(ArtDir, "photo_friend.jpg"), headPath));
            if (File.Exists(headPath))
            {
                long hb = new FileInfo(headPath).Length;
                media["friend_head"] = new { url = $"/api/lessons/{Id}/media/{headFile}", bytes = hb };
                records["friend_head"] = new { bytes = hb, files = new[] { headFile } };
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
                        new { type = "repeat", video = "friend", friendName = t.friend, friendHead = "friend_head", friendBody = "boy",
                              phrase = new { ar = "بِسْمِ ٱللَّٰهِ", translit = t.translit, meaning = t.meaning } },
                        new { type = "quiz", question = t.question, options = new[]
                        {
                            new { text = t.options[0], image = "quiz_tv", correct = false },
                            new { text = t.options[1], image = "quiz_plate", correct = true },
                            new { text = t.options[2], image = "quiz_brush", correct = false },
                        } },
                        new { type = "trace", text = t.trace, page = "lantern" },
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

        /// <summary>
        /// A Play Mode screenshot of the UI into <paramref name="path"/>. The game view can't
        /// capture the overlay canvas, so [UICanvas] is drawn once through a temporary camera.
        /// </summary>
        public static string Capture(string path, int width = 1792, int height = 828)
        {
            var canvas = GameObject.Find("[UICanvas]")?.GetComponent<Canvas>();
            if (canvas == null) return "no [UICanvas]";
            var mode = canvas.renderMode;
            var camGo = new GameObject("CaptureCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            return "captured " + path;
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
