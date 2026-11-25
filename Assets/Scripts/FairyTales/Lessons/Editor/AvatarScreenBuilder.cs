using System.Collections.Generic;
using System.IO;
using FairyTales.UI.Core;
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
    /// The avatar parts asset and the avatar screen (design 13):
    ///
    ///   Nooria ▸ Lessons ▸ Build avatar screen — Resources/LessonAvatarArt.asset, the prefab, the scene instance
    /// </summary>
    public static class AvatarScreenBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Lessons/AvatarScreen.prefab";
        public const string ArtAssetPath = "Assets/Resources/" + AvatarArt.ResourcePath + ".asset";
        private const string ArtDir = "Assets/UI/Lessons/Art";
        private const string HolePath = Shapes.Dir + "/oval_hole.png";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        /// <summary>The face oval inside the photo viewport (share of its size), also cut into the hole sprite.</summary>
        private const float OvalShare = 0.8f;

        private static Sprite Art(string name)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png")
               ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.jpg");

        [MenuItem("Nooria/Lessons/Build avatar screen")]
        public static string BuildAll()
        {
            var art = BuildArt();
            var msg = BuildPrefab(art) + " | " + InstallInScene();
            Debug.Log(msg);
            return msg;
        }

        // ─────────────────────────── art ───────────────────────────

        /// <summary>
        /// Face and head rects measured on the stand-in bodies (Assets/UI/Lessons/Art).
        /// New art → measure again, or edit the asset in the Inspector.
        /// </summary>
        public static AvatarArt BuildArt()
        {
            Shapes.Ensure();
            Directory.CreateDirectory(Path.GetDirectoryName(ArtAssetPath)!);
            var art = AssetDatabase.LoadAssetAtPath<AvatarArt>(ArtAssetPath);
            bool created = art == null;
            if (created) art = ScriptableObject.CreateInstance<AvatarArt>();

            art.girl = new AvatarArt.Body { sprite = Art("char_girl"), face = new Rect(0.402f, 0.19f, 0.35f, 0.21f), head = new Rect(0.385f, 0.17f, 0.5f, 0.35f) };
            art.boy = new AvatarArt.Body { sprite = Art("char_boy"), face = new Rect(0.696f, 0.258f, 0.45f, 0.23f), head = new Rect(0.688f, 0.19f, 0.62f, 0.38f) };
            art.mom = new AvatarArt.Body { sprite = Art("char_mom"), face = new Rect(0.5f, 0.165f, 0.5f, 0.17f), head = new Rect(0.505f, 0.15f, 0.8f, 0.3f) };
            var faces = new List<Sprite>();
            for (int i = 1; i <= 12; i++) faces.Add(Art($"face_{i:00}"));
            art.faces = faces.ToArray();
            art.circle = Shapes.Circle;

            if (created) AssetDatabase.CreateAsset(art, ArtAssetPath);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        /// <summary>
        /// An avatar node. A placeholder body sprite keeps the prefab readable in the editor;
        /// AvatarView redraws it at runtime. A bust gets the circle mask.
        /// </summary>
        public static AvatarView Avatar(Box p, string name, AvatarPerson person, float cx, float cy, float w, float h,
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

        public static string BuildPrefab(AvatarArt art)
        {
            Shapes.Ensure();
            LoadFonts();
            EnsureHoleSprite();
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

            Img(safe, "Glow", Shapes.Radial("avglow", new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0)), 500, 600, 900, 900, A.L);
            s.preview = Avatar(safe, "Preview", AvatarPerson.Child, 500, 640, 470, 760, A.L);
            var remove = Rect(safe, "RemovePhoto", Color.white, 500, 1010, 320, 72, 36, A.L);
            s.removePhoto = LessonPlayerBuilder.Clickable(remove);
            LessonPlayerBuilder.Label(Txt(remove, "", FontXB, 30, Pal.Rose, 160, 34, 300, 60), "avatar_remove");

            var panel = Card(safe, "Panel", 1370, 560, 880, 820, 56, null, 0.14f, A.R);
            s.title = AutoSize(Txt(panel, "", FontXB, 56, Pal.Ink, 440, 80, 800, 80), 36);

            var row = Node(panel, "PhotoButtons", 440, 200, 860, 180);
            var layout = row.Go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            s.takePhoto = LessonPlayerBuilder.Raised(row, "TakePhoto", "avatar_take", Pal.Rose, 0, 90, 380, 100, A.C, null, 36);
            s.gallery = LessonPlayerBuilder.Raised(row, "Gallery", "avatar_gallery", Pal.LavSoft, 0, 90, 380, 100, A.C, Pal.Ink, 36);

            var faces = Node(panel, "Faces", 440, 470, 860, 360);
            s.facesBlock = faces.Go;
            LessonPlayerBuilder.Label(Txt(faces, "", FontB, 32, Pal.Sub, 430, 30, 800, 50), "avatar_or_face");
            var buttons = new List<Button>();
            var selected = new List<GameObject>();
            for (int i = 0; i < 12; i++)
            {
                int col = i % 6, r = i / 6;
                float x = 105 + col * 130, y = 130 + r * 140;
                var ring = Circle(faces, $"Selected{i + 1}", Pal.Rose, x, y, 124);
                ring.Go.SetActive(false);
                selected.Add(ring.Go);
                var f = Circle(faces, $"Face{i + 1}", Pal.LavSoft, x, y, 112);
                buttons.Add(LessonPlayerBuilder.Clickable(f));
                Img(f, "Face", art.Face(i), 56, 56, 104, 104);
            }
            s.faces = buttons.ToArray();
            s.faceSelected = selected.ToArray();

            var hint = LessonPlayerBuilder.Label(AutoSize(Txt(panel, "", FontB, 36, Pal.Sub, 440, 430, 760, 140), 26), "avatar_mom_hint");
            s.momHint = hint.gameObject;
            s.momHint.SetActive(false);

            var note = Rect(panel, "Privacy", Pal.Hex("E6F6EC"), 440, 655, 800, 80, 30);
            Img(note, "Lock", Shapes.Lock, 50, 40, 40, 40, A.C, Pal.GreenDark);
            LessonPlayerBuilder.Label(AutoSize(Txt(note, "", FontB, 28, Pal.GreenDark, 430, 40, 700, 60, TA.Left), 20), "avatar_privacy");
            s.done = LessonPlayerBuilder.Raised(panel, "Done", "done", Pal.Green, 440, 755, 420, 96, A.C, null, 42);
        }

        private static void BuildPhoto(Box root, AvatarScreen s)
        {
            var panel = Fill(root, "PhotoPanel");
            s.photoPanel = panel.Go;
            var dim = Fill(panel, "Dim");
            AddImage(dim, null, new Color(0.1f, 0.07f, 0.22f, 0.94f)).raycastTarget = true;
            var safe = Fill(panel, "Safe");
            safe.Go.AddComponent<SafeAreaFitter>();

            const float vw = 588, vh = 672; // aspect of the saved photo (448×512)
            var view = Rect(safe, "Viewport", Pal.Night, 900, 560, vw, vh, 48);
            view.Go.GetComponent<Image>().raycastTarget = true;
            view.Go.AddComponent<Mask>().showMaskGraphic = true;
            var photoGo = Node(view, "Photo", vw / 2, vh / 2, vw, vh);
            var raw = photoGo.Go.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var hole = Fill(view, "Hole");
            AddImage(hole, AssetDatabase.LoadAssetAtPath<Sprite>(HolePath), Color.white);
            var oval = Node(view, "Oval", vw / 2, vh / 2, vw * OvalShare, vh * OvalShare);
            Img(oval, "Ring", Shapes.DashedRing, vw * OvalShare / 2, vh * OvalShare / 2, vw * OvalShare + 8, vh * OvalShare + 8,
                A.C, Color.white, preserve: false);

            var cropper = view.Go.AddComponent<PhotoCropper>();
            cropper.viewport = view.Rt;
            cropper.photo = raw;
            cropper.oval = oval.Rt;
            s.cropper = cropper;

            s.photoHint = AutoSize(Txt(safe, "", FontXB, 44, Color.white, 900, 150, 1100, 80, TA.Center, A.T), 28);
            s.photoClose = LessonPlayerBuilder.Icon(safe, "Close", Shapes.Cross, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.4f);
            s.shoot = LessonPlayerBuilder.Icon(safe, "Shoot", Art("icon_camera"), Pal.Rose, Color.white, 1530, 560, 180, A.R, 0.6f, holder: true);
            s.retake = LessonPlayerBuilder.Raised(safe, "Retake", "avatar_retake", Pal.LavSoft, 1530, 480, 340, 100, A.R, Pal.Ink, 38);
            s.savePhoto = LessonPlayerBuilder.Raised(safe, "Save", "done", Pal.Green, 1530, 640, 340, 100, A.R, null, 42);
        }

        /// <summary>Dark veil with a soft oval hole, stretched over the photo viewport.</summary>
        private static void EnsureHoleSprite()
        {
            if (File.Exists(HolePath)) return;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - n / 2f) / (n / 2f * OvalShare), dy = (y + 0.5f - n / 2f) / (n / 2f * OvalShare);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((r - 1f) / 0.02f) * 0.62f;
                px[y * n + x] = new Color32(20, 14, 45, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(HolePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(HolePath);
            Shapes.Import(HolePath, 0, 512);
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
