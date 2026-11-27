using System.IO;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
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
    /// Builds the lessons home (design 29 «Урок дня + разделы») and «Все уроки»
    /// (design 30) and «Дерево добрых дел» (design 14) as screens of the app, and
    /// gives the tales library a home button.
    ///
    ///   Nooria ▸ Lessons ▸ Build lessons home            — the three prefabs
    ///   Nooria ▸ Lessons ▸ Install lessons home in scene — instances under [UICanvas] + library home button
    ///   Nooria ▸ Lessons ▸ Render lessons home PNGs      — Design/Home/*.png with sample content
    /// </summary>
    public static class LessonHomeBuilder
    {
        public const string HomePrefab = "Assets/Prefabs/Lessons/LessonHomeScreen.prefab";
        public const string ListPrefab = "Assets/Prefabs/Lessons/LessonListScreen.prefab";
        public const string TreePrefab = "Assets/Prefabs/Lessons/LessonTreeScreen.prefab";
        private const string ArtDir = "Assets/UI/Lessons/Art";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PngDir = "Design/Home";

        private static readonly Color TYellow = Pal.Hex("FFE08A");
        private static readonly Color TSky = Pal.Hex("A9DCF6");
        private static readonly Color TMint = Pal.Hex("BDEBD2");

        private static Sprite Art(string name)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png")
               ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.jpg");

        // ─────────────────────────── build ───────────────────────────

        [MenuItem("Nooria/Lessons/Build lessons home")]
        public static string Build()
        {
            Shapes.Ensure();
            LoadFonts();
            foreach (var n in new[] { "home_button", "cover_night", "cover_bismillah", "tree_full", "reward_star", "topic_mosque",
                         "tree_stage1", "tree_stage2", "tree_stage3", "tree_stage4", "leaf_green", "leaf_gold", "topic_sleep", "icon_camera" })
                ForceSingle($"{ArtDir}/{n}");
            Directory.CreateDirectory(Path.GetDirectoryName(HomePrefab)!);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("Holder", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(holder, scene);
                var home = Screen<LessonHomeScreen>(holder.transform, "LessonHomeScreen");
                BuildHome(home.root, home.screen);
                PrefabUtility.SaveAsPrefabAsset(home.root.Go, HomePrefab);

                var list = Screen<LessonListScreen>(holder.transform, "LessonListScreen");
                BuildList(list.root, list.screen);
                PrefabUtility.SaveAsPrefabAsset(list.root.Go, ListPrefab);

                var tree = Screen<LessonTreeScreen>(holder.transform, "LessonTreeScreen");
                BuildTree(tree.root, tree.screen);
                PrefabUtility.SaveAsPrefabAsset(tree.root.Go, TreePrefab);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            return $"[LessonHome] built {HomePrefab}, {ListPrefab}, {TreePrefab}";
        }

        private static void ForceSingle(string pathNoExt)
        {
            var path = File.Exists(pathNoExt + ".png") ? pathNoExt + ".png" : pathNoExt + ".jpg";
            if (AssetImporter.GetAtPath(path) is TextureImporter ti
                && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
                Shapes.Import(path, 0, 2048, compress: true);
        }

        private static (Box root, T screen) Screen<T>(Transform parent, string name) where T : BaseScreen
        {
            var root = Root(name, parent);
            root.Go.AddComponent<Canvas>();
            root.Go.AddComponent<CanvasGroup>();
            root.Go.AddComponent<GraphicRaycaster>();
            return (root, root.Go.AddComponent<T>());
        }

        private static void Painted(Box r, string name, Color top, Color bottom, float paper)
        {
            AddImage(Fill(r, "Background"), Shapes.Gradient(name, top, bottom), Color.white);
            Paper(r, Pal.Darker(bottom, 0.7f), paper);
        }

        private static Box Safe(Box r)
        {
            var b = Fill(r, "Safe");
            b.Go.AddComponent<SafeAreaFitter>();
            return b;
        }

        private static LessonTileView View(TileParts t)
        {
            var v = t.Holder.Go.AddComponent<LessonTileView>();
            v.button = t.Button;
            v.picture = t.Picture;
            v.fit = t.Fit;
            v.label = t.Label;
            v.labelPill = t.LabelPill;
            v.play = t.Play;
            v.locked = t.Lock;
            v.done = t.Done;
            return v;
        }

        private static TMP_Text PillText(Box pill) => pill.Go.GetComponentInChildren<TMP_Text>();

        private static TMP_Text Label(TMP_Text t, string key)
        {
            t.gameObject.AddComponent<LessonLabel>().key = key;
            t.text = LessonStrings.T(key);
            return t;
        }

        // ─────────────────────────── home ───────────────────────────

        private static void BuildHome(Box r, LessonHomeScreen s)
        {
            Painted(r, "lav_peach_c", Pal.Hex("E4D9FF"), Pal.Hex("FFE1CC"), 0.14f);
            var safe = Safe(r);

            // Top: the child (opens the avatar), stars, leaves; «Для родителей» on the right.
            Circle(safe, "AvatarRing", Color.white, 108, 100, 132, A.TL);
            s.avatar = AvatarScreenBuilder.Avatar(safe, "Avatar", AvatarPerson.Child, 108, 100, 118, 118, A.TL, true, Pal.Lav);
            var avatarImage = s.avatar.GetComponent<Image>();
            avatarImage.raycastTarget = true;
            s.avatarButton = s.avatar.gameObject.AddComponent<Button>();
            s.stars = PillText(Pill(safe, Art("reward_star"), "12", 292, 100, 168, A.TL));
            s.leaves = PillText(Pill(safe, Art("leaf_green"), "3", 460, 100, 146, A.TL));
            // Days in a row, in a holder so the pill's shadow hides with it: no chip (and no
            // «0 days» reproach) until the first lesson, or after a day without one.
            var streak = Node(safe, "Streak", 617, 100, 170, 100, A.TL);
            s.streakChip = streak.Go;
            s.streak = PillText(Pill(streak, Art("icon_flame"), "2", 85, 50, 146));

            const float pw = 384, ph = 84;
            Shadow(safe, 1700, 100, pw, ph, 0.12f, 8, A.TR);
            var parents = Rect(safe, "Parents", Color.white, 1700, 100, pw, ph, ph / 2, A.TR);
            parents.Go.GetComponent<Image>().raycastTarget = true;
            s.parents = parents.Go.AddComponent<Button>();
            Label(AutoFit(Txt(parents, "", FontXB, 32, Pal.MintDark, 160, ph / 2 - 1, 280, ph), 22), "home_parents");
            Img(parents, "Icon", Art("topic_family"), pw - 52, ph / 2, 64, 64);

            // The lesson of the day, big, with ▶ on its corner.
            s.day = View(Tile(safe, "LessonOfDay", 540, 610, 780, 700, Pal.Rose, Art("cover_bismillah"), true, A.C, 60,
                "Бисмиллях", 42, play: true, allBadges: true));
            var badge = Rect(safe, "DayBadge", Pal.Rose, 350, 262, 260, 70, 35);
            Label(AutoFit(Txt(badge, "", FontXB, 34, Color.white, 130, 34, 240, 70), 24), "home_lesson_day");
            Sparkle(safe, 168, 330, 44, A.C, Pal.Hex("FFC94A"));
            Sparkle(safe, 945, 290, 32, A.C, Pal.Hex("FFC94A"));

            // The sections.
            s.all = View(Tile(safe, "AllLessons", 1190, 420, 400, 340, TYellow, Art("topic_mosque"), false, A.C, 52, "Все уроки", 30, allBadges: true));
            s.tales = View(Tile(safe, "Tales", 1630, 420, 400, 340, Pal.Night2, Art("cover_night"), true, A.C, 52, "Сказки", 30, allBadges: true));
            s.tree = View(Tile(safe, "Tree", 1190, 820, 400, 340, TMint, Art("tree_full"), false, A.C, 52, "Дерево", 30, allBadges: true));
            s.games = View(Tile(safe, "Games", 1630, 820, 400, 340, TSky, Art("reward_star"), false, A.C, 52, "Игры", 30, locked: true, allBadges: true));

            // «Ждёт отметки: 1» hanging on the tree tile's top edge, for the parent.
            const float cw = 330, ch = 64;
            var chip = Node(safe, "DeedsChip", 1190, 652, cw + 40, ch + 40);
            Shadow(chip, (cw + 40) / 2, (ch + 40) / 2, cw, ch, 0.16f, 8);
            var chipFace = Rect(chip, "Face", Pal.Orange, (cw + 40) / 2, (ch + 40) / 2, cw, ch, ch / 2);
            Circle(chipFace, "LeafRing", Color.white, 34, ch / 2, 52);
            Img(chipFace, "Leaf", Art("leaf_gold"), 34, ch / 2, 38, 38);
            s.deedsChipText = AutoFit(Txt(chipFace, "Ждёт отметки: 1", FontXB, 28, Color.white, 70 + (cw - 84) / 2, ch / 2 - 1, cw - 90, ch), 20);
            s.deedsChip = chip.Go;

            BuildAvatarOffer(r, s);
        }

        // ─────────────────────────── avatar offer ───────────────────────────

        /// <summary>
        /// «Сделаем тебя героем!» over the home: the child's bust with a camera badge on
        /// a sticker card, «Сделать» opens the avatar screen, «Потом» closes it.
        /// </summary>
        private static void BuildAvatarOffer(Box r, LessonHomeScreen s)
        {
            var offer = Fill(r, "AvatarOffer");
            offer.Go.AddComponent<CanvasGroup>();
            AddImage(offer, null, new Color(0.17f, 0.13f, 0.32f, 0.55f)).raycastTarget = true;
            s.avatarOffer = offer.Go;

            const float w = 940, h = 560;
            var cardHolder = Node(offer, "Card", 960, 600, w + 200, h + 360);
            s.avatarOfferCard = cardHolder.Rt;
            float x = (w + 200) / 2, y = (h + 360) / 2 + 60;
            var card = Sticker(cardHolder, "Card", x, y, w, h, 56, Color.white, 14);

            // The bust sits on the card's top edge.
            float top = y - h / 2;
            Circle(cardHolder, "AvatarRing", Color.white, x, top, 262);
            SketchFrame(cardHolder, x, top, 262, 262, 131);
            AvatarScreenBuilder.Avatar(cardHolder, "Avatar", AvatarPerson.Child, x, top, 240, 240, A.C, true, Pal.Lav);
            CornerBadge(cardHolder, "Camera", x + 92, top + 84, 84, Color.white, Art("icon_camera"), Color.white, A.C, 0.62f);

            Label(AutoFit(Txt(card, "", FontXB, 56, Pal.Ink, w / 2, 200, w - 120, 80), 36), "home_avatar_title");
            Label(Txt(card, "", FontM, 34, Pal.Sub, w / 2, 290, w - 200, 100), "home_avatar_text");
            var go = Button(card, "Go", Pal.Rose, 320, 450, 400, 104, A.C, null, 42);
            Label(PillText(go), "home_avatar_go");
            s.avatarOfferGo = go.Go.GetComponent<Button>();
            var later = Button(card, "Later", Pal.LavSoft, 690, 450, 260, 104, A.C, Pal.Ink, 38);
            Label(PillText(later), "home_avatar_later");
            s.avatarOfferLater = later.Go.GetComponent<Button>();
            offer.Go.SetActive(false);
        }

        // ─────────────────────────── tree ───────────────────────────

        private const float RowW = 800, ListW = 840;

        private static void BuildTree(Box r, LessonTreeScreen s)
        {
            Painted(r, "sky_mint_tree", Pal.Hex("CDE7FF"), Pal.Hex("DCF4E2"), 0.16f);
            var safe = Safe(r);

            var homeBtn = IconButton(safe, "Home", Shapes.Home, Color.white, Pal.MintDark, 96, 92, 112, A.TL, 0.5f);
            s.home = homeBtn.Go.GetComponent<Button>();
            Label(AutoFit(Txt(safe, "", FontXB, 56, Pal.Ink, 600, 94, 760, 90, TA.Left, A.TL), 36), "tree_title");
            s.stars = PillText(Pill(safe, Art("reward_star"), "12", 1606, 92, 168, A.TR));
            s.leaves = PillText(Pill(safe, Art("leaf_green"), "3", 1772, 92, 146, A.TR));

            // The tree stands on the bottom of its area (from under the title to the
            // screen's bottom: wide phones are shorter), a soft light behind its crown.
            const float th = 840, tw = th * 745f / 1006f;
            var area = Node(safe, "TreeArea", 520, 600, 900, th, A.L);
            StretchY(area.Rt, 160, 22);
            s.treeArea = area.Rt;
            var glow = Img(area, "Glow", Shapes.Radial("tree_glow", new Color(1, 1, 1, 0.8f), new Color(1, 1, 1, 0)), 450, th * 0.45f, 1000, 1000, A.C, null, false);
            glow.Rt.anchorMin = glow.Rt.anchorMax = new Vector2(0.5f, 0.55f);
            glow.Rt.anchoredPosition = Vector2.zero;
            var tree = Img(area, "Tree", Art("tree_stage4"), 450, th - th / 2, tw, th, A.B, null, false);
            tree.Rt.pivot = new Vector2(0.5f, 0f);
            tree.Rt.anchoredPosition = Vector2.zero;
            s.tree = tree.Go.GetComponent<Image>();
            s.treeMaxHeight = th;
            var leafLayer = Fill(tree, "Leaves");
            s.leafLayer = leafLayer.Rt;
            s.leafImage = Img(leafLayer, "LeafTemplate", Art("leaf_green"), tw / 2, th / 2, 80, 100, A.C, null, false).Go.GetComponent<Image>();
            s.stages = new[] { Art("tree_stage1"), Art("tree_stage2"), Art("tree_stage3"), Art("tree_stage4") };
            s.leafGreen = Art("leaf_green");
            s.leafGold = Art("leaf_gold");

            // How far to the next growth.
            var growth = Sticker(safe, "Growth", 1400, 250, RowW, 150, 44, Color.white, 10, A.TR);
            Circle(growth, "IconBg", Pal.Mint, 88, 75, 116);
            s.growthIcon = Img(growth, "Icon", Art("tree_stage2"), 88, 75, 92, 92).Go.GetComponent<Image>();
            s.growthText = AutoFit(Txt(growth, "Ещё 2 листка — и дерево подрастёт", FontXB, 34, Pal.Ink, 480, 54, 600, 56, TA.Left), 24);
            var track = Rect(growth, "Track", Pal.Hex("E2F1E7"), 480, 106, 600, 26, 13);
            var fill = Rect(track, "Fill", Pal.Green, 150, 13, 300, 26, 13, A.L);
            fill.Rt.pivot = new Vector2(0f, 0.5f);
            fill.Rt.anchoredPosition = Vector2.zero;
            s.growthFill = fill.Rt;

            // The list: deeds waiting for the tick, then the leaves.
            var view = Node(safe, "Scroll", 1400, 700, ListW, 660, A.R);
            StretchY(view.Rt, 362, 10);
            view.Go.AddComponent<RectMask2D>().padding = new Vector4(-30, 0, -30, 0);
            var scroll = view.Go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            AddImage(view, null, new Color(1, 1, 1, 0)).raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view.Rt, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 600);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.padding = new RectOffset(0, 0, 4, 40);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = false;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view.Rt;
            scroll.content = content;
            s.scroll = scroll;
            s.content = content;
            var c = new Box { Rt = content, W = ListW, H = 600 };

            s.waitingHeader = Header(c, "WaitingHeader", "tree_waiting");
            s.deedTemplate = Row(c, "DeedTemplate", Pal.Hex("FFF3D1"), Art("leaf_gold"), "Сказали Бисмиллях вместе", true);
            s.leavesHeader = Header(c, "LeavesHeader", "tree_leaves");
            s.leafRowTemplate = Row(c, "LeafTemplate", Color.white, Art("leaf_green"), "Бисмиллях", false);

            var empty = Node(c, "Empty", ListW / 2, 70, ListW, 150);
            var hint = Sticker(empty, "Hint", ListW / 2, 66, RowW, 120, 40, Pal.Hex("F1FAF4"), 8);
            Img(hint, "Moon", Art("topic_sleep"), 72, 60, 84, 84);
            Label(Txt(hint, "", FontB, 30, Pal.Ink, 450, 60, 620, 100, TA.Left), "tree_empty");
            s.empty = empty.Go;
        }

        /// <summary>Stretches a node from <paramref name="top"/> to <paramref name="bottom"/> of its parent, keeping its x.</summary>
        private static void StretchY(RectTransform rt, float top, float bottom)
        {
            float ax = rt.anchorMin.x, x = rt.anchoredPosition.x, w = rt.sizeDelta.x;
            rt.anchorMin = new Vector2(ax, 0);
            rt.anchorMax = new Vector2(ax, 1);
            rt.sizeDelta = new Vector2(w, -(top + bottom));
            rt.anchoredPosition = new Vector2(x, (bottom - top) / 2);
        }

        private static GameObject Header(Box c, string name, string key)
        {
            var b = Node(c, name, ListW / 2, 40, ListW, 76);
            Label(AutoFit(Txt(b, "", FontXB, 40, Pal.Ink, ListW / 2, 44, RowW - 20, 64, TA.Left), 28), key);
            return b.Go;
        }

        private static LessonTreeRow Row(Box c, string name, Color fill, Sprite leaf, string title, bool deed)
        {
            const float h = 96;
            var holder = Node(c, name, ListW / 2, 60, ListW, h + 22);
            var face = Sticker(holder, "Row", ListW / 2, h / 2 + 4, RowW, h, 34, fill, 7);
            var row = holder.Go.AddComponent<LessonTreeRow>();
            row.icon = Img(face, "Leaf", leaf, 58, h / 2, 64, 64).Go.GetComponent<Image>();
            float right = deed ? 250 : 230;
            var t = Txt(face, title, FontXB, 32, Pal.Ink, 110 + (RowW - 110 - right) / 2, h / 2, RowW - 110 - right, h - 12, TA.Left);
            t.enableAutoSizing = true;
            t.fontSizeMax = 32;
            t.fontSizeMin = 22;
            t.overflowMode = TextOverflowModes.Ellipsis;
            row.title = t;
            if (deed)
            {
                var b = Button(face, "Mark", Pal.MintDark, RowW - 128, h / 2 - 3, 210, 66, A.C, null, 30);
                Label(AutoFit(PillText(b), 20), "tree_mark");
                row.button = b.Go.GetComponent<Button>();
            }
            else
            {
                row.note = AutoFit(Txt(face, "новый!", FontM, 26, Pal.Sub, RowW - 125, h / 2, 210, h, TA.Right), 18);
            }
            return row;
        }


        private static TMP_Text AutoFit(TMP_Text t, float min)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = min;
            t.enableWordWrapping = false;
            return t;
        }

        // ─────────────────────────── all lessons ───────────────────────────

        private const float TileW = 420, TileH = 340, TileMargin = 80;

        private static void BuildList(Box r, LessonListScreen s)
        {
            Painted(r, "meadow", Pal.Hex("DDF2B0"), Pal.Hex("BCE28C"), 0.22f);
            var safe = Safe(r);

            var homeBtn = IconButton(safe, "Home", Shapes.Home, Color.white, Pal.MintDark, 96, 92, 112, A.TL, 0.5f);
            s.home = homeBtn.Go.GetComponent<Button>();
            s.stars = PillText(Pill(safe, Art("reward_star"), "12", 1780, 92, 168, A.TR));
            Label(AutoFit(Txt(safe, "", FontXB, 54, Pal.Ink, 960, 92, 900, 90, TA.Center, A.T), 34), "home_all");

            // Vertical scroll of tiles, three to a row.
            var view = Node(safe, "Scroll", 960, 620, 1920, 900, A.Fill);
            view.Rt.offsetMax = new Vector2(0, -170);
            view.Rt.offsetMin = Vector2.zero;
            view.Go.AddComponent<RectMask2D>();
            var scroll = view.Go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            var img = AddImage(view, null, new Color(1, 1, 1, 0));
            img.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view.Rt, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 600);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TileW + 2 * TileMargin, TileH + 2 * TileMargin);
            grid.spacing = new Vector2(-2 * TileMargin + 60, -2 * TileMargin + 50);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(0, 0, -40, 40);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view.Rt;
            scroll.content = content;
            s.scroll = scroll;
            s.content = content;

            var contentBox = new Box { Rt = content, W = 1920, H = 600 };
            var t = Tile(contentBox, "Template", 960, 300, TileW, TileH, Pal.Lav, Art("cover_bismillah"), true, A.C, 52,
                "Бисмиллях", 30, allBadges: true);
            t.Holder.Go.AddComponent<CanvasGroup>();
            s.template = View(t);

            s.empty = Label(AutoFit(Txt(safe, "", FontB, 40, Pal.Sub, 960, 540, 1200, 120, TA.Center), 28), "home_no_lessons").gameObject;
            s.empty.SetActive(false);
        }

        // ─────────────────────────── scene ───────────────────────────

        /// <summary>
        /// Puts the screens under [UICanvas] (before the lesson player), gives the tales
        /// library a home button left of its settings, and saves SampleScene.
        /// </summary>
        [MenuItem("Nooria/Lessons/Install lessons home in scene")]
        public static string InstallInScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var sm = Object.FindAnyObjectByType<ScreenManager>(FindObjectsInactive.Include);
            if (sm == null) return "no ScreenManager in the scene";

            var player = sm.GetComponentInChildren<LessonPlayerScreen>(true);
            foreach (var path in new[] { HomePrefab, ListPrefab, TreePrefab })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) return "no prefab — build it first";
                var type = prefab.GetComponent<BaseScreen>().GetType();
                foreach (var old in sm.GetComponentsInChildren(type, true))
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
            }

            var msg = AddLibraryHomeButton(sm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "[LessonHome] installed; " + msg;
        }

        /// <summary>
        /// The library's top-left buttons (settings, mail) move into a row with a home
        /// button in front; the row closes up when the home button hides (languages
        /// without lessons), so those keep their old look.
        /// </summary>
        private static string AddLibraryHomeButton(ScreenManager sm)
        {
            var lib = sm.GetComponentInChildren<LibraryScreen>(true);
            if (lib == null) return "no library";
            var so = new SerializedObject(lib);
            var settings = so.FindProperty("btnSettings").objectReferenceValue as Button;
            var mail = so.FindProperty("btnEmail").objectReferenceValue as Button;
            if (settings == null) return "no settings button";

            var bar = settings.transform.parent;
            var row = bar.Find("LeftButtons") as RectTransform;
            if (row == null)
            {
                row = new GameObject("LeftButtons", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(bar, false);
                var srt = (RectTransform)settings.transform;
                row.anchorMin = row.anchorMax = new Vector2(0, 1);
                row.pivot = new Vector2(0, 0.5f);
                row.anchoredPosition = new Vector2(srt.anchoredPosition.x - srt.sizeDelta.x / 2, srt.anchoredPosition.y);
                row.sizeDelta = new Vector2(700, srt.sizeDelta.y);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = mail != null
                    ? ((RectTransform)mail.transform).anchoredPosition.x - srt.anchoredPosition.x - srt.sizeDelta.x
                    : 30;
                h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = h.childControlHeight = false;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
            }

            var homeT = row.Find("BtnHome");
            GameObject home;
            if (homeT == null)
            {
                home = Object.Instantiate(settings.gameObject, row);
                home.name = "BtnHome";
                home.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
            }
            else home = homeT.gameObject;
            home.GetComponent<Image>().sprite = Art("home_button");
            home.transform.SetSiblingIndex(0);
            settings.transform.SetParent(row, false);
            settings.transform.SetSiblingIndex(1);
            if (mail != null) { mail.transform.SetParent(row, false); mail.transform.SetSiblingIndex(2); }

            so.FindProperty("btnHome").objectReferenceValue = home.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return "library home button set";
        }
    }
}
