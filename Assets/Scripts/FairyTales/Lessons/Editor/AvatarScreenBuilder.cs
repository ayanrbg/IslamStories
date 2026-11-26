using System.Collections.Generic;
using System.IO;
using FairyTales.UI.Core;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.InferenceEngine;
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
    /// The avatar parts asset, the head-cutting model and the avatar screen:
    ///
    ///   Nooria ▸ Lessons ▸ Build avatar screen — Resources/LessonAvatarArt.asset (from
    ///   Assets/UI/Lessons/Avatar2D + parts.json), the float16 model, the prefab, the scene instance
    /// </summary>
    public static class AvatarScreenBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Lessons/AvatarScreen.prefab";
        public const string ArtAssetPath = "Assets/Resources/" + AvatarArt.ResourcePath + ".asset";
        private const string ArtDir = "Assets/UI/Lessons/Art";
        private const string PartsDir = "Assets/UI/Lessons/Avatar2D";
        private const string ModelSource = "Assets/ThirdParty/MediaPipe/selfie_multiclass_256x256.tflite";
        private const string ModelFp16 = "Assets/ThirdParty/MediaPipe/selfie_multiclass_fp16.sentis";
        private const string GuidePath = Shapes.Dir + "/head_guide.png";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        // Sizes in body widths. Kids get big heads (the paper-doll look), grown-ups a little less.
        private const float KidHead = 1.0f, KidFace = 0.84f, MomHead = 0.86f, MomFace = 0.58f;

        private static Sprite Art(string name)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png")
               ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.jpg");

        private static Sprite Part(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{PartsDir}/{name}.png");

        [MenuItem("Nooria/Lessons/Build avatar screen")]
        public static string BuildAll()
        {
            var art = BuildArt();
            var model = BuildModel();
            var msg = BuildPrefab(art, model) + " | " + InstallInScene();
            Debug.Log(msg);
            return msg;
        }

        // ─────────────────────────── art ───────────────────────────

        /// <summary>Bodies and heads from Avatar2D; neck and chin points from parts.json (art2/parts.js measured them).</summary>
        public static AvatarArt BuildArt()
        {
            Shapes.Ensure();
            foreach (var file in Directory.GetFiles(PartsDir, "*.png"))
                Shapes.Import(file.Replace('\\', '/'), 0, 1024, compress: true);
            var parts = JObject.Parse(File.ReadAllText(Path.Combine(PartsDir, "parts.json")));

            Directory.CreateDirectory(Path.GetDirectoryName(ArtAssetPath)!);
            var art = AssetDatabase.LoadAssetAtPath<AvatarArt>(ArtAssetPath);
            bool created = art == null;
            if (created) art = ScriptableObject.CreateInstance<AvatarArt>();

            AvatarArt.Body Body(string id, float head, float face)
            {
                var m = parts[id];
                return new AvatarArt.Body
                {
                    id = id, sprite = Part(id),
                    neck = new Vector2((float)m["neck"][0], (float)m["neck"][1]),
                    headWidth = head, faceWidth = face,
                };
            }
            AvatarArt.Head Head(string id, bool hijab)
            {
                var m = parts[id];
                return new AvatarArt.Head { sprite = Part(id), chin = new Vector2((float)m["chin"][0], (float)m["chin"][1]), hijab = hijab };
            }

            art.boy = Body("body_boy", KidHead, KidFace);
            art.girl = Body("body_girl", KidHead, KidFace);
            art.girlHijab = Body("body_girl_hijab", KidHead, KidFace);
            art.mom = Body("body_mom", MomHead, MomFace);
            art.friendBoy = Body("body_friend_boy", KidHead, KidFace);
            art.friendGirl = Body("body_friend_girl", KidHead, KidFace);
            art.kidHeads = new[]
            {
                Head("head_g1", true), Head("head_g2", true), Head("head_g3", true),
                Head("head_g4", true), Head("head_g5", true), Head("head_g6", false),
                Head("head_b1", false), Head("head_b2", false), Head("head_b3", false),
                Head("head_b4", false), Head("head_b5", false), Head("head_b6", false),
            };
            art.momHeads = new[] { Head("head_m1", true), Head("head_m2", true) };
            art.circle = Shapes.Circle;

            if (created) AssetDatabase.CreateAsset(art, ArtAssetPath);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        /// <summary>The segmentation model at half precision (~8 MB): what the app ships.</summary>
        public static ModelAsset BuildModel()
        {
            if (!File.Exists(ModelFp16))
            {
                var src = AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelSource);
                var model = ModelLoader.Load(src);
                ModelQuantizer.QuantizeWeights(QuantizationType.Float16, ref model);
                ModelWriter.Save(ModelFp16, model);
                AssetDatabase.ImportAsset(ModelFp16);
            }
            return AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelFp16);
        }

        /// <summary>
        /// An avatar node. A placeholder sprite keeps the prefab readable in the editor;
        /// AvatarView redraws it at runtime. A bust gets the circle mask.
        /// </summary>
        internal static AvatarView Avatar(Box p, string name, AvatarPerson person, float cx, float cy, float w, float h,
            A a = A.C, bool bust = false, Color? bustBg = null)
        {
            var node = Node(p, name, cx, cy, w, h, a);
            if (bust)
            {
                AddImage(node, Shapes.Circle, bustBg ?? Pal.LavSoft);
                node.Go.AddComponent<Mask>().showMaskGraphic = true;
            }
            var placeholder = person == AvatarPerson.Mom ? Art("char_mom") : Art("char_girl");
            var body = bust ? Fill(node, "Body") : Img(node, "Body", placeholder, w / 2, h / 2, w, h);
            if (bust) AddImage(body, placeholder, Color.white, true);
            var view = node.Go.AddComponent<AvatarView>();
            view.person = person;
            view.bust = bust;
            return view;
        }

        // ─────────────────────────── screen ───────────────────────────

        public static string BuildPrefab(AvatarArt art, ModelAsset model)
        {
            Shapes.Ensure();
            LoadFonts();
            EnsureGuideSprite();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath)!);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("Holder", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(holder, scene);
                var root = Root("AvatarScreen", holder.transform);
                root.Go.AddComponent<Canvas>();
                root.Go.AddComponent<CanvasGroup>();
                root.Go.AddComponent<GraphicRaycaster>();
                var s = root.Go.AddComponent<AvatarScreen>();
                var so = new SerializedObject(s);
                so.FindProperty("slideFromBottom").boolValue = false;
                so.FindProperty("animDuration").floatValue = 0.3f;
                so.ApplyModifiedPropertiesWithoutUndo();
                s.headModel = model;

                BuildMain(root, s, art);
                BuildPhoto(root, s);
                s.photoPanel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root.Go, PrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            return $"[AvatarScreen] built {PrefabPath}";
        }

        private static Button Chip(Box p, string name, string key, float cx, float cy, float w, out Image bg)
        {
            var b = Rect(p, name, Pal.LavSoft, cx, cy, w, 64, 32);
            bg = b.Go.GetComponent<Image>();
            LessonPlayerBuilder.Label(AutoSize(Txt(b, "", FontXB, 28, Pal.Sub, w / 2, 31, w - 24, 56), 18), key);
            return LessonPlayerBuilder.Clickable(b);
        }

        private static Button RoundText(Box p, string name, string text, float cx, float cy, float d)
        {
            Shadow(p, cx, cy, d * 0.8f, d * 0.8f, 0.12f, 8);
            var b = Circle(p, name, Color.white, cx, cy, d);
            Txt(b, text, FontXB, d * 0.55f, Pal.LavDeep, d / 2, d / 2 - 3, d, d);
            return LessonPlayerBuilder.Clickable(b);
        }

        private static void BuildMain(Box root, AvatarScreen s, AvatarArt art)
        {
            var bg = Fill(root, "Background");
            AddImage(bg, Shapes.Gradient("sky_mint", Pal.Sky, Pal.Mint), Color.white).raycastTarget = true;
            var safe = Fill(root, "Safe");
            safe.Go.AddComponent<SafeAreaFitter>();

            s.back = LessonPlayerBuilder.Icon(safe, "Back", Shapes.Arrow, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.42f);

            var tabs = Rect(safe, "Tabs", Color.white, 960, 78, 520, 88, 44, A.T);
            var tc = Rect(tabs, "TabChild", Pal.Rose, 130, 44, 252, 72, 36);
            s.tabChild = LessonPlayerBuilder.Clickable(tc);
            s.tabChildBg = tc.Go.GetComponent<Image>();
            s.tabChildLabel = AutoSize(Txt(tc, "Айша", FontXB, 36, Color.white, 126, 36, 230, 60), 24);
            var tm = Rect(tabs, "TabMom", Color.clear, 390, 44, 252, 72, 36);
            s.tabMom = LessonPlayerBuilder.Clickable(tm);
            s.tabMomBg = tm.Go.GetComponent<Image>();
            s.tabMomLabel = LessonPlayerBuilder.Label(AutoSize(Txt(tm, "", FontXB, 36, Pal.Sub, 126, 36, 230, 60), 24), "avatar_mom_tab");

            // The figure, with a finger area over it for fitting a photo head.
            Img(safe, "Glow", Shapes.Radial("avglow", new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0)), 440, 600, 900, 900, A.L);
            s.preview = Avatar(safe, "Preview", AvatarPerson.Child, 440, 590, 520, 760, A.L);
            var hit = Node(safe, "FitArea", 440, 590, 520, 760, A.L);
            AddImage(hit, null, new Color(1, 1, 1, 0)).raycastTarget = true;
            s.fitter = hit.Go.AddComponent<AvatarFitter>();
            s.fitter.view = s.preview;

            var fit = Node(safe, "Fit", 110, 520, 120, 300, A.L);
            s.fitBlock = fit.Go;
            s.sizePlus = RoundText(fit, "Bigger", "+", 60, 70, 96);
            s.sizeMinus = RoundText(fit, "Smaller", "−", 60, 190, 96);
            fit.Go.SetActive(false);

            var body = Node(safe, "Body", 440, 1010, 500, 80, A.L);
            s.bodyBlock = body.Go;
            s.bodyPlain = Chip(body, "Plain", "avatar_body_plain", 125, 40, 236, out s.bodyPlainBg);
            s.bodyHijab = Chip(body, "Hijab", "avatar_body_hijab", 375, 40, 236, out s.bodyHijabBg);

            var panel = Card(safe, "Panel", 1370, 560, 880, 820, 56, null, 0.14f, A.R);
            s.title = AutoSize(Txt(panel, "", FontXB, 56, Pal.Ink, 440, 80, 800, 80), 36);

            var row = Node(panel, "PhotoButtons", 440, 195, 860, 160);
            var layout = row.Go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            s.takePhoto = LessonPlayerBuilder.Raised(row, "TakePhoto", "avatar_take", Pal.Rose, 0, 80, 340, 100, A.C, null, 36);
            s.gallery = LessonPlayerBuilder.Raised(row, "Gallery", "avatar_gallery", Pal.LavSoft, 0, 80, 340, 100, A.C, Pal.Ink, 36);

            var remove = Node(panel, "RemovePhoto", 440, 288, 360, 50);
            AddImage(remove, null, new Color(1, 1, 1, 0)).raycastTarget = true;
            s.removePhoto = LessonPlayerBuilder.Clickable(remove);
            LessonPlayerBuilder.Label(Txt(remove, "", FontXB, 28, Pal.Rose, 180, 25, 360, 50), "avatar_remove");

            s.headsLabel = AutoSize(Txt(panel, "", FontB, 30, Pal.Sub, 440, 340, 800, 46), 22);
            var buttons = new List<Button>();
            var images = new List<Image>();
            var selected = new List<GameObject>();
            for (int i = 0; i < 12; i++)
            {
                int col = i % 6, r = i / 6;
                float x = 115 + col * 130, y = 440 + r * 124;
                var ring = Circle(panel, $"Selected{i + 1}", Pal.Rose, x, y, 116);
                ring.Go.SetActive(false);
                selected.Add(ring.Go);
                var f = Circle(panel, $"Head{i + 1}", Pal.LavSoft, x, y, 104);
                buttons.Add(LessonPlayerBuilder.Clickable(f));
                var sprite = i < art.kidHeads.Length ? art.kidHeads[i].sprite : null;
                images.Add(Img(f, "Head", sprite, 52, 54, 92, 92).Go.GetComponent<Image>());
            }
            s.heads = buttons.ToArray();
            s.headImages = images.ToArray();
            s.headSelected = selected.ToArray();

            var note = Rect(panel, "Privacy", Pal.Hex("E6F6EC"), 440, 665, 800, 70, 30);
            Img(note, "Lock", Shapes.Lock, 50, 35, 38, 38, A.C, Pal.GreenDark);
            LessonPlayerBuilder.Label(AutoSize(Txt(note, "", FontB, 28, Pal.GreenDark, 430, 35, 700, 56, TA.Left), 20), "avatar_privacy");
            s.done = LessonPlayerBuilder.Raised(panel, "Done", "done", Pal.Green, 440, 760, 420, 96, A.C, null, 42);
        }

        private static void BuildPhoto(Box root, AvatarScreen s)
        {
            var panel = Fill(root, "PhotoPanel");
            s.photoPanel = panel.Go;
            var dim = Fill(panel, "Dim");
            AddImage(dim, null, new Color(0.1f, 0.07f, 0.22f, 1f)).raycastTarget = true;
            var safe = Fill(panel, "Safe");
            safe.Go.AddComponent<SafeAreaFitter>();

            const float vw = 588, vh = 672;
            var view = Rect(safe, "Viewport", Pal.Night, 900, 560, vw, vh, 48);
            view.Go.AddComponent<Mask>().showMaskGraphic = true;
            var photoGo = Node(view, "Photo", vw / 2, vh / 2, vw, vh);
            var raw = photoGo.Go.AddComponent<RawImage>();
            raw.raycastTarget = false;
            // Head-and-shoulders outline: where the child should be in the frame.
            var guide = Fill(view, "Guide");
            AddImage(guide, AssetDatabase.LoadAssetAtPath<Sprite>(GuidePath), Color.white);

            var cropper = view.Go.AddComponent<PhotoCropper>();
            cropper.viewport = view.Rt;
            cropper.photo = raw;
            s.cropper = cropper;

            s.photoHint = AutoSize(Txt(safe, "", FontXB, 44, Color.white, 900, 150, 1200, 80, TA.Center, A.T), 26);
            s.photoClose = LessonPlayerBuilder.Icon(safe, "Close", Shapes.Cross, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.4f);
            s.shoot = LessonPlayerBuilder.Icon(safe, "Shoot", Art("icon_camera"), Pal.Rose, Color.white, 1530, 560, 180, A.R, 0.6f, holder: true);
            s.retake = LessonPlayerBuilder.Raised(safe, "Retake", "avatar_retake", Pal.LavSoft, 1530, 560, 340, 100, A.R, Pal.Ink, 38);
        }

        /// <summary>Dark veil with a head-and-shoulders hole and a white outline, over the camera picture.</summary>
        private static void EnsureGuideSprite()
        {
            if (File.Exists(GuidePath)) return;
            const int w = 448, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            // Implicit shape: < 1 inside. Head ellipse and a shoulders ellipse below it (y from the top).
            float Shape(float u, float v)
            {
                float hx = (u - 0.5f) / 0.27f, hy = (v - 0.4f) / 0.27f * (h / (float)w) * 0.82f;
                float sx = (u - 0.5f) / 0.5f, sy = (v - 1.05f) / 0.3f;
                return Mathf.Min(Mathf.Sqrt(hx * hx + hy * hy), Mathf.Sqrt(sx * sx + sy * sy));
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = 1f - (y + 0.5f) / h;
                float f = Shape(u, v);
                Color32 c;
                if (Mathf.Abs(f - 1f) < 0.012f) c = new Color32(255, 255, 255, 230);
                else if (f > 1f) c = new Color32(20, 14, 45, 150);
                else c = new Color32(0, 0, 0, 0);
                px[y * w + x] = c;
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(GuidePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GuidePath);
            Shapes.Import(GuidePath, 0, 512);
        }

        private static TMP_Text AutoSize(TMP_Text t, float min)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = min;
            return t;
        }

        // ─────────────────────────── scene ───────────────────────────

        public static string InstallInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return "no prefab";
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var sm = Object.FindAnyObjectByType<ScreenManager>(FindObjectsInactive.Include);
            if (sm == null) return "no ScreenManager";
            var canvas = sm.transform;
            foreach (var old in canvas.GetComponentsInChildren<AvatarScreen>(true)) Object.DestroyImmediate(old.gameObject);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            inst.name = "AvatarScreen";
            var player = canvas.GetComponentInChildren<LessonPlayerScreen>(true);
            var anchor = player != null ? player.transform : canvas.Find("NarrationProgressScreen");
            inst.transform.SetSiblingIndex(anchor != null ? anchor.GetSiblingIndex() + 1 : canvas.childCount - 1);
            var rt = (RectTransform)inst.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            inst.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"[AvatarScreen] installed at sibling {inst.transform.GetSiblingIndex()}";
        }
    }
}
