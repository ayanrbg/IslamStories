using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static FairyTales.Lessons.EditorTools.UI;
using TA = TMPro.TextAlignmentOptions;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>
    /// Design screens of Nooria 2.0 (release 1 and 2), built in code as real prefabs at
    /// 1920×1080 and rendered to PNG. The prefabs are the starting point for the lesson
    /// player and the new sections: layout, art and texts, no behaviour yet.
    ///
    ///   Nooria ▸ Lessons ▸ Build design screens  — prefabs into Assets/Prefabs/Lessons/Design
    ///   Nooria ▸ Lessons ▸ Render design PNGs    — Design/Nooria2/*.png in the project root
    ///
    /// Art is in Assets/UI/Lessons/Art (generated stand-ins until the artist draws the
    /// final set). Rebuilding overwrites the prefabs: tweak by hand only once the
    /// builder is retired.
    /// </summary>
    public static class LessonDesignScreens
    {
        private const string ArtDir = "Assets/UI/Lessons/Art";
        private const string PrefabDir = "Assets/Prefabs/Lessons/Design";
        private const string PngDir = "Design/Nooria2";

        private const string Child = "Айша";
        private const string ChildGen = "Айши";

        private static readonly (string id, string title, Action<Box> build)[] Screens =
        {
            ("01_Library_LessonOfDay", "Р1 · Библиотека сказок с «Уроком дня»", S_Library),
            ("02_Lesson_Prepare", "Р1 · Подготовка урока", S_Prepare),
            ("03_Step1_Story", "Р1 · Шаг 1 · История", S_Story),
            ("04_Step2_FriendIntro", "Р1 · Шаг 2 · Друг показывает фразу", S_FriendIntro),
            ("05_Step2_Repeat_AR", "Р1 · Шаг 2 · Повтори как друг (AR-маска)", S_RepeatAR),
            ("06_Step2_ListenYourself", "Р1 · Шаг 2 · Послушай себя", S_Listen),
            ("07_Step2_CharacterMode", "Р1 · Шаг 2 · Режим без камеры", S_CharacterMode),
            ("08_Step3_Quiz", "Р1 · Шаг 3 · Задание", S_Quiz),
            ("09_Step4_Deed", "Р1 · Шаг 4 · Сделай в жизни", S_Deed),
            ("10_Step5_Reward", "Р1 · Шаг 5 · Награда", S_Reward),
            ("11_Lesson_ExitConfirm", "Р1 · Выход из урока", S_Exit),
            ("12_Permissions_Parent", "Р1 · Камера и микрофон (для родителя)", S_Permissions),
            ("13_Avatar_Create", "Р1 · Создание аватара", S_Avatar),
            ("14_Tree_Simple", "Р1 · Дерево добрых дел", S_TreeSimple),
            ("15_SoftOffer", "Р1 · Предложение подписки после 2-го урока", S_SoftOffer),
            ("16_Home_Learn", "Р2 · Главная «Учимся»", S_Home),
            ("17_Tree_Growing", "Р2 · Растущее Дерево", S_TreeGrowing),
            ("18_Tales_Evening", "Р2 · Вкладка «Сказки» вечером", S_TalesEvening),
            ("19_Parents", "Р2 · Раздел «Родителям»", S_Parents),
            ("20_Family_Avatars", "Р2 · Семья в аватарах", S_Family),
            ("21_Task_Trace", "Р2 · Задание «Обведи»", S_Trace),
            ("22_Task_Puzzle", "Р2 · Задание «Собери картинку»", S_Puzzle),
            ("23_Onboarding_Knows", "Р2 · Анкета: что ребёнок уже знает", S_Questionnaire),
            ("24_Onboarding_Plan", "Р2 · План для ребёнка", S_Plan),
            ("25_Paywall_Trial", "Р2 · Пейволл с пробным периодом", S_Paywall),
        };

        // ───────────────────────────── menu ─────────────────────────────

        [MenuItem("Nooria/Lessons/Build design screens")]
        public static void BuildAll()
        {
            Shapes.Ensure();
            ImportArt();
            LoadFonts();
            Directory.CreateDirectory(PrefabDir);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("Holder", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(holder, scene);
                foreach (var (id, _, build) in Screens)
                {
                    var root = Root(id, holder.transform);
                    build(root);
                    PrefabUtility.SaveAsPrefabAsset(root.Go, $"{PrefabDir}/{id}.prefab");
                    UnityEngine.Object.DestroyImmediate(root.Go);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[LessonDesign] built {Screens.Length} prefabs in {PrefabDir}");
        }

        [MenuItem("Nooria/Lessons/Render design PNGs")]
        public static void RenderAll() => Render(null);

        public static int Count => Screens.Length;

        /// <summary>Builds and renders screens [from, from+count) — small batches for automation with a call time limit.</summary>
        public static string BuildAndRender(int from, int count)
        {
            Shapes.Ensure();
            if (from == 0) ImportArt();
            LoadFonts();
            Directory.CreateDirectory(PrefabDir);
            var scene = EditorSceneManager.NewPreviewScene();
            var ids = new List<string>();
            try
            {
                var holder = new GameObject("Holder", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(holder, scene);
                for (int i = from; i < Math.Min(Screens.Length, from + count); i++)
                {
                    var (id, _, build) = Screens[i];
                    var root = Root(id, holder.transform);
                    build(root);
                    PrefabUtility.SaveAsPrefabAsset(root.Go, $"{PrefabDir}/{id}.prefab");
                    UnityEngine.Object.DestroyImmediate(root.Go);
                    ids.Add(id);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            foreach (var id in ids) Render(id);
            return string.Join(",", ids);
        }

        /// <summary>Renders the prefabs (all, or those whose id starts with <paramref name="only"/>).</summary>
        public static string Render(string only)
        {
            Directory.CreateDirectory(PngDir);
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            int done = 0;
            try
            {
                var camGo = new GameObject("Cam", typeof(Camera));
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.GetComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.white;
                cam.orthographic = true;
                cam.targetTexture = rt;
                cam.transform.position = new Vector3(0, 0, -10);

                var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                foreach (var (id, _, _) in Screens)
                {
                    if (only != null && !id.StartsWith(only)) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{id}.prefab");
                    if (prefab == null) continue;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    inst.transform.SetParent(canvasGo.transform, false);
                    ShapeArabic(inst);
                    Canvas.ForceUpdateCanvases();
                    foreach (var t in inst.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(PngDir, id + ".png"), tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(inst);
                    done++;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
            var msg = $"[LessonDesign] rendered {done} PNGs into {Path.GetFullPath(PngDir)}";
            Debug.Log(msg);
            return msg;
        }

        /// <summary>The app shapes Arabic at runtime (ShapedText); the render has to do the same.</summary>
        private static void ShapeArabic(GameObject root)
        {
            var type = Type.GetType("FairyTales.UI.Core.ShapedText, Assembly-CSharp");
            var attach = type?.GetMethod("Attach", BindingFlags.NonPublic | BindingFlags.Static);
            if (attach == null) return;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) attach.Invoke(null, new object[] { t });
        }

        // ───────────────────────────── art ─────────────────────────────

        private static void ImportArt()
        {
            foreach (var path in Directory.GetFiles(ArtDir))
            {
                if (path.EndsWith(".meta")) continue;
                var p = path.Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                // A 2D project imports new PNGs as Multiple sprites; a sheet with stale or no
                // slices then renders blank. Every art file is one picture.
                if (ti == null || (ti.textureType == TextureImporterType.Sprite
                                   && ti.spriteImportMode == SpriteImportMode.Single)) continue;
                Shapes.Import(p, 0, 2048, compress: true);
            }
        }

        private static readonly Dictionary<string, Sprite> ArtCache = new();

        private static Sprite Art(string name)
        {
            if (ArtCache.TryGetValue(name, out var s) && s != null) return s;
            s = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.jpg");
            if (s == null) Debug.LogWarning($"[LessonDesign] no art '{name}'");
            ArtCache[name] = s;
            return s;
        }

        private static Sprite AppSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // Where each character's face is in its sprite (0 top … 1 bottom) and how wide to crop.
        private static readonly Dictionary<string, (float headY, float zoom)> Heads = new()
        {
            ["char_girl"] = (0.17f, 1.75f),
            ["char_boy"] = (0.17f, 1.9f),
            ["char_mom"] = (0.12f, 2.6f),
            ["char_friend"] = (0.2f, 1.9f),
            ["char_dad"] = (0.12f, 2.6f),
            ["char_grandma"] = (0.13f, 2.5f),
            ["char_grandpa"] = (0.12f, 2.6f),
        };

        private static Box Head(Box p, string ch, float cx, float cy, float d, Color bg, A a = A.C, float ring = 0)
        {
            var (hy, z) = Heads.TryGetValue(ch, out var v) ? v : (0.2f, 1.8f);
            return Bust(p, "Avatar " + ch, Art(ch), cx, cy, d, bg, hy, z, a, ring);
        }

        // ───────────────────────────── shared pieces ─────────────────────────────

        private static void Bg(Box r, string name, Color top, Color bottom)
        {
            var b = Fill(r, "Background");
            AddImage(b, Shapes.Gradient(name, top, bottom), Color.white);
        }

        /// <summary>✕, the 5-segment progress bar of the lesson player.</summary>
        private static void PlayerTop(Box r, int step, bool onDark = false)
        {
            IconButton(r, "Close", Shapes.Cross, new Color(1, 1, 1, onDark ? 0.92f : 1f), Pal.Sub, 90, 78, 92, A.TL, 0.4f);
            var bar = Node(r, "StepProgress", 960, 78, 5 * 150 + 4 * 16, 22, A.T);
            for (int i = 0; i < 5; i++)
                Rect(bar, $"Segment{i + 1}", i < step ? Pal.Rose : new Color(1, 1, 1, onDark ? 0.35f : 0.75f),
                    75 + i * 166, 11, 150, 22, 11);
        }

        private static void Stats(Box r, int stars, int days, float cy = 78, A a = A.TR, float right = 1830)
        {
            Pill(r, Art("icon_flame"), $"{days} дня", right - 110, cy, 220, a);
            Pill(r, Art("reward_star"), stars.ToString(), right - 330, cy, 180, a);
        }

        private static void BottomNav(Box r, int active, bool dark = false)
        {
            var w = 1040f;
            Shadow(r, 960, 1000, w, 124, 0.16f, 10, A.B);
            var bar = Rect(r, "BottomNav", dark ? Pal.NightCard : Color.white, 960, 1000, w, 124, 62, A.B);
            var tabs = new[] { ("Учимся", "topic_dua"), ("Дерево", "tree_stage4"), ("Сказки", "topic_sleep"), ("Родителям", "topic_family") };
            for (int i = 0; i < tabs.Length; i++)
            {
                float x = w / 8 + i * w / 4;
                bool on = i == active;
                var tab = Node(bar, "Tab " + tabs[i].Item1, x, 62, w / 4, 124);
                tab.Go.AddComponent<UnityEngine.UI.Button>();
                if (on) Rect(tab, "Active", dark ? Pal.Night2 : Pal.RoseSoft, w / 8, 62, w / 4 - 30, 100, 50);
                Img(tab, "Icon", Art(tabs[i].Item2), 58, 62, 64, 64);
                Txt(tab, tabs[i].Item1, FontXB, 28, on ? (dark ? Color.white : Pal.Rose) : (dark ? Pal.Lav : Pal.Sub),
                    184, 62, 160, 60, TA.Left);
            }
        }

        private static Box Bubble(Box p, string text, float cx, float cy, float w, float h, float size = 40, bool tailLeft = true)
        {
            Shadow(p, cx, cy, w, h, 0.12f, 10);
            var b = Rect(p, "SpeechBubble", Color.white, cx, cy, w, h, 40);
            var tail = Rect(p, "Tail", Color.white, cx + (tailLeft ? -w / 2 + 70 : w / 2 - 70), cy + h / 2 + 6, 44, 44, 8);
            tail.Rt.localRotation = Quaternion.Euler(0, 0, 45);
            b.Rt.SetAsLastSibling();
            Txt(b, text, FontXB, size, Pal.Ink, w / 2, h / 2, w - 60, h - 20);
            return b;
        }

        private static void Phrase(Box p, float cx, float cy, float w, bool meaning = true)
        {
            var card = Card(p, "PhraseCard", cx, cy, w, meaning ? 250 : 210, 48);
            Txt(card, "بِسْمِ ٱللَّٰهِ", FontAr, 84, Pal.LavDeep, w / 2, 82, w - 40, 120);
            Txt(card, "Бисмиллях", FontXB, 54, Pal.Ink, w / 2, 170, w - 40, 70);
            if (meaning) Txt(card, "«Во имя Аллаха»", FontM, 32, Pal.Sub, w / 2, 220, w - 40, 44);
        }

        private static void Mic(Box p, float cx, float cy, float d, bool listening)
        {
            if (listening)
            {
                Circle(p, "Pulse2", new Color(Pal.Rose.r, Pal.Rose.g, Pal.Rose.b, 0.12f), cx, cy, d * 1.75f);
                Circle(p, "Pulse1", new Color(Pal.Rose.r, Pal.Rose.g, Pal.Rose.b, 0.22f), cx, cy, d * 1.38f);
            }
            Shadow(p, cx, cy, d * 0.8f, d * 0.8f, 0.2f, 12);
            var b = Circle(p, "MicButton", Pal.Rose, cx, cy, d);
            b.Go.AddComponent<UnityEngine.UI.Button>();
            Img(b, "Icon", Art("icon_mic"), d / 2, d / 2, d * 0.62f, d * 0.62f);
        }

        private static void Wave(Box p, float cx, float cy, float w, float h, Color c, int seed)
        {
            var rnd = new System.Random(seed);
            var holder = Node(p, "Waveform", cx, cy, w, h);
            int n = (int)(w / 22);
            for (int i = 0; i < n; i++)
            {
                float k = Mathf.Sin(i / (float)n * Mathf.PI);
                float bh = Mathf.Max(14, h * k * (0.35f + 0.65f * (float)rnd.NextDouble()));
                Rect(holder, "Bar", c, 11 + i * 22, h / 2, 12, bh, 6);
            }
        }

        private static void CameraLabel(Box p, string text, float cx, float cy, Color dot)
        {
            var w = text.Length * 19 + 90;
            var b = Rect(p, "Label", new Color(1, 1, 1, 0.94f), cx + w / 2, cy, w, 64, 32, A.TL);
            Circle(b, "Dot", dot, 36, 32, 20);
            Txt(b, text, FontXB, 30, Pal.Ink, w / 2 + 18, 31, w - 60, 60);
        }

        private static void MaskPicker(Box p, float cx, float cy, int active)
        {
            var names = new[] { "mask_crown", "mask_fanous", "mask_stars" };
            for (int i = 0; i < 3; i++)
            {
                float x = cx, y = cy + (i - 1) * 120;
                if (i == active) Circle(p, "Selected", Pal.Rose, x, y, 112);
                var c = Circle(p, "Mask " + names[i], Color.white, x, y, 100);
                c.Go.AddComponent<UnityEngine.UI.Button>();
                Img(c, "Icon", Art(names[i]), 50, 50, 74, 74);
            }
        }

        private static void Check(Box p, float cx, float cy, float d, Color c)
        {
            var b = Circle(p, "Check", c, cx, cy, d);
            Img(b, "Mark", Shapes.Check, d / 2, d / 2 + 2, d * 0.62f, d * 0.62f, A.C, Color.white);
        }

        // ───────────────────────────── release 1 ─────────────────────────────

        private static void S_Library(Box r)
        {
            // The tales library keeps its night look; the lesson block sits on top of it.
            AddImage(Fill(r, "Background"), AppSprite("Assets/UI/main-back.png"), Color.white);
            Txt(r, "Сказки", FontXB, 64, Color.white, 300, 92, 500, 90, TA.Left, A.TL);
            IconButton(r, "Settings", Shapes.Gear, Pal.NightCard, Pal.Lav, 1800, 92, 96, A.TR);
            Stats(r, 12, 3, 92, A.TR, 1720);

            // Lesson of the day
            var card = Card(r, "LessonOfDay", 360, 560, 600, 760, 56, Pal.Peach, 0.3f, A.L);
            Txt(card, "УРОК ДНЯ", FontXB, 30, Pal.Rose, 260, 62, 400, 50, TA.Left);
            Cover(card, "Cover", Art("cover_bismillah"), 300, 290, 520, 360, 40);
            Txt(card, "Бисмиллях", FontXB, 58, Pal.Ink, 300, 530, 480, 80, TA.Left);
            Txt(card, "5 минут · история, повтори как друг, задание", FontM, 28, Pal.Sub, 300, 600, 480, 70, TA.TopLeft);
            Button(card, "Начать", Pal.Rose, 300, 690, 520, 100, A.C, null, 44, Shapes.Triangle);
            Head(card, "char_girl", 530, 60, 100, Pal.Lav, A.C, 6);


            // Tales grid
            var tales = new[] { "tale_firefly_nur", "tale_ramadan_moon", "tale_warm_bread", "tale_dua_garden", "tale_noah_ark", "tale_prayer_lamp", "tale_camel_tear", "tale_wishes_garden" };
            var titles = new[] { "Светлячок Нур", "Луна Рамадана", "Тёплый хлеб", "Сад дуа", "Ковчег", "Лампа молитвы", "Слеза верблюда", "Сад желаний" };
            var grid = Node(r, "TalesGrid", 1330, 590, 1100, 820, A.R);
            for (int i = 0; i < tales.Length; i++)
            {
                int col = i % 4, row = i / 4;
                float x = 130 + col * 280, y = 190 + row * 410;
                var c = Node(grid, "Tale " + titles[i], x, y, 250, 380);
                Img(c, "Cover", Art(tales[i]), 125, 140, 250, 272);
                Txt(c, titles[i], FontB, 28, Color.white, 125, 310, 250, 60);
                if (i >= 1) Img(c, "Lock", AppSprite("Assets/Resources/Covers/padlock.png"), 205, 50, 62, 62);
            }
            var treeBtn = Card(r, "TreeButton", 360, 1000, 600, 110, 55, Color.white, 0.25f, A.BL);
            Img(treeBtn, "Tree", Art("tree_full"), 70, 55, 96, 96);
            Txt(treeBtn, "Дерево добрых дел", FontXB, 36, Pal.Ink, 330, 40, 420, 50, TA.Left);
            Txt(treeBtn, "3 листка · ещё 2 до новой сказки", FontM, 26, Pal.Sub, 330, 80, 420, 40, TA.Left);
            Img(treeBtn, "Go", Shapes.Arrow, 556, 55, 40, 40, A.C, Pal.Sub).Rt.localScale = new Vector3(-1, 1, 1);
        }

        private static void S_Prepare(Box r)
        {
            Bg(r, "lav_peach", Pal.Lav, Pal.Peach);
            IconButton(r, "Close", Shapes.Cross, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.4f);
            Img(r, "Girl", Art("char_girl"), 330, 640, 420, 700, A.BL);
            Img(r, "Mom", Art("char_mom"), 1600, 600, 440, 820, A.BR);
            var card = Card(r, "Card", 960, 520, 820, 760, 60);
            Cover(card, "Cover", Art("cover_bismillah"), 410, 250, 700, 400, 44);
            Txt(card, "Бисмиллях", FontXB, 72, Pal.Ink, 410, 520, 700, 90);
            Txt(card, "Готовим урок…", FontB, 34, Pal.Sub, 410, 590, 700, 50);
            Progress(card, 410, 660, 600, 30, 0.64f, Pal.Rose, Pal.LavSoft);
            Txt(card, "64%", FontXB, 30, Pal.Rose, 410, 712, 200, 40);
        }

        private static void S_Story(Box r)
        {
            Cover(r, "Illustration", Art("story_dinner"), 960, 540, 1920, 1080, 0, A.Fill);
            var shade = Fill(r, "Shade");
            AddImage(shade, Shapes.Gradient("story_shade", new Color(0, 0, 0, 0), new Color(0.1f, 0.06f, 0.25f, 0.55f)), Color.white);
            PlayerTop(r, 1, true);
            Pill(r, null, "1 / 2", 1800, 78, 150, A.TR, new Color(1, 1, 1, 0.92f), Pal.Sub, 70);

            var card = Card(r, "TextCard", 900, 905, 1300, 230, 50, null, 0.25f, A.B);
            Txt(card, $"<color=#E0577B>{Child}</color> села обедать. Мама улыбается: «Что мы говорим перед едой?»",
                FontB, 46, Pal.Ink, 650, 115, 1180, 200);
            IconButton(r, "Listen", Shapes.Speaker, Color.white, Pal.LavDeep, 120, 905, 140, A.BL, 0.5f);
            Button(r, "Дальше", Pal.Rose, 1720, 905, 300, 120, A.BR, null, 46);
        }

        private static void S_FriendIntro(Box r)
        {
            Cover(r, "Landscape", Art("bg_day"), 960, 540, 1920, 1080, 0, A.Fill);
            PlayerTop(r, 2);
            Img(r, "Friend", Art("char_friend"), 640, 700, 470, 700, A.B);
            Img(r, "Child", Art("char_girl"), 1300, 710, 430, 680, A.B);
            Txt(r, "Юсуф", FontXB, 40, Pal.Ink, 640, 1050, 300, 50, TA.Center, A.B);
            Txt(r, $"{Child}", FontXB, 40, Pal.Ink, 1300, 1050, 300, 50, TA.Center, A.B);
            Bubble(r, "Скажи вместе со мной!", 470, 270, 520, 130, 40);
            Phrase(r, 1180, 250, 640, true);
            Button(r, "Смотреть Юсуфа", Pal.Rose, 1640, 960, 440, 110, A.BR, null, 40, Shapes.Triangle);
        }

        private static void RepeatLayout(Box r, int step, out Box friend, out Box me)
        {
            Bg(r, "yellow", Pal.Hex("FFE9A0"), Pal.Hex("FFD45A"));
            PlayerTop(r, step);
            friend = Cover(r, "FriendVideo", Art("photo_friend"), 500, 450, 800, 560, 48, A.L);
            CameraLabel(friend, "Юсуф", 24, 50, Pal.Green);
            Img(friend, "PlayBadge", Shapes.Triangle, 760, 520, 40, 40, A.C, new Color(1, 1, 1, 0.9f));
            me = Cover(r, "ChildCamera", Art("photo_selfie"), 1420, 450, 800, 560, 48, A.R);
        }

        private static void S_RepeatAR(Box r)
        {
            RepeatLayout(r, 2, out var friend, out var me);
            Bubble(friend, "Бисмиллях!", 610, 110, 320, 100, 40, false);
            Img(me, "ARMask", Art("mask_crown"), 410, 92, 320, 240);
            CameraLabel(me, $"Это ты, {Child}", 24, 50, Pal.Rose);
            MaskPicker(me, 720, 280, 0);
            // phrase + mic
            var p = Node(r, "Bottom", 960, 900, 1920, 300, A.B);
            Txt(p, "Бисмиллях", FontXB, 66, Pal.Ink, 500, 120, 600, 90, TA.Right);
            Txt(p, "بِسْمِ ٱللَّٰهِ", FontAr, 70, Pal.LavDeep, 500, 210, 600, 100, TA.Right);
            Mic(p, 960, 160, 190, true);
            Txt(p, "Скажи вместе с другом!", FontXB, 40, Pal.Ink, 1380, 160, 600, 100, TA.Left);
        }

        private static void S_Listen(Box r)
        {
            RepeatLayout(r, 2, out var friend, out var me);
            Img(me, "ARMask", Art("mask_crown"), 410, 92, 320, 240);
            CameraLabel(me, $"Это ты, {Child}", 24, 50, Pal.Rose);
            var p = Node(r, "Bottom", 960, 900, 1920, 300, A.B);
            var card = Card(p, "ListenCard", 960, 150, 1100, 210, 105);
            IconButton(card, "Play", Shapes.Triangle, Pal.Rose, Color.white, 120, 105, 150, A.C, 0.42f);
            Txt(card, "Послушай себя!", FontXB, 48, Pal.Ink, 520, 62, 600, 70, TA.Left);
            Wave(card, 600, 140, 700, 90, Pal.Rose, 7);
            Txt(p, "Машаллах!", FontXB, 44, Pal.GreenDark, 1680, 120, 400, 60);
            Button(p, "Ещё раз", Color.white, 1680, 210, 300, 96, A.C, Pal.Ink, 38);
        }

        private static void S_CharacterMode(Box r)
        {
            Bg(r, "yellow", Pal.Hex("FFE9A0"), Pal.Hex("FFD45A"));
            PlayerTop(r, 2);
            var friend = Cover(r, "FriendVideo", Art("photo_friend"), 500, 450, 800, 560, 48, A.L);
            CameraLabel(friend, "Юсуф", 24, 50, Pal.Green);
            var me = Card(r, "Character", 1420, 450, 800, 560, 48, Pal.Hex("FAD3DE"), 0.16f, A.R);
            Img(me, "Glow", Shapes.Radial("charglow", new Color(1, 1, 1, 0.8f), new Color(1, 1, 1, 0)), 400, 300, 600, 600);
            Img(me, "Avatar", Art("char_girl"), 380, 320, 320, 500);
            Img(me, "Mask", Art("mask_crown"), 372, 98, 170, 128);
            CameraLabel(me, "Без камеры", 24, 50, Pal.LavDeep);
            Txt(me, "Рот двигается,\nкогда ты говоришь", FontB, 28, Pal.Sub, 660, 470, 240, 90);
            var p = Node(r, "Bottom", 960, 900, 1920, 300, A.B);
            Txt(p, "Бисмиллях", FontXB, 66, Pal.Ink, 500, 120, 600, 90, TA.Right);
            Txt(p, "بِسْمِ ٱللَّٰهِ", FontAr, 70, Pal.LavDeep, 500, 210, 600, 100, TA.Right);
            Mic(p, 960, 160, 190, false);
            Button(p, "Я сказала!", Pal.Green, 1460, 160, 420, 110, A.C, null, 42);
        }

        private static void S_Quiz(Box r)
        {
            Bg(r, "sky", Pal.Hex("DCEFFD"), Pal.Sky);
            PlayerTop(r, 3);
            Txt(r, $"Что делает {Child} перед едой?", FontXB, 62, Pal.Ink, 960, 210, 1400, 90, TA.Center, A.T);
            IconButton(r, "Listen", Shapes.Speaker, Color.white, Pal.LavDeep, 1720, 210, 100, A.T, 0.5f);
            var items = new[] { ("quiz_tv", "Смотрит мультик"), ("quiz_plate", "Говорит «Бисмиллях»"), ("quiz_ball", "Играет в мяч") };
            for (int i = 0; i < 3; i++)
            {
                float x = 960 + (i - 1) * 520;
                bool ok = i == 1, wrong = i == 0;
                if (ok) Rect(r, "CorrectGlow", Pal.Green, x, 600, 470, 540, 54);
                var c = Card(r, "Option " + (i + 1), x, 600, ok ? 450 : 450, ok ? 520 : 520, 46, null, ok ? 0 : 0.14f);
                c.Go.AddComponent<UnityEngine.UI.Button>();
                Cover(c, "Picture", Art(items[i].Item1), 225, 205, 390, 340, 34);
                Txt(c, items[i].Item2, FontXB, 36, Pal.Ink, 225, 445, 400, 100);
                if (wrong) c.Go.AddComponent<CanvasGroup>().alpha = 0.45f;
                if (ok) Check(r, x + 190, 360, 96, Pal.Green);
            }
            var toast = Rect(r, "Toast", Pal.Green, 960, 975, 620, 104, 52, A.B);
            Txt(toast, "Машаллах, правильно!", FontXB, 44, Color.white, 310, 50, 600, 90);
        }

        private static void S_Deed(Box r)
        {
            Bg(r, "peach_rose", Pal.Peach, Pal.RoseSoft);
            PlayerTop(r, 4);
            Img(r, "Glow", Shapes.Radial("deedglow", new Color(1, 1, 1, 0.85f), new Color(1, 1, 1, 0)), 520, 600, 900, 900, A.L);
            Img(r, "Mom", Art("char_mom"), 400, 640, 400, 760, A.L);
            Img(r, "Child", Art("char_girl"), 700, 720, 330, 540, A.L);
            var card = Card(r, "DeedCard", 1360, 560, 860, 760, 56, null, 0.18f, A.R);
            Txt(card, "ЗАДАНИЕ С МАМОЙ", FontXB, 32, Pal.Rose, 430, 80, 760, 50);
            Img(card, "Picture", Art("cover_bismillah"), 430, 230, 420, 220);
            Txt(card, "Скажите «Бисмиллях» вместе перед обедом", FontXB, 50, Pal.Ink, 430, 420, 760, 150);
            var row = Rect(card, "CheckRow", Pal.LavSoft, 430, 560, 760, 110, 36);
            row.Go.AddComponent<UnityEngine.UI.Button>();
            Rect(row, "Box", Color.white, 70, 55, 64, 64, 16);
            Txt(row, "Сделали вместе", FontXB, 38, Pal.Ink, 340, 55, 400, 60, TA.Left);
            Txt(row, "отмечает мама", FontM, 26, Pal.Sub, 640, 55, 200, 60, TA.Right);
            Button(card, "Готово", Pal.Rose, 430, 680, 420, 100, A.C, null, 44);
        }

        private static void S_Reward(Box r)
        {
            var bg = Fill(r, "Background");
            AddImage(bg, Shapes.Radial("reward", Pal.Hex("FFF2C2"), Pal.Orange), Color.white);
            UI.Confetti(r, 3, 70, 80, 120, 1840, 980);
            Img(r, "Rays", Shapes.Radial("rays", new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0)), 960, 340, 800, 800);
            Img(r, "Star", Art("reward_star"), 960, 330, 430, 430);
            Txt(r, $"Машаллах, {Child}!", FontXB, 92, Pal.Hex("5B3410"), 960, 640, 1400, 120);
            var chips = Node(r, "Chips", 960, 770, 760, 90);
            Pill(chips, Art("reward_star"), "+1 звезда", 180, 45, 340, A.C, null, null, 88);
            Pill(chips, Art("leaf_green"), "+1 листок", 580, 45, 340, A.C, null, null, 88);
            Button(r, "Посмотреть дерево", Pal.Rose, 720, 960, 520, 116, A.B, null, 42);
            Button(r, "На главную", Color.white, 1240, 960, 420, 116, A.B, Pal.Ink, 42);
        }

        private static void S_Exit(Box r)
        {
            S_Story(r);
            var dim = Fill(r, "Dim");
            AddImage(dim, null, new Color(0.12f, 0.08f, 0.28f, 0.6f));
            var pop = Card(r, "ExitPopup", 960, 540, 820, 520, 60, null, 0.3f);
            Img(pop, "Star", Art("reward_star"), 410, 120, 170, 170);
            Txt(pop, "Выйти из урока?", FontXB, 60, Pal.Ink, 410, 260, 740, 80);
            Txt(pop, "Мы запомним, где ты остановилась", FontM, 34, Pal.Sub, 410, 330, 740, 60);
            Button(pop, "Остаться", Pal.Rose, 220, 440, 330, 100, A.C, null, 40);
            Button(pop, "Выйти", Pal.LavSoft, 600, 440, 300, 100, A.C, Pal.Ink, 40);
        }

        private static void S_Permissions(Box r)
        {
            Bg(r, "lavsoft", Color.white, Pal.LavSoft);
            IconButton(r, "Back", Shapes.Arrow, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.42f);
            Txt(r, "ДЛЯ РОДИТЕЛЕЙ", FontXB, 30, Pal.Rose, 960, 120, 800, 50, TA.Center, A.T);
            Txt(r, "Камера и микрофон в уроках", FontXB, 64, Pal.Ink, 960, 195, 1400, 90, TA.Center, A.T);
            var rows = new[]
            {
                ("icon_camera", "Камера", "Ребёнок видит себя на экране в весёлой маске и повторяет фразу за другом."),
                ("icon_mic", "Микрофон", "Записываем голос на несколько секунд и сразу даём послушать себя."),
            };
            for (int i = 0; i < 2; i++)
            {
                var c = Card(r, "Row " + rows[i].Item2, 960, 400 + i * 200, 1240, 170, 44);
                Rect(c, "IconBg", i == 0 ? Pal.Sky : Pal.RoseSoft, 100, 85, 120, 120, 34);
                Img(c, "Icon", Art(rows[i].Item1), 100, 85, 96, 96);
                Txt(c, rows[i].Item2, FontXB, 42, Pal.Ink, 700, 50, 960, 60, TA.Left);
                Txt(c, rows[i].Item3, FontM, 30, Pal.Sub, 700, 112, 960, 70, TA.TopLeft);
            }
            var note = Rect(r, "PrivacyNote", Pal.Hex("E6F6EC"), 960, 790, 1240, 100, 34);
            Img(note, "Lock", Shapes.Lock, 70, 50, 54, 54, A.C, Pal.GreenDark);
            Txt(note, "Видео и голос нигде не сохраняются и никуда не отправляются", FontB, 32, Pal.GreenDark, 660, 50, 1060, 80, TA.Left);
            Button(r, "Разрешить", Pal.Rose, 790, 950, 460, 110, A.B, null, 44);
            Button(r, "Без камеры", Color.white, 1240, 950, 400, 110, A.B, Pal.Ink, 40);
        }

        private static void S_Avatar(Box r)
        {
            Bg(r, "sky_mint", Pal.Sky, Pal.Mint);
            IconButton(r, "Back", Shapes.Arrow, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.42f);
            // tabs child / mom
            var tabs = Rect(r, "Tabs", Color.white, 960, 78, 520, 88, 44, A.T);
            Rect(tabs, "Active", Pal.Rose, 130, 44, 252, 72, 36);
            Txt(tabs, Child, FontXB, 36, Color.white, 130, 44, 240, 60);
            Txt(tabs, "Мама", FontXB, 36, Pal.Sub, 390, 44, 240, 60);

            Img(r, "Glow", Shapes.Radial("avglow", new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0)), 500, 600, 900, 900, A.L);
            Img(r, "Body", Art("char_girl"), 500, 660, 470, 740, A.L);
            Img(r, "FaceFrame", Shapes.DashedRing, 470, 405, 250, 250, A.L, Pal.LavDeep);
            IconButton(r, "Camera", Art("icon_camera"), Color.white, Color.white, 590, 505, 100, A.L, 0.66f);

            var panel = Card(r, "Panel", 1370, 560, 880, 820, 56, null, 0.14f, A.R);
            Txt(panel, $"Аватар для {ChildGen}", FontXB, 56, Pal.Ink, 440, 80, 800, 80);
            Button(panel, "Сделать фото", Pal.Rose, 230, 200, 380, 100, A.C, null, 36);
            Button(panel, "Из галереи", Pal.LavSoft, 650, 200, 380, 100, A.C, Pal.Ink, 36);
            Txt(panel, "или выбери готовое лицо", FontB, 32, Pal.Sub, 440, 300, 800, 50);
            for (int i = 0; i < 12; i++)
            {
                int col = i % 6, row = i / 6;
                float x = 115 + col * 130, y = 400 + row * 140;
                if (i == 0) Circle(panel, "Selected", Pal.Rose, x, y, 124);
                var f = Circle(panel, $"Face{i + 1}", Pal.LavSoft, x, y, 112);
                f.Go.AddComponent<UnityEngine.UI.Button>();
                Img(f, "Face", Art($"face_{i + 1:00}"), 56, 56, 104, 104);
            }
            var note = Rect(panel, "Privacy", Pal.Hex("E6F6EC"), 440, 625, 800, 80, 30);
            Img(note, "Lock", Shapes.Lock, 50, 40, 40, 40, A.C, Pal.GreenDark);
            Txt(note, "Фото остаётся только на этом телефоне", FontB, 28, Pal.GreenDark, 430, 40, 700, 60, TA.Left);
            Button(panel, "Готово", Pal.Green, 440, 740, 420, 100, A.C, null, 42);
        }

        private static void LeafRow(Box p, string title, string note, float cx, float cy, float w, bool fresh, bool mum)
        {
            var c = Card(p, "Leaf " + title, cx, cy, w, 104, 34, null, 0.08f);
            Img(c, "Leaf", Art(mum ? "leaf_gold" : "leaf_green"), 62, 52, 70, 70);
            Txt(c, title, FontXB, 32, Pal.Ink, 115 + (w - 340) / 2, 52, w - 340, 60, TA.Left);
            Txt(c, note, fresh ? FontXB : FontM, 24, fresh ? Pal.GreenDark : Pal.Sub, w - 110, 52, 190, 60, TA.Right);
        }

        private static void S_TreeSimple(Box r)
        {
            Bg(r, "sky_mint", Pal.Sky, Pal.Mint);
            IconButton(r, "Back", Shapes.Arrow, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.42f);
            Txt(r, "Дерево добрых дел", FontXB, 60, Pal.Ink, 540, 92, 700, 90, TA.Left, A.TL);
            Stats(r, 13, 3);
            Img(r, "Tree", Art("tree_full"), 560, 600, 820, 820, A.L);
            var list = Node(r, "Leaves", 1390, 590, 820, 700, A.R);
            Txt(list, "Листки", FontXB, 44, Pal.Ink, 410, 30, 800, 60, TA.Left);
            LeafRow(list, "Бисмиллях", "новый!", 410, 130, 800, true, false);
            LeafRow(list, "Сказали Бисмиллях вместе", "отметила мама", 410, 250, 800, false, true);
            LeafRow(list, "Приветствие", "вчера", 410, 370, 800, false, false);
            var hint = Rect(list, "Hint", new Color(1, 1, 1, 0.75f), 410, 520, 800, 110, 36);
            Img(hint, "Moon", Art("topic_sleep"), 66, 55, 76, 76);
            Txt(hint, "Пройди урок — и на дереве появится новый листок", FontB, 30, Pal.Ink, 440, 55, 640, 90, TA.Left);
        }

        private static void S_SoftOffer(Box r)
        {
            S_Reward(r);
            var dim = Fill(r, "Dim");
            AddImage(dim, null, new Color(0.12f, 0.08f, 0.28f, 0.6f));
            var pop = Card(r, "OfferPopup", 960, 540, 1100, 760, 60, null, 0.3f);
            Img(pop, "Kids", Art("cover_salam"), 550, 170, 420, 260);
            Txt(pop, "Понравились уроки?", FontXB, 60, Pal.Ink, 550, 360, 1000, 80);
            Txt(pop, "Откройте все уроки и сказки, Дерево добрых дел и прогресс ребёнка", FontM, 34, Pal.Sub, 550, 450, 900, 100);
            Button(pop, "Открыть всё", Pal.Rose, 400, 600, 440, 104, A.C, null, 42);
            Button(pop, "Позже", Pal.LavSoft, 800, 600, 280, 104, A.C, Pal.Ink, 40);
            Txt(pop, "Сначала — родительский замок", FontM, 26, Pal.Sub, 550, 700, 1000, 40);
        }

        // ───────────────────────────── release 2 ─────────────────────────────

        private static void S_Home(Box r)
        {
            Bg(r, "lav_peach", Pal.Lav, Pal.Peach);
            Head(r, "char_girl", 150, 120, 130, Pal.Peach, A.TL, 8);
            Txt(r, "Ассаляму алейкум,", FontB, 34, Pal.Sub, 560, 95, 600, 50, TA.Left, A.TL);
            Txt(r, $"{Child}!", FontXB, 60, Pal.Ink, 560, 150, 600, 80, TA.Left, A.TL);
            Stats(r, 12, 3, 110);

            var card = Card(r, "LessonOfDay", 560, 560, 860, 560, 56, null, 0.16f, A.L);
            Cover(card, "Cover", Art("cover_bismillah"), 250, 280, 420, 480, 40);
            Txt(card, "УРОК ДНЯ", FontXB, 30, Pal.Rose, 660, 90, 360, 50, TA.Left);
            Txt(card, "Бисмиллях", FontXB, 62, Pal.Ink, 660, 170, 360, 90, TA.Left);
            Txt(card, "5 минут · 5 шагов", FontM, 30, Pal.Sub, 660, 240, 360, 50, TA.Left);
            Txt(card, "Хорошие манеры", FontB, 28, Pal.LavDeep, 660, 290, 360, 50, TA.Left);
            Button(card, "Начать", Pal.Rose, 660, 450, 320, 104, A.C, null, 42, Shapes.Triangle);

            Txt(r, "Чему учимся", FontXB, 44, Pal.Ink, 1430, 300, 760, 60, TA.Left, A.R);
            var topics = new[]
            {
                ("topic_dua", "Вежливые слова", "FFE9A8"), ("topic_food", "Перед едой", "FAD3DE"), ("topic_family", "Семья", "D6F2E2"),
                ("topic_animals", "Животные", "DCEBFA"), ("topic_sleep", "Перед сном", "E6DDFB"), ("topic_kindness", "Доброта", "FFE3CF"),
            };
            var grid = Node(r, "Topics", 1430, 580, 760, 480, A.R);
            for (int i = 0; i < topics.Length; i++)
            {
                int col = i % 3, row = i / 3;
                float x = 127 + col * 253, y = 110 + row * 240;
                var t = Node(grid, "Topic " + topics[i].Item2, x, y, 240, 230);
                t.Go.AddComponent<UnityEngine.UI.Button>();
                Shadow(t, 120, 80, 150, 150, 0.12f, 8);
                Circle(t, "Ring", Color.white, 120, 80, 170);
                Circle(t, "Bubble", Pal.Hex(topics[i].Item3), 120, 80, 150);
                Img(t, "Icon", Art(topics[i].Item1), 120, 80, 112, 112);
                Txt(t, topics[i].Item2, FontXB, 28, Pal.Ink, 120, 196, 240, 50);
            }
            BottomNav(r, 0);
        }

        private static void S_TreeGrowing(Box r)
        {
            Bg(r, "sky_mint", Pal.Sky, Pal.Mint);
            Txt(r, "Дерево добрых дел", FontXB, 60, Pal.Ink, 450, 92, 700, 90, TA.Left, A.TL);
            Stats(r, 18, 5);
            Img(r, "Tree", Art("tree_stage4"), 560, 450, 580, 580, A.L);
            // new leaf animation hint
            Img(r, "Sparkle", Shapes.Radial("spark", new Color(1, 1, 0.8f, 0.9f), new Color(1, 1, 0.8f, 0)), 690, 300, 220, 220, A.L);
            Img(r, "NewLeaf", Art("leaf_gold"), 690, 300, 100, 100, A.L);
            // stages
            var stages = Node(r, "Stages", 560, 800, 780, 130, A.L);
            var st = new[] { "tree_stage1", "tree_stage2", "tree_stage3", "tree_stage4", "tree_full" };
            for (int i = 0; i < 5; i++)
            {
                float x = 78 + i * 156;
                var c = Circle(stages, "Stage" + (i + 1), i == 3 ? Color.white : new Color(1, 1, 1, 0.55f), x, 65, 124);
                Img(c, "Tree", Art(st[i]), 62, 62, 100, 100);
                if (i > 3) c.Go.AddComponent<CanvasGroup>().alpha = 0.5f;
            }
            Txt(r, "Большое дерево · 15 листков", FontB, 30, Pal.Sub, 560, 880, 780, 50, TA.Center, A.L);

            var panel = Node(r, "Panel", 1400, 500, 820, 760, A.R);
            var reward = Card(panel, "TaleReward", 410, 110, 800, 200, 44);
            Img(reward, "Moon", Art("topic_sleep"), 90, 100, 120, 120);
            Txt(reward, "Ещё 2 листка — откроется новая сказка", FontXB, 36, Pal.Ink, 460, 70, 560, 100, TA.Left);
            Progress(reward, 460, 150, 560, 26, 0.6f, Pal.Green, Pal.Mint);
            Txt(panel, "Новые листки", FontXB, 40, Pal.Ink, 410, 280, 800, 60, TA.Left);
            LeafRow(panel, "Помогла маме", "отметила мама", 410, 370, 800, false, true);
            LeafRow(panel, "Благодарность", "новый!", 410, 490, 800, true, false);
            LeafRow(panel, "Поделилась игрушкой", "отметил папа", 410, 610, 800, false, true);
            BottomNav(r, 1);
        }

        private static void S_TalesEvening(Box r)
        {
            var bg = Fill(r, "Background");
            AddImage(bg, AppSprite("Assets/UI/main-back.png"), Color.white);
            Img(r, "Moon", AppSprite("Assets/UI/Title/_____-Photoroom 1.png"), 1780, 120, 110, 170, A.TR);
            Txt(r, "Добрый вечер, " + Child, FontB, 34, Pal.Lav, 400, 80, 700, 50, TA.Left, A.TL);
            Txt(r, "Сказки на ночь", FontXB, 64, Color.white, 400, 140, 700, 90, TA.Left, A.TL);
            // reward tale
            var featured = Card(r, "RewardTale", 420, 560, 680, 600, 56, Pal.NightCard, 0.3f, A.L);
            Img(featured, "Cover", Art("tale_firefly_nur"), 340, 250, 400, 436);
            var ribbon = Rect(featured, "Ribbon", Pal.Yellow, 340, 40, 420, 68, 34);
            Img(ribbon, "Leaf", Art("leaf_gold"), 50, 34, 50, 50);
            Txt(ribbon, "Награда за 5 листков", FontXB, 28, Pal.Hex("5B3410"), 230, 34, 340, 50);
            Txt(featured, "Светлячок Нур", FontXB, 44, Color.white, 340, 500, 600, 60);
            Txt(featured, "открыта навсегда", FontM, 28, Pal.Lav, 340, 548, 600, 40);
            var tales = new[] { "tale_ramadan_moon", "tale_crescent_dua", "tale_warm_bread", "tale_dua_garden", "tale_haya_pearl", "tale_words_bridge" };
            var titles = new[] { "Луна Рамадана", "Дуа полумесяца", "Тёплый хлеб", "Сад дуа", "Жемчужина", "Мост слов" };
            var grid = Node(r, "TalesGrid", 1330, 520, 1060, 720, A.R);
            for (int i = 0; i < tales.Length; i++)
            {
                int col = i % 3, row = i / 3;
                float x = 175 + col * 355, y = 175 + row * 370;
                var c = Node(grid, "Tale " + titles[i], x, y, 300, 350);
                c.Go.AddComponent<UnityEngine.UI.Button>();
                Img(c, "Cover", Art(tales[i]), 150, 140, 270, 294);
                Txt(c, titles[i], FontB, 30, Color.white, 150, 318, 300, 50);
            }
            BottomNav(r, 2, true);
        }

        private static void S_Parents(Box r)
        {
            Bg(r, "parents", Color.white, Pal.LavSoft);
            Txt(r, "Для родителей", FontXB, 60, Pal.Ink, 450, 92, 700, 90, TA.Left, A.TL);
            Img(r, "Lock", Shapes.Lock, 1830, 92, 60, 60, A.TR, Pal.Sub);
            var left = Node(r, "Left", 520, 530, 900, 820, A.L);
            var stats = new[] { ("4", "урока"), ("13", "звёзд"), ("15", "листков"), ("3", "дня подряд") };
            for (int i = 0; i < 4; i++)
            {
                var c = Card(left, "Stat " + stats[i].Item2, 105 + i * 230, 100, 210, 170, 36, Pal.LavSoft, 0);
                Txt(c, stats[i].Item1, FontXB, 66, Pal.Ink, 105, 70, 200, 80);
                Txt(c, stats[i].Item2, FontB, 28, Pal.Sub, 105, 130, 200, 40);
            }
            Txt(left, "Ждут отметки", FontXB, 40, Pal.Ink, 450, 250, 900, 60, TA.Left);
            var pend = Card(left, "Pending", 450, 350, 900, 120, 36, null, 0.1f);
            Img(pend, "Leaf", Art("leaf_gold"), 66, 60, 70, 70);
            Txt(pend, "Сказать «Бисмиллях» вместе перед обедом", FontB, 32, Pal.Ink, 380, 60, 520, 90, TA.Left);
            Button(pend, "Сделали", Pal.Green, 770, 60, 210, 80, A.C, null, 32);
            Txt(left, "Темы и привычки", FontXB, 40, Pal.Ink, 450, 470, 900, 60, TA.Left);
            var themes = new[] { ("Бисмиллях", "стала привычкой", Pal.GreenDark), ("Приветствие", "пройдено", Pal.Sub), ("Благодарность", "пройдено", Pal.Sub) };
            for (int i = 0; i < themes.Length; i++)
            {
                var c = Rect(left, "Theme " + themes[i].Item1, Color.white, 450, 550 + i * 84, 900, 74, 26);
                Txt(c, themes[i].Item1, FontXB, 32, Pal.Ink, 300, 42, 520, 60, TA.Left);
                Txt(c, themes[i].Item2, FontXB, 28, themes[i].Item3, 740, 42, 280, 60, TA.Right);
            }

            var right = Card(r, "Settings", 1430, 500, 760, 740, 48, null, 0.12f, A.R);
            Button(right, "Добавить доброе дело", Pal.Ink, 380, 90, 660, 100, A.C, null, 36);
            var rows = new[] { ("Камера в уроках", "вкл"), ("Микрофон", "вкл"), ("Вечер начинается", "18:00"), ("Голоса для сказок", "Мама"), ("Имя и язык", $"{Child} · RU"), ("Подписка", "Год"), ("Удалить фото", "") };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 210 + i * 76;
                Txt(right, rows[i].Item1, FontB, 32, i == rows.Length - 1 ? Pal.Rose : Pal.Ink, 300, y, 520, 60, TA.Left);
                if (rows[i].Item2 == "вкл")
                {
                    var tg = Rect(right, "Toggle", Pal.Green, 640, y, 100, 56, 28);
                    Circle(tg, "Knob", Color.white, 72, 28, 46);
                }
                else Txt(right, rows[i].Item2, FontB, 30, Pal.Sub, 600, y, 240, 60, TA.Right);
                if (i < rows.Length - 1) Rect(right, "Line", Pal.LavSoft, 380, y + 38, 660, 3, 0);
            }
            BottomNav(r, 3);
        }

        private static void S_Family(Box r)
        {
            Bg(r, "sky_mint", Pal.Sky, Pal.Mint);
            IconButton(r, "Back", Shapes.Arrow, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.42f);
            Txt(r, $"Семья {ChildGen}", FontXB, 64, Pal.Ink, 960, 100, 1200, 90, TA.Center, A.T);
            Txt(r, "Кто есть в семье, тот появляется в заданиях: «с папой» показывает папу", FontM, 32, Pal.Sub, 960, 170, 1400, 50, TA.Center, A.T);
            var people = new[] { ("char_girl", Child, true), ("char_mom", "Мама", true), ("char_dad", "Папа", true), ("char_grandma", "Бабушка", true), ("char_grandpa", "Дедушка", true), ("", "Брат", false), ("", "Сестра", false) };
            for (int i = 0; i < people.Length; i++)
            {
                float x = 960 + (i - 3) * 250, y = 560;
                var c = Card(r, "Member " + people[i].Item2, x, y, 220, 420, 44, people[i].Item3 ? Color.white : new Color(1, 1, 1, 0.5f), people[i].Item3 ? 0.12f : 0);
                c.Go.AddComponent<UnityEngine.UI.Button>();
                if (people[i].Item3) Img(c, "Body", Art(people[i].Item1), 110, 190, 200, 330);
                else
                {
                    Img(c, "Add", Shapes.DashedRing, 110, 170, 150, 150, A.C, Pal.LavDeep);
                    Txt(c, "+", FontXB, 90, Pal.LavDeep, 110, 162, 150, 150);
                }
                Txt(c, people[i].Item2, FontXB, 32, Pal.Ink, 110, 385, 210, 50);
            }
            var note = Rect(r, "Note", new Color(1, 1, 1, 0.75f), 960, 880, 1100, 100, 34);
            Img(note, "Lock", Shapes.Lock, 60, 50, 46, 46, A.C, Pal.GreenDark);
            Txt(note, "Фото добавляет родитель. Они хранятся только на телефоне", FontB, 30, Pal.GreenDark, 580, 50, 960, 80, TA.Left);
            Button(r, "Сохранить", Pal.Green, 960, 990, 400, 104, A.B, null, 40);
        }

        private static void S_Trace(Box r)
        {
            Bg(r, "lav_peach", Pal.Lav, Pal.Peach);
            PlayerTop(r, 3);
            Txt(r, "Обведи фонарик пальчиком", FontXB, 60, Pal.Ink, 960, 200, 1400, 90, TA.Center, A.T);
            var board = Card(r, "Board", 960, 600, 1000, 720, 56);
            Img(board, "Outline", Art("mask_fanous"), 500, 370, 520, 560, A.C, new Color(0.8f, 0.74f, 0.96f, 0.55f));
            // dotted path around it
            int n = 46;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2;
                float x = 500 + Mathf.Sin(t) * 250, y = 370 - Mathf.Cos(t) * 300;
                bool done = i < n * 0.55f;
                Circle(board, "Dot", done ? Pal.Rose : Pal.Lav, x, y, done ? 26 : 18);
            }
            float fx = 500 + Mathf.Sin(0.55f * Mathf.PI * 2) * 250, fy = 370 - Mathf.Cos(0.55f * Mathf.PI * 2) * 300;
            Circle(board, "FingerRing", new Color(Pal.Rose.r, Pal.Rose.g, Pal.Rose.b, 0.25f), fx, fy, 110);
            Circle(board, "Finger", Pal.Rose, fx, fy, 64);
            Img(r, "Hint", Art("char_girl"), 260, 760, 300, 480, A.BL);
            Bubble(r, "После обводки фонарик раскрасится!", 1620, 520, 420, 170, 34);
        }

        private static void S_Puzzle(Box r)
        {
            Bg(r, "sky", Pal.Hex("DCEFFD"), Pal.Sky);
            PlayerTop(r, 3);
            Txt(r, "Собери картинку", FontXB, 60, Pal.Ink, 960, 200, 1400, 90, TA.Center, A.T);
            var board = Card(r, "Board", 820, 600, 880, 640, 48);
            var spr = Art("cover_salam");
            // 2×2 pieces: three placed, one dragged
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                float x = 230 + col * 420, y = 170 + row * 300;
                if (i == 3)
                {
                    Rect(board, "Slot", Pal.LavSoft, x, y, 410, 290, 24);
                    Img(board, "SlotRing", Shapes.DashedRing, x, y, 160, 160, A.C, Pal.Lav);
                    continue;
                }
                var piece = Rect(board, "Piece" + (i + 1), Color.white, x, y, 410, 290, 24);
                piece.Go.AddComponent<Mask>().showMaskGraphic = true;
                Img(piece, "Picture", spr, 205 + (col == 0 ? 210 : -210), 145 + (row == 0 ? 150 : -150), 830, 590, A.C, null, false);
            }
            var drag = Rect(r, "DraggedPiece", Color.white, 1530, 620, 410, 290, 24);
            drag.Rt.localRotation = Quaternion.Euler(0, 0, -6);
            Shadow(r, 1530, 620, 410, 290, 0.25f, 24).Rt.SetSiblingIndex(drag.Rt.GetSiblingIndex());
            drag.Go.AddComponent<Mask>().showMaskGraphic = true;
            Img(drag, "Picture", spr, 205 - 210, 145 - 150, 830, 590, A.C, null, false);
            Circle(r, "Finger", new Color(1, 1, 1, 0.9f), 1600, 690, 70);
        }

        private static void S_Questionnaire(Box r)
        {
            Bg(r, "lavsoft", Color.white, Pal.LavSoft);
            IconButton(r, "Back", Shapes.Arrow, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.42f);
            Progress(r, 960, 78, 700, 22, 3 / 6f, Pal.Rose, Pal.Lav, A.T);
            Txt(r, "3 из 6", FontB, 28, Pal.Sub, 1420, 78, 160, 40, TA.Left, A.T);
            Img(r, "Mom", Art("char_mom"), 300, 640, 400, 780, A.BL);
            Txt(r, $"Что {Child} уже знает?", FontXB, 66, Pal.Ink, 1100, 230, 1200, 90, TA.Left, A.T);
            Txt(r, "Отметьте всё, что подходит. Начнём с нового для ребёнка", FontM, 32, Pal.Sub, 1100, 300, 1200, 50, TA.Left, A.T);
            var chips = new[] { ("Бисмиллях перед едой", true), ("Ассаляму алейкум", true), ("Альхамдулиллях", false), ("Дуа перед сном", false), ("Сура аль-Фатиха", false), ("Пока ничего", false) };
            for (int i = 0; i < chips.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x = 810 + col * 580, y = 450 + row * 140;
                bool on = chips[i].Item2;
                var c = Card(r, "Chip " + chips[i].Item1, x, y, 550, 112, 56, on ? Pal.RoseSoft : Color.white, on ? 0 : 0.1f);
                c.Go.AddComponent<UnityEngine.UI.Button>();
                if (on) Check(c, 60, 56, 64, Pal.Rose);
                else Img(c, "Box", Shapes.Ring, 60, 56, 60, 60, A.C, Pal.Lav);
                Txt(c, chips[i].Item1, FontXB, 34, Pal.Ink, 320, 56, 420, 80, TA.Left);
            }
            Button(r, "Дальше", Pal.Rose, 1400, 950, 420, 110, A.BR, null, 44);
        }

        private static void S_Plan(Box r)
        {
            Bg(r, "lav_peach", Pal.Lav, Pal.Peach);
            Txt(r, $"План для {ChildGen}", FontXB, 72, Pal.Ink, 960, 130, 1400, 100, TA.Center, A.T);
            Txt(r, "5 минут в день · уроки в 10:00, сказка в 19:30", FontB, 34, Pal.Sub, 960, 205, 1400, 50, TA.Center, A.T);
            var days = new[] { ("Пн", "cover_bismillah", "Бисмиллях"), ("Вт", "cover_salam", "Приветствие"), ("Ср", "cover_shukr", "Благодарность"), ("Чт", "topic_kindness", "Доброта"), ("Пт", "topic_family", "Родители"), ("Сб", "topic_animals", "Животные"), ("Вс", "topic_sleep", "Дуа перед сном") };
            for (int i = 0; i < days.Length; i++)
            {
                float x = 960 + (i - 3) * 236;
                var c = Card(r, "Day " + days[i].Item1, x, 520, 214, 380, 40, i == 0 ? Color.white : new Color(1, 1, 1, 0.8f), i == 0 ? 0.16f : 0.06f);
                Txt(c, days[i].Item1, FontXB, 36, i == 0 ? Pal.Rose : Pal.Sub, 107, 50, 200, 50);
                if (days[i].Item2.StartsWith("cover")) Cover(c, "Pic", Art(days[i].Item2), 107, 170, 170, 150, 28);
                else { Circle(c, "Bubble", Pal.LavSoft, 107, 170, 150); Img(c, "Pic", Art(days[i].Item2), 107, 170, 110, 110); }
                Txt(c, days[i].Item3, FontXB, 26, Pal.Ink, 107, 300, 200, 70);
            }
            var perks = new[] { "Уроки об исламе каждый день", "Вся библиотека сказок на вечер", "Дерево добрых дел и прогресс" };
            for (int i = 0; i < 3; i++)
            {
                float x = 960 + (i - 1) * 580;
                Check(r, x - 250, 800, 50, Pal.Green);
                Txt(r, perks[i], FontB, 28, Pal.Ink, x + 20, 800, 480, 60, TA.Left);
            }
            Button(r, "Попробовать 7 дней бесплатно", Pal.Rose, 960, 950, 760, 116, A.B, null, 44);
        }

        private static void S_Paywall(Box r)
        {
            Bg(r, "paywall", Pal.Night, Pal.Night2);
            IconButton(r, "Close", Shapes.Cross, new Color(1, 1, 1, 0.15f), Color.white, 90, 78, 92, A.TL, 0.4f);
            Img(r, "Kids", Art("char_friend"), 260, 680, 360, 560, A.BL);
            Img(r, "Girl", Art("char_girl"), 520, 720, 330, 520, A.BL);
            Img(r, "Star", Art("reward_star"), 390, 220, 200, 200, A.TL);
            Txt(r, "Все уроки и сказки", FontXB, 70, Color.white, 1230, 140, 1100, 100, TA.Left, A.TR);
            var perks = new[] { "Новый урок каждый день и 2–4 новых в месяц", "Вся библиотека сказок на вечер", "Дерево добрых дел и прогресс ребёнка" };
            for (int i = 0; i < perks.Length; i++)
            {
                Check(r, 720, 250 + i * 70, 48, Pal.Green);
                Txt(r, perks[i], FontB, 34, Color.white, 1260, 250 + i * 70, 1000, 60, TA.Left, A.TR);
            }
            var plans = new[] { ("Год", "14 990 ₸", "1 249 ₸ в месяц", true), ("Месяц", "2 490 ₸", "в месяц", false) };
            for (int i = 0; i < 2; i++)
            {
                float x = 990 + i * 480;
                bool best = plans[i].Item4;
                var c = Card(r, "Plan " + plans[i].Item1, x, 620, 440, 290, 44, best ? Color.white : Pal.NightCard, best ? 0.25f : 0, A.R);
                if (best)
                {
                    var tag = Rect(c, "Tag", Pal.Yellow, 220, 0, 260, 56, 28);
                    Txt(tag, "7 дней бесплатно", FontXB, 26, Pal.Hex("5B3410"), 130, 28, 260, 50);
                }
                Txt(c, plans[i].Item1, FontXB, 40, best ? Pal.Ink : Color.white, 220, 80, 400, 60);
                Txt(c, plans[i].Item2, FontXB, 56, best ? Pal.Ink : Color.white, 220, 160, 400, 70);
                Txt(c, plans[i].Item3, FontM, 28, best ? Pal.Sub : Pal.Lav, 220, 225, 400, 40);
            }
            Button(r, "Начать 7 дней бесплатно", Pal.Purple, 1230, 900, 940, 120, A.BR, null, 46);
            Txt(r, "Потом 14 990 ₸ в год. Отмена в любой момент · Восстановить покупки", FontM, 26, Pal.Lav, 1230, 1010, 1100, 40, TA.Center, A.BR);
        }
    }
}
