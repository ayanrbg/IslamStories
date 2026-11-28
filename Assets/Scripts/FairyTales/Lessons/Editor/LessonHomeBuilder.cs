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
                         "tree_stage1", "tree_stage2", "tree_stage3", "tree_stage4", "leaf_green", "leaf_gold", "topic_sleep", "icon_camera", "coloring_tile", "trace_tile", "char_dad", "char_girl", "topic_family", "icon_flame",
                         "card_trace", "card_coloring", "card_tree", "hero_bg", "badge_burst", "hi_lines", "pattern_sparkles",
                         "topic_food", "topic_kindness", "topic_dua", "topic_mosque", "topic_animals" })
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

        // The page runs past the safe area to the screen's edges (cards slide out of sight
        // there, not at the notch); rows start at the safe edge plus a margin.
        private const float Bleed = 160, RowPad = 110, TileM = 80, Gap = 40;
        private const float PicksH = 320, FamilyH = 280, TopicH = 300, TitleH = 84;
        /// <summary>Room under the first row: its ▶ hangs below the cards and must clear the family title.</summary>
        private const float PicksBelow = 110;

        private static void BuildHome(Box r, LessonHomeScreen s)
        {
            Painted(r, "sun_peach_c", Pal.Hex("FFE6A6"), Pal.Hex("FFCB8E"), 0.2f);
            var safe = Safe(r);

            // ── the page: rows of cards, scrolling down under the top bar ──
            var view = Node(safe, "Page", 960, 600, 1920, 900);
            view.Rt.anchorMin = Vector2.zero;
            view.Rt.anchorMax = Vector2.one;
            view.Rt.offsetMin = new Vector2(-Bleed, -120); // and under the home indicator
            view.Rt.offsetMax = new Vector2(Bleed, -158);
            view.Go.AddComponent<RectMask2D>();
            AddImage(view, null, new Color(1, 1, 1, 0)).raycastTarget = true;
            var page = view.Go.AddComponent<ScrollRect>();
            page.horizontal = false;
            page.movementType = ScrollRect.MovementType.Elastic;
            s.page = page;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(view.Rt, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 1400);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(0, 0, 0, 200);
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            page.viewport = view.Rt;
            page.content = content;
            s.sections = content;
            var c = new Box { Rt = content, W = 1920 + 2 * Bleed, H = 1400 };

            // ── the sea, BEHIND every row: a band of water from under the first row down, like the
            //    beach in Speech Blubs. Out of the layout; the first row's cards and ▶ overlap its edge. ──
            const float seaTop = TitleH + PicksH + PicksBelow - 30;
            var backdrop = Node(c, "Backdrop", c.W / 2, 2000, c.W, 4000, A.T);
            backdrop.Go.AddComponent<LayoutElement>().ignoreLayout = true;
            Img(backdrop, "Foam", Shapes.Wave, c.W / 2 - 60, seaTop - 16, c.W + 400, 128, A.T, new Color(1, 1, 1, 0.85f), false);
            Img(backdrop, "Sea", Shapes.Wave, c.W / 2, seaTop, c.W + 400, 128, A.T, Pal.Hex("A9DCF6"), false);
            var water = Rect(backdrop, "Water", Pal.Hex("A9DCF6"), c.W / 2, seaTop + 64 + 1700, c.W + 400, 3400, 0, A.T);
            Paper(water, Pal.Hex("6FB3DE"), 0.14f);
            var bubbles = Node(backdrop, "Bubbles", c.W / 2, seaTop + 650, c.W, 1100, A.T);
            var rb = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                float d = 10 + (float)rb.NextDouble() * 22;
                var bub = Circle(bubbles, "Bubble", new Color(1, 1, 1, 0.25f + (float)rb.NextDouble() * 0.3f),
                    (float)rb.NextDouble() * c.W, (float)rb.NextDouble() * 1100, d);
                Circle(bub, "Shine", new Color(1, 1, 1, 0.8f), d * 0.32f, d * 0.32f, d * 0.3f);
            }

            // ── «Адиль, это для тебя!»: the child in the lesson of the day, then the activities ──
            var picks = Section(c, "Picks", PicksH, out s.picksTitle, out var picksRow, PicksBelow);
            s.picksTitle.text = "Адиль, это для тебя!";
            s.day = Hero(picksRow, 600, PicksH);
            s.trace = View(Tile(picksRow, "Trace", 0, 0, 340, PicksH, Pal.Hex("B9A3F5"), Art("card_trace"), true, A.C, 52, "Обведи", 30, allBadges: true));
            s.coloring = View(Tile(picksRow, "Coloring", 0, 0, 340, PicksH, Pal.Hex("8CCDF2"), Art("card_coloring"), true, A.C, 52, "Раскраски", 30, allBadges: true));
            s.tales = View(Tile(picksRow, "Tales", 0, 0, 340, PicksH, Pal.Night2, Art("cover_night"), true, A.C, 52, "Сказки", 30, allBadges: true));
            var treeTile = Tile(picksRow, "Tree", 0, 0, 340, PicksH, Pal.Hex("9FDDB2"), Art("card_tree"), true, A.C, 52, "Дерево", 30, allBadges: true);
            s.tree = View(treeTile);
            // «Ждёт отметки: 1» hanging on the tree card's top edge, for the parent.
            const float cw = 300, ch = 60;
            var chip = Node(treeTile.Holder, "DeedsChip", treeTile.Holder.W / 2, TileM, cw + 40, ch + 40);
            Shadow(chip, (cw + 40) / 2, (ch + 40) / 2, cw, ch, 0.16f, 8);
            var chipFace = Rect(chip, "Face", Pal.Orange, (cw + 40) / 2, (ch + 40) / 2, cw, ch, ch / 2);
            Circle(chipFace, "LeafRing", Color.white, 32, ch / 2, 48);
            Img(chipFace, "Leaf", Art("leaf_gold"), 32, ch / 2, 34, 34);
            s.deedsChipText = AutoFit(Txt(chipFace, "Ждёт отметки: 1", FontXB, 26, Color.white, 66 + (cw - 80) / 2, ch / 2 - 1, cw - 86, ch), 18);
            s.deedsChip = chip.Go;

            // ── «Моя семья»: the family as their avatars, on the sea ──
            var family = Section(c, "Family", FamilyH, out var familyTitle, out var familyRow);
            Label(familyTitle, "home_family");
            var edit = Rect(family.box, "Edit", new Color(1, 1, 1, 0.85f), Bleed + RowPad + 520, TitleH / 2 + 2, 230, 58, 29, A.TL);
            edit.Go.GetComponent<Image>().raycastTarget = true;
            s.familyEdit = edit.Go.AddComponent<Button>();
            Img(edit, "Icon", Art("topic_family"), 36, 29, 40, 40);
            Label(AutoFit(Txt(edit, "", FontB, 26, Pal.Sub, 130, 28, 150, 50), 18), "home_family_edit");
            s.childCard = Person(familyRow, "Child", Pal.Peach, AvatarPerson.Child, null, "Адиль");
            s.momCard = Person(familyRow, "Mom", Pal.RoseSoft, AvatarPerson.Mom, null, "Мама");
            s.dadCard = Person(familyRow, "Dad", Pal.Hex("D8ECFF"), AvatarPerson.Child, Art("char_dad"), "Папа");

            // ── a row per topic: the template, cloned at runtime ──
            var topic = Section(c, "TopicTemplate", TopicH, out var topicTitle, out var topicRow);
            topicTitle.text = "Хорошие манеры";
            var sec = topic.box.Go.AddComponent<LessonHomeSection>();
            sec.title = topicTitle;
            var icon = Circle(topic.box, "IconRing", Color.white, Bleed + RowPad + 34, TitleH / 2 + 2, 70, A.TL);
            OutlineRound(icon);
            sec.icon = Img(icon, "Icon", Art("topic_food"), 35, 35, 58, 58).Go.GetComponent<Image>();
            topicTitle.rectTransform.anchoredPosition += new Vector2(86, 0);
            topic.box.Rt.Find("TitleShadow").GetComponent<RectTransform>().anchoredPosition += new Vector2(86, 0);
            sec.scroll = topic.scroll;
            sec.row = topicRow.Rt;
            var lessonTile = Tile(topicRow, "LessonTemplate", 0, 0, 400, TopicH, Pal.Rose, Art("cover_bismillah"), true, A.C, 52, "Бисмиллях", 30, allBadges: true);
            lessonTile.Holder.Go.AddComponent<CanvasGroup>();
            sec.tileTemplate = View(lessonTile);
            s.topicTemplate = sec;
            s.topicIcons = new[] { Art("topic_food"), Art("topic_kindness"), Art("topic_dua"), Art("topic_mosque"),
                Art("topic_family"), Art("topic_sleep"), Art("topic_animals"), Art("reward_star") };

            // ── the top bar, over the page ──
            Circle(safe, "AvatarRing", Color.white, 108, 96, 132, A.TL);
            s.avatar = AvatarScreenBuilder.Avatar(safe, "Avatar", AvatarPerson.Child, 108, 96, 118, 118, A.TL, true, Pal.Lav);
            var avatarImage = s.avatar.GetComponent<Image>();
            avatarImage.raycastTarget = true;
            s.avatarButton = s.avatar.gameObject.AddComponent<Button>();
            s.stars = PillText(Pill(safe, Art("reward_star"), "12", 292, 96, 168, A.TL));
            s.leaves = PillText(Pill(safe, Art("leaf_green"), "3", 460, 96, 146, A.TL));
            var streak = Node(safe, "Streak", 617, 96, 170, 100, A.TL);
            s.streakChip = streak.Go;
            s.streak = PillText(Pill(streak, Art("icon_flame"), "2", 85, 50, 146));

            // «Открыть все уроки» in the middle, like «Unlock All Practices» (hidden for subscribers).
            const float uw = 440, uh = 82;
            var unlock = Node(safe, "Unlock", 960, 96, uw + 80, uh + 60, A.T);
            Shadow(unlock, (uw + 80) / 2, (uh + 60) / 2 + 6, uw, uh, 0.16f, 8);
            Rect(unlock, "Base", Pal.Darker(Pal.LavDeep, 0.75f), (uw + 80) / 2, (uh + 60) / 2 + 7, uw, uh, uh / 2);
            var uface = Rect(unlock, "Face", Pal.LavDeep, (uw + 80) / 2, (uh + 60) / 2, uw, uh, uh / 2);
            uface.Go.GetComponent<Image>().raycastTarget = true;
            s.unlock = uface.Go.AddComponent<Button>();
            Img(uface, "Lock", Shapes.Lock, 52, uh / 2, 40, 40, A.C, Color.white);
            Label(AutoFit(Txt(uface, "", FontXB, 32, Color.white, uw / 2 + 24, uh / 2 - 1, uw - 110, uh), 22), "home_unlock");
            Sparkle(unlock, 40, 26, 34, A.C, Pal.Hex("FFE08A"));
            Sparkle(unlock, uw + 50, uh + 40, 28, A.C, Pal.Hex("FFE08A"));

            const float pw = 384, ph = 84;
            Shadow(safe, 1700, 96, pw, ph, 0.12f, 8, A.TR);
            var parents = Rect(safe, "Parents", Color.white, 1700, 96, pw, ph, ph / 2, A.TR);
            parents.Go.GetComponent<Image>().raycastTarget = true;
            s.parents = parents.Go.AddComponent<Button>();
            Label(AutoFit(Txt(parents, "", FontXB, 32, Pal.MintDark, 160, ph / 2 - 1, 280, ph), 22), "home_parents");
            Img(parents, "Icon", Art("topic_family"), pw - 52, ph / 2, 64, 64);

            BuildAvatarOffer(r, s);
        }

        /// <summary>The rounded font, white with a thin dark outline and a solid cartoon drop shadow (titles on sand and sea).</summary>
        private static Material TitleMaterial()
        {
            const string path = "Assets/UI/Lessons/Shapes/TitleOutline.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(FontXB.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.CopyPropertiesFromMaterial(FontXB.material);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.1f);
            mat.SetColor("_OutlineColor", Pal.Ink);
            mat.SetFloat("_FaceDilate", 0.08f);
            mat.DisableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(Pal.Ink.r, Pal.Ink.g, Pal.Ink.b, 0.9f));
            mat.SetFloat("_UnderlayOffsetX", 0.4f);
            mat.SetFloat("_UnderlayOffsetY", -0.9f);
            mat.SetFloat("_UnderlayDilate", 0.25f);
            mat.SetFloat("_UnderlaySoftness", 0.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// A row: a title, then cards side by side that scroll sideways (a drag up or down
        /// goes to the page). Cards go into <paramref name="row"/>; their holders carry a
        /// <see cref="TileM"/> margin for the corner badges, which the spacing takes back.
        /// </summary>
        private static (Box box, ScrollRect scroll) Section(Box content, string name, float cardH, out TMP_Text title, out Box row,
            float below = 75)
        {
            float rowH = cardH + below, h = TitleH + rowH;
            var sec = Node(content, name, content.W / 2, h / 2, content.W, h, A.T);
            var shadow = Txt(sec, "", FontXB, 50, Pal.Ink, Bleed + RowPad + 600 + 4, TitleH / 2 + 2 + 5, 1200, 74, TA.Left, A.TL);
            shadow.enableWordWrapping = false;
            shadow.gameObject.name = "TitleShadow";
            title = AutoFit(Txt(sec, "", FontXB, 50, Color.white, Bleed + RowPad + 600, TitleH / 2 + 2, 1200, 74, TA.Left, A.TL), 30);
            title.fontSharedMaterial = TitleMaterial();
            shadow.fontSharedMaterial = title.fontSharedMaterial;
            shadow.gameObject.AddComponent<TextDropShadow>().source = title;
            Sparkle(sec, Bleed + RowPad - 10, 16, 30, A.TL, Color.white);

            var rowView = Node(sec, "Row", content.W / 2, TitleH + rowH / 2, content.W, rowH, A.T);
            AddImage(rowView, null, new Color(1, 1, 1, 0)).raycastTarget = true;
            var scroll = rowView.Go.AddComponent<NestedScrollRect>();
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            var cards = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
            cards.SetParent(rowView.Rt, false);
            cards.anchorMin = new Vector2(0, 0);
            cards.anchorMax = new Vector2(0, 1);
            cards.pivot = new Vector2(0, 0.5f);
            cards.anchoredPosition = Vector2.zero;
            cards.sizeDelta = new Vector2(content.W, 0);
            var hl = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = Gap - 2 * TileM;
            // Cards hang from the top of the row (25 px under the title); extra room stays below.
            hl.padding = new RectOffset((int)(Bleed + RowPad - TileM), (int)(Bleed + RowPad - TileM), (int)(25 - TileM), 0);
            hl.childAlignment = TextAnchor.UpperLeft;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            cards.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = rowView.Rt;
            scroll.content = cards;
            row = new Box { Rt = cards, W = content.W, H = rowH };
            return (sec, scroll);
        }

        /// <summary>
        /// The lesson of the day the Speech Blubs way: the child — their own head on the drawn
        /// body — stands on the card beside the lesson's cover, «Урок дня» on the corner, ▶.
        /// </summary>
        private static LessonTileView Hero(Box row, float w, float h)
        {
            var t = Tile(row, "LessonOfDay", 0, 0, w, h, Pal.Hex("7CC4F4"), Art("cover_bismillah"), false, A.C, 52,
                "Бисмиллях", 32, play: true, allBadges: true);
            Object.DestroyImmediate(t.Picture.gameObject);
            var face = t.Face;
            foreach (var n in new[] { "Paper", "Glow" })
            {
                var old = face.Rt.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            // The rim: the face itself painted pink to orange, the scene inset in it.
            var rimGrad = Fill(face, "Rim");
            AddImage(rimGrad, Shapes.Gradient("hero_rim", Pal.Hex("FF7AA8"), Pal.Hex("FFB547")), Color.white);
            rimGrad.Rt.SetAsFirstSibling();
            const float rimW = 12f;
            var scene = Cover(face, "Scene", Art("hero_bg"), w / 2, h / 2, w - 2 * rimW, h - 2 * rimW, 52 - rimW);
            scene.Rt.SetSiblingIndex(1);
            int under = t.LabelPill.GetSiblingIndex();
            var rim = Rect(face, "CoverRim", Color.white, w * 0.66f, h * 0.45f, w * 0.56f + 18, h * 0.72f + 18, 34);
            rim.Rt.localRotation = Quaternion.Euler(0, 0, -3);
            var frame = Cover(face, "Cover", Art("cover_bismillah"), w * 0.66f, h * 0.45f, w * 0.56f, h * 0.72f, 28);
            frame.Rt.localRotation = Quaternion.Euler(0, 0, -3);
            t.Picture = frame.Rt.Find("Picture").GetComponent<Image>();
            t.Fit = t.Picture.GetComponent<AspectRatioFitter>();
            // The child stands at the left, feet cut by the card's edge like a sticker peeking in.
            var child = AvatarScreenBuilder.Avatar(face, "Child", AvatarPerson.Child, w * 0.19f, h * 0.6f, w * 0.38f, h * 1.1f);
            var hi = Img(face, "Hi", Art("hi_lines"), w * 0.3f, h * 0.2f, 70, 70);
            foreach (var b in new[] { rim.Rt, frame.Rt, child.transform, hi.Rt }) b.SetSiblingIndex(under++);
            // The title sits under the cover, clear of the child and of ▶.
            t.LabelPill.anchoredPosition += new Vector2(w * 0.31f, 0);
            Sparkle(face, w * 0.4f, h * 0.12f, 30, A.C, Color.white);
            Sparkle(face, w * 0.96f, h * 0.1f, 24, A.C, Pal.Hex("FFE08A"));
            Sparkle(face, w * 0.36f, h * 0.82f, 20, A.C, Color.white);

            var badge = Img(t.Holder, "DayBadge", Art("badge_burst"), TileM + 120, TileM + 10, 250, 250 * 0.7f, A.C, null, false);
            badge.Rt.localRotation = Quaternion.Euler(0, 0, 7);
            Label(AutoFit(Txt(badge, "", FontXB, 32, Color.white, 125, 80, 190, 60), 20), "home_lesson_day");
            var view = View(t);
            view.maxLabelWidth = w * 0.42f; // ends before ▶
            view.minLabelScale = 0.55f;
            return view;
        }

        /// <summary>A family member as their avatar on a card (or a drawn stand-in for someone not in the app yet).</summary>
        private static LessonTileView Person(Box row, string name, Color fill, AvatarPerson person, Sprite drawn, string label)
        {
            var t = Tile(row, name, 0, 0, 250, FamilyH, fill, drawn ?? Art("char_girl"), false, A.C, 46, label, 28, allBadges: true);
            // A scatter of sparkles on the card, like the patterned backs in Speech Blubs.
            var pattern = Img(t.Face, "Pattern", Art("pattern_sparkles"), 125, FamilyH / 2, 250, FamilyH, A.C, new Color(1, 1, 1, 0.55f), false);
            pattern.Go.GetComponent<Image>().type = Image.Type.Tiled;
            var glow = t.Face.Rt.Find("Glow");
            pattern.Rt.SetSiblingIndex(glow != null ? glow.GetSiblingIndex() + 1 : 1);
            int under = t.LabelPill.GetSiblingIndex();
            if (drawn == null)
            {
                Object.DestroyImmediate(t.Picture.gameObject);
                var av = AvatarScreenBuilder.Avatar(t.Face, "Avatar", person, 125, FamilyH * 0.47f, 210, FamilyH * 0.86f);
                av.transform.SetSiblingIndex(under - 1);
            }
            else
            {
                var pic = t.Picture.rectTransform;
                pic.sizeDelta = new Vector2(210, FamilyH * 0.86f);
                pic.anchoredPosition = new Vector2(0, FamilyH * 0.03f);
                t.Picture.preserveAspect = true;
            }
            return View(t);
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

            // After the first install settings already sit in the row: look for it in the bar
            // above, or every reinstall nests one more row with one more home button.
            var bar = settings.transform.parent;
            while (bar.name == "LeftButtons" && bar.parent != null) bar = bar.parent;
            var row = bar.Find("LeftButtons") as RectTransform;
            if (row != null)
            {
                // Undo the nesting the earlier version left: settings and mail back into the
                // outer row, the inner rows (each with its own home button) removed.
                settings.transform.SetParent(row, false);
                if (mail != null) mail.transform.SetParent(row, false);
                for (int i = row.childCount - 1; i >= 0; i--)
                    if (row.GetChild(i).name == "LeftButtons") Object.DestroyImmediate(row.GetChild(i).gameObject);
            }
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
