using System.IO;
using FairyTales.UI.Core;
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
    /// Builds «Раскраски» (<see cref="ColoringScreen"/>) in the sticker look of the lessons home:
    /// the gallery of pages, and the page with the crayons on the right.
    ///
    ///   Nooria ▸ Lessons ▸ Build coloring — import the pages, the prefab, its instance in SampleScene
    ///
    /// The pages themselves (Resources/Coloring) and the icons come from Tools/coloring.
    /// </summary>
    public static class ColoringBuilder
    {
        public const string Prefab = "Assets/Prefabs/Lessons/ColoringScreen.prefab";
        private const string ArtDir = "Assets/UI/Lessons/Art";
        private const string PagesDir = "Assets/Resources/Coloring";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        /// <summary>The crayons, in palette order; white rubs colour out.</summary>
        private static readonly string[] Palette =
        {
            "E8453C", "FF9A2E", "FFD93B",
            "8BD450", "2FA84F", "5BC4F0",
            "3D6FD9", "9B59D0", "FF8FB8",
            "A0643C", "FFD3B0", "9AA0A8",
            "3A3346", "FFFFFF",
        };

        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        [MenuItem("Nooria/Lessons/Build coloring")]
        public static string BuildAll()
        {
            ImportPages();
            var msg = Build();
            return msg + "; " + InstallInScene();
        }

        /// <summary>Line art as plain textures with mipmaps (shown big on the page and small in the gallery).</summary>
        public static void ImportPages()
        {
            foreach (var n in new[] { "icon_bucket", "icon_crayon", "icon_undo", "icon_magic", "coloring_tile" })
            {
                var path = $"{ArtDir}/{n}.png";
                if (AssetImporter.GetAtPath(path) is TextureImporter ti
                    && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
                    Shapes.Import(path, 0, 1024, compress: true);
            }
            foreach (var file in Directory.GetFiles(PagesDir, "*_lines.png"))
            {
                var path = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                ti.textureType = TextureImporterType.Default;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.maxTextureSize = 2048;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
        }

        public static string Build()
        {
            Shapes.Ensure();
            LoadFonts();
            Directory.CreateDirectory(Path.GetDirectoryName(Prefab)!);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("Holder", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(holder, scene);
                var root = Root("ColoringScreen", holder.transform);
                root.Go.AddComponent<Canvas>();
                root.Go.AddComponent<CanvasGroup>();
                root.Go.AddComponent<GraphicRaycaster>();
                var s = root.Go.AddComponent<ColoringScreen>();
                BuildScreen(root, s);
                PrefabUtility.SaveAsPrefabAsset(root.Go, Prefab);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            return $"[Coloring] built {Prefab}";
        }

        private static void BuildScreen(Box r, ColoringScreen s)
        {
            AddImage(Fill(r, "Background"), Shapes.Gradient("cream_pink_c", Pal.Hex("FFF3DA"), Pal.Hex("FFE0EA")), Color.white);
            Paper(r, Pal.Hex("D9B9A0"), 0.14f);
            var safe = Fill(r, "Safe");
            safe.Go.AddComponent<SafeAreaFitter>();
            BuildGallery(safe, s);
            BuildBoard(safe, s);
        }

        // ─────────────────────────── gallery ───────────────────────────

        private const float CardW = 400, CardH = 330, PicW = 340, PicH = 255;

        private static void BuildGallery(Box safe, ColoringScreen s)
        {
            var g = Fill(safe, "Gallery");
            s.gallery = g.Go;
            s.home = IconButton(g, "Home", Shapes.Home, Color.white, Pal.MintDark, 96, 92, 112, A.TL, 0.5f).Go.GetComponent<Button>();
            Label(AutoFit(Txt(g, "", FontXB, 56, Pal.Ink, 600, 94, 760, 90, TA.Left, A.TL), 36), "coloring_title");

            // Pages in two rows, scrolling sideways, from under the title to the bottom. The
            // view runs past the safe area to the screen's edges, so cards slide out of sight
            // there instead of being cut at the notch.
            const float bleed = 160;
            var view = Node(g, "Scroll", 960, 600, 1920, 760, A.C);
            view.Rt.anchorMin = new Vector2(0, 0);
            view.Rt.anchorMax = new Vector2(1, 1);
            view.Rt.offsetMin = new Vector2(-bleed, 0);
            view.Rt.offsetMax = new Vector2(bleed, -150);
            view.Go.AddComponent<RectMask2D>();
            AddImage(view, null, new Color(1, 1, 1, 0)).raycastTarget = true;
            var scroll = view.Go.AddComponent<ScrollRect>();
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            s.galleryScroll = scroll;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view.Rt, false);
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(1920, 0);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CardW + 30, CardH + 30);
            grid.spacing = new Vector2(20, 8);
            grid.padding = new RectOffset(110 + (int)bleed, 110 + (int)bleed, 24, 24);
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view.Rt;
            scroll.content = content;
            s.cards = content;

            // The card template: a white sticker, the picture under the lines, the title below.
            var c = new Box { Rt = content, W = 1920, H = 760 };
            var holder = Node(c, "CardTemplate", (CardW + 30) / 2, (CardH + 30) / 2, CardW + 30, CardH + 30);
            AddImage(holder, null, new Color(1, 1, 1, 0)).raycastTarget = true;
            var card = holder.Go.AddComponent<ColoringCard>();
            card.button = holder.Go.AddComponent<Button>();
            var face = Sticker(holder, "Card", (CardW + 30) / 2, (CardH + 30) / 2 - 4, CardW, CardH, 40, Color.white, 8, A.C, 0.06f);
            var pic = Node(face, "Picture", CardW / 2, 22 + PicH / 2, PicW, PicH);
            card.picture = Raw(pic, "Paint");
            card.lines = Raw(pic, "Lines");
            card.title = AutoFit(Txt(face, "Мечеть", FontXB, 30, Pal.Ink, CardW / 2, CardH - 30, CardW - 40, 46), 20);
            s.cardTemplate = card;
        }

        private static RawImage Raw(Box p, string name)
        {
            var b = Fill(p, name);
            var img = b.Go.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        // ─────────────────────────── the page ───────────────────────────

        private static void BuildBoard(Box safe, ColoringScreen s)
        {
            var b = Fill(safe, "Board");
            s.board = b.Go;
            s.back = IconButton(b, "Back", Shapes.Arrow, Color.white, Pal.Sub, 96, 92, 112, A.TL, 0.42f).Go.GetComponent<Button>();

            // The picture: a white card, 4:3 like the pages.
            const float pw = 1120, ph = 840, cx = 790, cy = 540;
            var face = Sticker(b, "Picture", cx, cy, pw + 24, ph + 24, 40, Color.white, 10, A.C, 0f);
            var paintBox = Node(face, "Paint", (pw + 24) / 2, (ph + 24) / 2, pw, ph);
            var paint = paintBox.Go.AddComponent<RawImage>();
            paint.raycastTarget = true;
            var canvas = paintBox.Go.AddComponent<ColoringCanvas>();
            canvas.paint = paint;
            canvas.lines = Raw(paintBox, "Lines");
            s.canvas = canvas;

            var spark = Node(b, "Sparkles", cx, cy, pw, ph);
            s.sparkles = spark.Rt;
            var tpl = Img(spark, "Sparkle", Shapes.Sparkle, pw / 2, ph / 2, 70, 70);
            s.sparkleTemplate = tpl.Go.GetComponent<Image>();

            var banner = Node(b, "Done", cx, cy + ph / 2 - 40, 820, 150);
            banner.Go.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var bannerFace = Sticker(banner, "Card", 410, 75, 780, 116, 58, Pal.Rose, 8, A.C, 0.1f);
            Label(AutoFit(Txt(bannerFace, "", FontXB, 50, Color.white, 390, 56, 720, 100), 30), "coloring_done");
            Sparkle(banner, 30, 30, 56, A.C, Pal.Hex("FFC94A"));
            Sparkle(banner, 790, 120, 44, A.C, Pal.Hex("FFC94A"));
            s.doneBanner = banner.Rt;

            // On the right: the tools, then the crayons, on a white panel.
            const float px = 1652, top = 64;
            var panel = Sticker(b, "Panel", px, 548, 452, 880, 48, Color.white, 10, A.R, 0.08f);
            float tx(int col) => 76 + col * 150;
            var fill = Tool(panel, "Fill", Art("icon_bucket"), tx(0), top + 40);
            var crayon = Tool(panel, "Crayon", Art("icon_crayon"), tx(1), top + 40);
            var undo = Tool(panel, "Undo", Art("icon_undo"), tx(2), top + 40);
            s.fillTool = fill.button; s.fillToolFace = fill.face;
            s.crayonTool = crayon.button; s.crayonToolFace = crayon.face;
            s.undo = undo.button;
            s.undoGroup = undo.button.gameObject.AddComponent<CanvasGroup>();
            Rect(panel, "Divider", Pal.Hex("EFE7F7"), 226, top + 128, 380, 6, 3);

            const float d = 104, step = 130;
            var list = new System.Collections.Generic.List<ColoringSwatch>();
            for (int i = 0; i <= Palette.Length; i++)
            {
                int col = i % 3, row = i / 3;
                float x = 96 + col * step, y = top + 218 + row * 126;
                bool isMagic = i == Palette.Length;
                var c = isMagic ? Color.white : Pal.Hex(Palette[i]);
                var sw = Swatch(panel, isMagic ? "Magic" : "Crayon " + Palette[i], c, x, y, d, isMagic ? Art("icon_magic") : null);
                if (isMagic) s.magic = sw;
                else list.Add(sw);
            }
            s.swatches = list.ToArray();
        }

        private static (Button button, Image face) Tool(Box p, string name, Sprite icon, float cx, float cy)
        {
            const float d = 112;
            var holder = Node(p, name, cx, cy, d + 20, d + 20);
            Shadow(holder, (d + 20) / 2, (d + 20) / 2 + 4, d * 0.8f, d * 0.8f, 0.14f, 8);
            var face = Circle(holder, "Face", Color.white, (d + 20) / 2, (d + 20) / 2, d);
            var img = face.Go.GetComponent<Image>();
            img.raycastTarget = true;
            OutlineRound(face);
            Img(face, "Icon", icon, d / 2, d / 2, d * 0.66f, d * 0.66f);
            return (holder.Go.AddComponent<Button>(), img);
        }

        private static ColoringSwatch Swatch(Box p, string name, Color c, float cx, float cy, float d, Sprite icon)
        {
            var holder = Node(p, name, cx, cy, d + 24, d + 24);
            var ring = Circle(holder, "Ring", Pal.Ink, (d + 24) / 2, (d + 24) / 2, d + 22);
            Circle(ring, "Gap", Color.white, (d + 22) / 2, (d + 22) / 2, d + 10);
            var face = Circle(holder, "Face", c, (d + 24) / 2, (d + 24) / 2, d);
            var img = face.Go.GetComponent<Image>();
            img.raycastTarget = true;
            OutlineRound(face);
            if (icon != null) Img(face, "Icon", icon, d / 2, d / 2, d * 0.74f, d * 0.74f);
            var sw = holder.Go.AddComponent<ColoringSwatch>();
            sw.button = holder.Go.AddComponent<Button>();
            sw.button.targetGraphic = img;
            sw.color = c;
            sw.ring = ring.Go;
            ring.Go.SetActive(false);
            return sw;
        }

        private static TMPro.TMP_Text AutoFit(TMPro.TMP_Text t, float min)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = min;
            t.enableWordWrapping = false;
            return t;
        }

        private static TMPro.TMP_Text Label(TMPro.TMP_Text t, string key)
        {
            t.gameObject.AddComponent<LessonLabel>().key = key;
            t.text = LessonStrings.T(key);
            return t;
        }

        // ─────────────────────────── scene ───────────────────────────

        public static string InstallInScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var sm = Object.FindAnyObjectByType<ScreenManager>(FindObjectsInactive.Include);
            if (sm == null) return "no ScreenManager in the scene";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null) return "no prefab — build it first";

            var player = sm.GetComponentInChildren<LessonPlayerScreen>(true);
            foreach (var old in sm.GetComponentsInChildren<ColoringScreen>(true))
                Object.DestroyImmediate(old.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sm.transform);
            inst.name = prefab.name;
            if (player != null) inst.transform.SetSiblingIndex(player.transform.GetSiblingIndex());
            var rt = (RectTransform)inst.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            inst.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "[Coloring] installed";
        }
    }
}
