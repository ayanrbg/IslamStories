using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    /// Builds the lesson player prefab from the design screens' layout (1920×1080,
    /// LessonDesignScreens 02–11) and wires it to <see cref="LessonPlayerScreen"/>.
    ///
    ///   Nooria ▸ Lessons ▸ Build lesson player         — Assets/Prefabs/Lessons/LessonPlayer.prefab
    ///   Nooria ▸ Lessons ▸ Install lesson player in scene — instance under [UICanvas] in SampleScene
    ///   Nooria ▸ Lessons ▸ Render lesson player PNGs   — Design/Player/*.png with sample content
    ///
    /// Rebuilding overwrites the prefab; the scene instance follows it.
    /// </summary>
    public static class LessonPlayerBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Lessons/LessonPlayer.prefab";
        private const string ArtDir = "Assets/UI/Lessons/Art";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PngDir = "Design/Player";

        private static Sprite Art(string name)
            => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png")
               ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.jpg");

        // ─────────────────────────── build ───────────────────────────

        [MenuItem("Nooria/Lessons/Build lesson player")]
        public static string Build()
        {
            Shapes.Ensure();
            LoadFonts();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath)!);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var holder = new GameObject("Holder", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(holder, scene);
                var root = Root("LessonPlayerScreen", holder.transform);
                root.Go.AddComponent<Canvas>();
                root.Go.AddComponent<CanvasGroup>();
                root.Go.AddComponent<GraphicRaycaster>();
                var screen = root.Go.AddComponent<LessonPlayerScreen>();
                // Fades in over the library instead of sliding up like the tale screens.
                var so = new SerializedObject(screen);
                so.FindProperty("slideFromBottom").boolValue = false;
                so.FindProperty("animDuration").floatValue = 0.3f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var steps = Fill(root, "Steps");
                BuildStory(steps, screen.story);
                BuildRepeat(steps, screen.repeat);
                BuildQuiz(steps, screen.quiz);
                BuildDeed(steps, screen.deed);
                BuildReward(steps, screen.reward);
                BuildChrome(root, screen);
                BuildPrepare(root, screen);
                BuildResume(root, screen);
                BuildExit(root, screen);

                foreach (var v in new LessonStepView[] { screen.story, screen.repeat, screen.quiz, screen.deed, screen.reward })
                    v.root.SetActive(false);
                screen.prepare.SetActive(false);
                screen.exitPopup.SetActive(false);
                screen.resumePopup.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root.Go, PrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            var msg = $"[LessonPlayer] built {PrefabPath}";
            Debug.Log(msg);
            return msg;
        }

        // ─────────────────────────── pieces ───────────────────────────

        private static Box View(Box parent, string name)
        {
            var b = Fill(parent, name);
            b.Go.AddComponent<CanvasGroup>();
            return b;
        }

        /// <summary>Everything a notch must not cover goes in here.</summary>
        private static Box Safe(Box parent)
        {
            var b = Fill(parent, "Safe");
            b.Go.AddComponent<SafeAreaFitter>();
            return b;
        }

        internal static TMP_Text Label(TMP_Text t, string key)
        {
            t.gameObject.AddComponent<LessonLabel>().key = key;
            t.text = LessonStrings.T(key);
            return t;
        }

        private static TMP_Text AutoSize(TMP_Text t, float min)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = min;
            return t;
        }

        internal static Button Clickable(Box b)
        {
            var img = b.Go.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            return b.Go.TryGetComponent<Button>(out var button) ? button : b.Go.AddComponent<Button>();
        }

        /// <summary>
        /// The design's raised button inside a holder ("…Holder") so its shadow and base
        /// hide and pop together with it (<see cref="LessonPlayerScreen.Holder"/>).
        /// </summary>
        internal static Button Raised(Box p, string name, string key, Color c, float cx, float cy, float w, float h,
            A a = A.C, Color? text = null, float size = 44, Sprite icon = null)
        {
            var holder = Node(p, name + "Holder", cx, cy, w + 80, h + 80, a);
            var face = UI.Button(holder, LessonStrings.T(key), c, (w + 80) / 2, (h + 80) / 2, w, h, A.C, text, size, icon);
            face.Go.name = name;
            Label(face.Go.GetComponentInChildren<TMP_Text>(), key);
            return Clickable(face);
        }

        internal static Button Icon(Box p, string name, Sprite icon, Color bg, Color fg, float cx, float cy, float d,
            A a = A.C, float scale = 0.46f, bool holder = false)
        {
            var parent = holder ? Node(p, name + "Holder", cx, cy, d + 40, d + 40, a) : p;
            var b = holder
                ? IconButton(parent, name, icon, bg, fg, (d + 40) / 2, (d + 40) / 2, d, A.C, scale)
                : IconButton(parent, name, icon, bg, fg, cx, cy, d, a, scale);
            return Clickable(b);
        }

        /// <summary>A picture frame filled at runtime (cover crop). The placeholder only sizes the fitter.</summary>
        private static Box Picture(Box p, string name, float cx, float cy, float w, float h, float radius,
            A a, Color frame, out Image pic, out AspectRatioFitter fit)
        {
            var box = Cover(p, name, Art("story_dinner"), cx, cy, w, h, radius, a);
            box.Go.GetComponent<Image>().color = frame;
            pic = box.Rt.Find("Picture").GetComponent<Image>();
            pic.sprite = null;
            pic.enabled = false;
            fit = pic.GetComponent<AspectRatioFitter>();
            return box;
        }

        /// <summary>Stretches a node across its parent's width with fixed side margins; vertical placement stays.</summary>
        private static void StretchX(Box b, float left, float right)
        {
            var rt = b.Rt;
            var y = rt.anchoredPosition.y;
            var h = rt.sizeDelta.y;
            rt.anchorMin = new Vector2(0, rt.anchorMin.y);
            rt.anchorMax = new Vector2(1, rt.anchorMax.y);
            rt.offsetMin = new Vector2(left, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        }

        private static Box Dim(Box parent, string name)
        {
            var b = Fill(parent, name);
            var img = AddImage(b, null, new Color(0.12f, 0.08f, 0.28f, 0.6f));
            img.raycastTarget = true; // swallows taps on the step underneath
            return b;
        }

        // ─────────────────────────── chrome, download, questions ───────────────────────────

        private static void BuildChrome(Box root, LessonPlayerScreen s)
        {
            var chrome = Safe(root);
            chrome.Go.name = "Chrome";
            s.chrome = chrome.Go;
            s.close = Icon(chrome, "Close", Shapes.Cross, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.4f);

            var seg = Node(chrome, "StepProgress", 960, 78, 5 * 150 + 4 * 16, 22, A.T);
            var layout = seg.Go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            s.segments = seg.Rt;
            s.segmentTemplate = Rect(seg, "SegmentTemplate", Color.white, 75, 11, 150, 22, 11).Go.GetComponent<Image>();
        }

        private static void BuildPrepare(Box root, LessonPlayerScreen s)
        {
            var r = Fill(root, "Prepare");
            s.prepare = r.Go;
            var bg = Fill(r, "Background");
            AddImage(bg, Shapes.Gradient("lav_peach", Pal.Lav, Pal.Peach), Color.white).raycastTarget = true;
            var safe = Safe(r);
            AvatarScreenBuilder.Avatar(safe, "Child", AvatarPerson.Child, 330, 640, 420, 700, A.BL);
            AvatarScreenBuilder.Avatar(safe, "Mom", AvatarPerson.Mom, 1600, 600, 440, 820, A.BR);
            s.prepareClose = Icon(safe, "Close", Shapes.Cross, Color.white, Pal.Sub, 90, 78, 92, A.TL, 0.4f);

            var card = Card(safe, "Card", 960, 520, 820, 760, 60);
            Picture(card, "Cover", 410, 250, 700, 400, 44, A.C, Pal.LavSoft, out s.prepareCover, out s.prepareCoverFit);
            s.prepareTitle = AutoSize(Txt(card, "Бисмиллях", FontXB, 72, Pal.Ink, 410, 520, 700, 90), 44);
            s.prepareStatus = AutoSize(Txt(card, LessonStrings.T("prepare"), FontB, 34, Pal.Sub, 410, 595, 720, 60), 24);

            var bar = Rect(card, "Bar", Pal.LavSoft, 410, 665, 600, 30, 15);
            s.prepareBar = bar.Go;
            var fill = Rect(bar, "Fill", Pal.Rose, 15, 15, 30, 30, 15, A.L);
            fill.Rt.pivot = new Vector2(0, 0.5f);
            fill.Rt.anchoredPosition = Vector2.zero;
            s.prepareBarFill = fill.Rt;
            s.preparePercent = Txt(bar, "0%", FontXB, 30, Pal.Rose, 300, 62, 200, 40);
            s.prepareRetry = Raised(card, "Retry", "retry", Pal.Rose, 410, 685, 380, 100, A.C, null, 40);
            PlayerHolderOff(s.prepareRetry);
        }

        private static void PlayerHolderOff(Button b) => LessonPlayerScreen.Holder(b).SetActive(false);

        private static Box Popup(Box root, string name, out Box card)
        {
            var r = Fill(root, name);
            Dim(r, "Dim");
            card = Card(r, "Card", 960, 540, 820, 520, 60, null, 0.3f);
            Img(card, "Star", Art("reward_star"), 410, 120, 170, 170);
            return r;
        }

        private static void BuildExit(Box root, LessonPlayerScreen s)
        {
            var r = Popup(root, "ExitPopup", out var card);
            s.exitPopup = r.Go;
            Label(Txt(card, "", FontXB, 60, Pal.Ink, 410, 260, 740, 80), "exit_title");
            Label(AutoSize(Txt(card, "", FontM, 34, Pal.Sub, 410, 330, 740, 60), 24), "exit_note");
            s.exitStay = Raised(card, "Stay", "stay", Pal.Rose, 220, 440, 330, 100, A.C, null, 40);
            s.exitLeave = Raised(card, "Leave", "leave", Pal.LavSoft, 600, 440, 300, 100, A.C, Pal.Ink, 40);
        }

        private static void BuildResume(Box root, LessonPlayerScreen s)
        {
            var r = Popup(root, "ResumePopup", out var card);
            s.resumePopup = r.Go;
            Label(Txt(card, "", FontXB, 60, Pal.Ink, 410, 260, 740, 80), "resume_title");
            s.resumeNote = AutoSize(Txt(card, "", FontM, 34, Pal.Sub, 410, 330, 740, 60), 24);
            s.resumeGo = Raised(card, "Resume", "resume", Pal.Rose, 240, 440, 360, 100, A.C, null, 40);
            s.resumeRestart = Raised(card, "Restart", "restart", Pal.LavSoft, 610, 440, 300, 100, A.C, Pal.Ink, 40);
        }

        // ─────────────────────────── 1. История ───────────────────────────

        private static void BuildStory(Box steps, StoryStepView v)
        {
            var r = View(steps, "Story");
            v.root = r.Go;
            Picture(r, "Illustration", 960, 540, 1920, 1080, 0, A.Fill, Pal.Night, out v.picture, out v.pictureFit);
            var shade = Fill(r, "Shade");
            AddImage(shade, Shapes.Gradient("story_shade", new Color(0, 0, 0, 0), new Color(0.1f, 0.06f, 0.25f, 0.55f)), Color.white);
            var safe = Safe(r);

            var pill = Node(safe, "PagePill", 1800, 78, 190, 116, A.TR);
            var pillBox = Pill(pill, null, "1 / 2", 95, 58, 150, A.C, new Color(1, 1, 1, 0.92f), Pal.Sub, 70);
            v.pagePill = pill.Go;
            v.pageLabel = pillBox.Go.GetComponentInChildren<TMP_Text>();

            // The card spans the room between the two buttons, so on a notched phone (safe
            // area narrower) it shrinks instead of sliding under them.
            var card = Card(safe, "TextCard", 900, 905, 1300, 230, 50, null, 0.25f, A.B);
            StretchX(card, 230, 370);
            StretchX(new Box { Rt = (RectTransform)card.Rt.parent.GetChild(card.Rt.GetSiblingIndex() - 1) }, 230 - 35, 370 - 35);
            v.textTap = Clickable(card);
            var text = AutoSize(Txt(card, "", FontB, 46, Pal.Ink, 650, 115, 1180, 200), 30);
            StretchX(new Box { Rt = text.rectTransform }, 40, 40);
            v.text = text;
            v.listen = Icon(safe, "Listen", Shapes.Speaker, Color.white, Pal.LavDeep, 120, 905, 140, A.BL, 0.5f);
            v.next = Raised(safe, "Next", "next", Pal.Rose, 1720, 905, 300, 120, A.BR, null, 46);
        }

        // ─────────────────────── 2. Повтори как друг ───────────────────────

        private static void BuildRepeat(Box steps, RepeatStepView v)
        {
            var r = View(steps, "Repeat");
            v.root = r.Go;
            var bg = Fill(r, "Background");
            AddImage(bg, Shapes.Gradient("yellow", Pal.Hex("FFE9A0"), Pal.Hex("FFD45A")), Color.white);
            var safe = Safe(r);

            // Friend: the video, or the drawn friend while there is none.
            var art = Node(safe, "FriendArt", 520, 470, 640, 640, A.L);
            Card(art, "Card", 320, 320, 640, 640, 48, Pal.Mint, 0.14f);
            Img(art, "Friend", Art("char_friend"), 320, 340, 400, 600);
            v.friendArt = art.Go;

            Shadow(safe, 520, 470, 640, 640, 0.16f, 14, A.L);
            var frame = Rect(safe, "FriendVideo", Pal.Night, 520, 470, 640, 640, 48, A.L);
            frame.Go.AddComponent<Mask>().showMaskGraphic = true;
            var vid = Fill(frame, "Video");
            v.video = vid.Go.AddComponent<RawImage>();
            v.video.raycastTarget = false;
            v.videoFit = vid.Go.AddComponent<AspectRatioFitter>();
            v.videoFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            v.videoFit.aspectRatio = 1f;
            v.videoFrame = frame.Go;

            var label = Rect(safe, "FriendLabel", new Color(1, 1, 1, 0.94f), 520, 840, 300, 64, 32, A.L);
            Circle(label, "Dot", Pal.Green, 36, 32, 20);
            v.friendName = Txt(label, "Юсуф", FontXB, 32, Pal.Ink, 160, 31, 220, 60);
            v.replay = Icon(safe, "Replay", Shapes.Triangle, Pal.Rose, Color.white, 790, 740, 110, A.L, 0.42f, holder: true);

            // Speech bubble with its tail pointing back at the friend.
            var bubble = Node(safe, "Bubble", 1300, 190, 600, 170, A.R);
            Shadow(bubble, 300, 75, 560, 120, 0.12f, 10);
            var tail = Rect(bubble, "Tail", Color.white, 70, 141, 44, 44, 8);
            tail.Rt.localRotation = Quaternion.Euler(0, 0, 45);
            var bubbleBox = Rect(bubble, "SpeechBubble", Color.white, 300, 75, 560, 120, 40);
            Label(AutoSize(Txt(bubbleBox, "", FontXB, 40, Pal.Ink, 280, 60, 500, 100), 28), "repeat_bubble");

            var card = Card(safe, "PhraseCard", 1300, 500, 820, 380, 48, null, 0.16f, A.R);
            v.phraseAr = Txt(card, "بِسْمِ ٱللَّٰهِ", FontAr, 96, Pal.LavDeep, 410, 120, 760, 150);
            v.phraseTranslit = AutoSize(Txt(card, "Бисмиллях", FontXB, 64, Pal.Ink, 410, 245, 760, 80), 40);
            v.phraseMeaning = AutoSize(Txt(card, "«Во имя Аллаха»", FontM, 36, Pal.Sub, 410, 320, 760, 50), 26);
            v.said = Raised(safe, "Said", "repeat_said", Pal.Green, 1300, 880, 460, 120, A.R, null, 44);
        }

        // ─────────────────────────── 3. Задание ───────────────────────────

        private static void BuildQuiz(Box steps, QuizStepView v)
        {
            var r = View(steps, "Quiz");
            v.root = r.Go;
            var bg = Fill(r, "Background");
            AddImage(bg, Shapes.Gradient("sky", Pal.Hex("DCEFFD"), Pal.Sky), Color.white);
            var safe = Safe(r);

            v.question = AutoSize(Txt(safe, "", FontXB, 62, Pal.Ink, 960, 210, 1300, 110, TA.Center, A.T), 40);
            v.listen = Icon(safe, "Listen", Shapes.Speaker, Color.white, Pal.LavDeep, 1760, 210, 100, A.TR, 0.5f);

            var cards = new List<QuizCard>();
            for (int i = 0; i < 3; i++)
            {
                float x = 960 + (i - 1) * 520;
                var holder = Node(safe, "Option" + (i + 1), x, 610, 520, 600);
                var c = new QuizCard { rect = holder.Rt, group = holder.Go.AddComponent<CanvasGroup>() };
                c.hintGlow = Rect(holder, "HintGlow", Pal.Yellow, 260, 300, 490, 560, 58).Go.GetComponent<Image>();
                c.correctGlow = Rect(holder, "CorrectGlow", Pal.Green, 260, 300, 490, 560, 58).Go;
                var card = Card(holder, "Card", 260, 300, 450, 520, 46, null, 0.14f);
                c.button = Clickable(card);
                Picture(card, "Picture", 225, 205, 390, 340, 34, A.C, Pal.LavSoft, out c.picture, out c.pictureFit);
                c.caption = AutoSize(Txt(card, "", FontXB, 36, Pal.Ink, 225, 445, 400, 100), 26);
                var check = Circle(holder, "Check", Pal.Green, 450, 60, 96);
                Img(check, "Mark", Shapes.Check, 48, 50, 60, 60, A.C, Color.white);
                c.check = check.Rt;
                c.hintGlow.gameObject.SetActive(false);
                c.correctGlow.SetActive(false);
                check.Go.SetActive(false);
                cards.Add(c);
            }
            v.cards = cards.ToArray();

            var toast = Rect(safe, "Toast", Pal.Green, 960, 985, 680, 104, 52, A.B);
            v.toast = toast.Rt;
            v.toastBg = toast.Go.GetComponent<Image>();
            v.toastText = AutoSize(Txt(toast, "", FontXB, 44, Color.white, 340, 50, 640, 90), 30);
            v.toastCorrect = Pal.Green;
            v.toastTry = Pal.LavDeep;
            toast.Go.SetActive(false);
        }

        // ─────────────────────────── 4. Сделай в жизни ───────────────────────────

        private static void BuildDeed(Box steps, DeedStepView v)
        {
            var r = View(steps, "Deed");
            v.root = r.Go;
            var bg = Fill(r, "Background");
            AddImage(bg, Shapes.Gradient("peach_rose", Pal.Peach, Pal.RoseSoft), Color.white);
            var safe = Safe(r);
            Img(safe, "Glow", Shapes.Radial("deedglow", new Color(1, 1, 1, 0.85f), new Color(1, 1, 1, 0)), 520, 600, 900, 900, A.L);
            v.momAvatar = AvatarScreenBuilder.Avatar(safe, "Mom", AvatarPerson.Mom, 400, 640, 400, 760, A.L);
            v.childAvatar = AvatarScreenBuilder.Avatar(safe, "Child", AvatarPerson.Child, 700, 720, 330, 540, A.L);

            var card = Card(safe, "DeedCard", 1360, 560, 860, 760, 56, null, 0.18f, A.R);
            Label(Txt(card, "", FontXB, 32, Pal.Rose, 430, 80, 640, 50), "deed_header");
            v.listen = Icon(card, "Listen", Shapes.Speaker, Pal.LavSoft, Pal.LavDeep, 770, 80, 90, A.C, 0.5f);
            v.pictureFrame = Picture(card, "Picture", 430, 235, 440, 230, 32, A.C, Pal.LavSoft, out v.picture, out v.pictureFit).Go;
            v.title = AutoSize(Txt(card, "", FontXB, 50, Pal.Ink, 430, 425, 760, 150), 32);

            var row = Rect(card, "CheckRow", Pal.LavSoft, 430, 565, 760, 110, 36);
            v.checkRow = Clickable(row);
            v.checkRowBg = row.Go.GetComponent<Image>();
            Rect(row, "Box", Color.white, 70, 55, 64, 64, 16);
            var mark = Circle(row, "CheckMark", Pal.Green, 70, 55, 64);
            Img(mark, "Mark", Shapes.Check, 32, 34, 40, 40, A.C, Color.white);
            v.checkMark = mark.Go;
            mark.Go.SetActive(false);
            v.checkLabel = AutoSize(Txt(row, LessonStrings.T("deed_check"), FontXB, 38, Pal.Ink, 330, 55, 380, 60, TA.Left), 28);
            v.whoLabel = Label(AutoSize(Txt(row, "", FontM, 26, Pal.Sub, 640, 55, 220, 60, TA.Right), 20), "deed_who");
            v.done = Raised(card, "Done", "done", Pal.Rose, 430, 685, 420, 100, A.C, null, 44);
            v.rowIdle = Pal.LavSoft;
            v.rowMarked = Pal.Mint;
        }

        // ─────────────────────────── 5. Награда ───────────────────────────

        private static void BuildReward(Box steps, RewardStepView v)
        {
            var r = View(steps, "Reward");
            v.root = r.Go;
            var bg = Fill(r, "Background");
            AddImage(bg, Shapes.Radial("reward", Pal.Hex("FFF2C2"), Pal.Orange), Color.white).raycastTarget = true;
            Confetti(r, 3, 70, 80, 120, 1840, 980);
            v.confetti = (RectTransform)r.Rt.Find("Confetti");
            v.rays = Img(r, "Rays", Shapes.Radial("rays", new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0)), 960, 340, 800, 800).Rt;
            v.star = Img(r, "Star", Art("reward_star"), 960, 330, 430, 430).Rt;
            var safe = Safe(r);
            // The child cheering next to the star (spec: the avatar shows up in the reward).
            AvatarScreenBuilder.Avatar(safe, "Child", AvatarPerson.Child, 250, 720, 330, 560, A.BL);
            var brown = Pal.Hex("5B3410");
            v.title = AutoSize(Txt(safe, "", FontXB, 92, brown, 960, 640, 1400, 120), 56);

            var chips = Node(safe, "Chips", 960, 770, 900, 120);
            var chipsLayout = chips.Go.AddComponent<HorizontalLayoutGroup>();
            chipsLayout.childAlignment = TextAnchor.MiddleCenter;
            chipsLayout.spacing = 10;
            chipsLayout.childControlWidth = chipsLayout.childControlHeight = false;
            chipsLayout.childForceExpandWidth = chipsLayout.childForceExpandHeight = false;
            v.chipStar = Chip(chips, "ChipStar", Art("reward_star"), out v.chipStarText);
            v.chipLeaf = Chip(chips, "ChipLeaf", Art("leaf_green"), out v.chipLeafText);
            v.again = AutoSize(Txt(safe, "", FontB, 40, brown, 960, 770, 1300, 100), 28);

            var buttons = Node(safe, "Buttons", 960, 960, 1300, 200, A.B);
            var buttonsLayout = buttons.Go.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.spacing = 0;
            buttonsLayout.childControlWidth = buttonsLayout.childControlHeight = false;
            buttonsLayout.childForceExpandWidth = buttonsLayout.childForceExpandHeight = false;
            v.tree = Raised(buttons, "Tree", "reward_tree", Pal.Rose, 300, 100, 520, 116, A.C, null, 42);
            v.home = Raised(buttons, "Home", "reward_home", Color.white, 900, 100, 420, 116, A.C, Pal.Ink, 42);
        }

        private static GameObject Chip(Box parent, string name, Sprite icon, out TMP_Text text)
        {
            var holder = Node(parent, name, 0, 60, 420, 120);
            var pill = Pill(holder, icon, "+1", 210, 60, 360, A.C, null, null, 88);
            text = pill.Go.GetComponentInChildren<TMP_Text>();
            return holder.Go;
        }

        // ─────────────────────────── scene ───────────────────────────

        /// <summary>
        /// Puts the player under [UICanvas] next to the other screens (before the popups,
        /// so ChildGatePopup draws over it), inactive like them, and saves SampleScene.
        /// </summary>
        [MenuItem("Nooria/Lessons/Install lesson player in scene")]
        public static string InstallInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return "no prefab — build it first";
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

            var sm = UnityEngine.Object.FindAnyObjectByType<ScreenManager>(FindObjectsInactive.Include);
            if (sm == null) return "no ScreenManager in the scene";
            var canvas = sm.transform;
            foreach (var old in canvas.GetComponentsInChildren<LessonPlayerScreen>(true))
                UnityEngine.Object.DestroyImmediate(old.gameObject);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            inst.name = "LessonPlayerScreen";
            var anchor = canvas.Find("NarrationProgressScreen");
            inst.transform.SetSiblingIndex(anchor != null ? anchor.GetSiblingIndex() + 1 : canvas.childCount - 1);
            var rt = (RectTransform)inst.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            inst.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var msg = $"[LessonPlayer] installed at sibling {inst.transform.GetSiblingIndex()} under {canvas.name}";
            Debug.Log(msg);
            return msg;
        }

        // ─────────────────────────── preview ───────────────────────────

        /// <summary>Renders each step with sample content into Design/Player — a layout check without Play Mode.</summary>
        [MenuItem("Nooria/Lessons/Render lesson player PNGs")]
        public static string RenderPreviews()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return "no prefab";
            Directory.CreateDirectory(PngDir);
            var states = new (string id, Action<LessonPlayerScreen> setup)[]
            {
                ("P1_Prepare", PrevPrepare), ("P2_Story", PrevStory), ("P3_Repeat", PrevRepeat),
                ("P4_Quiz", PrevQuiz), ("P5_Deed", PrevDeed), ("P6_Reward", PrevReward),
                ("P7_Exit", s => { PrevStory(s); s.exitPopup.SetActive(true); }),
                ("P8_Resume", s => { PrevPrepare(s); s.resumePopup.SetActive(true); s.resumeNote.text = LessonStrings.T("resume_note", 3, 5); }),
            };
            var scene = EditorSceneManager.NewPreviewScene();
            var rtex = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
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
                cam.targetTexture = rtex;
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

                foreach (var (id, setup) in states)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    inst.transform.SetParent(canvasGo.transform, false);
                    var s = inst.GetComponent<LessonPlayerScreen>();
                    inst.GetComponent<CanvasGroup>().alpha = 1f;
                    foreach (var l in inst.GetComponentsInChildren<LessonLabel>(true)) l.Apply();
                    setup(s);
                    ShapeArabic(inst);
                    Canvas.ForceUpdateCanvases();
                    foreach (var t in inst.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    RenderTexture.active = rtex;
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
                rtex.Release();
                UnityEngine.Object.DestroyImmediate(rtex);
            }
            return $"[LessonPlayer] rendered {done} PNGs into {Path.GetFullPath(PngDir)}";
        }

        private static void ShapeArabic(GameObject root)
        {
            var attach = typeof(ShapedText).GetMethod("Attach", BindingFlags.NonPublic | BindingFlags.Static);
            if (attach == null) return;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) attach.Invoke(null, new object[] { t });
        }

        private static void Pic(Image img, AspectRatioFitter fit, string art)
        {
            var s = Art(art);
            img.sprite = s;
            img.enabled = s != null;
            if (s != null && fit != null) fit.aspectRatio = s.rect.width / s.rect.height;
        }

        private static void Step(LessonPlayerScreen s, LessonStepView v, int step, int total = 5)
        {
            v.root.SetActive(true);
            var g = v.root.GetComponent<CanvasGroup>();
            if (g != null) g.alpha = 1f;
            s.chrome.SetActive(v != s.reward);
            s.segmentTemplate.gameObject.SetActive(false);
            for (int i = 0; i < total; i++)
            {
                var seg = UnityEngine.Object.Instantiate(s.segmentTemplate, s.segments);
                seg.gameObject.SetActive(true);
                seg.color = i < step ? s.segmentDone : s.segmentTodo;
            }
        }

        private static void PrevPrepare(LessonPlayerScreen s)
        {
            s.prepare.SetActive(true);
            Pic(s.prepareCover, s.prepareCoverFit, "cover_bismillah");
            s.prepareTitle.text = "Бисмиллях";
            var w = ((RectTransform)s.prepareBarFill.parent).rect.width;
            s.prepareBarFill.sizeDelta = new Vector2(w * 0.64f, s.prepareBarFill.sizeDelta.y);
            s.preparePercent.text = "64%";
        }

        private static void PrevStory(LessonPlayerScreen s)
        {
            Step(s, s.story, 1);
            Pic(s.story.picture, s.story.pictureFit, "story_dinner");
            s.story.text.text = "<color=#E0577B>Айша</color> села обедать. Мама улыбается: «Что мы говорим перед едой?»";
            s.story.pageLabel.text = "1 / 2";
        }

        private static void PrevRepeat(LessonPlayerScreen s)
        {
            Step(s, s.repeat, 2);
            s.repeat.videoFrame.SetActive(false);
            LessonPlayerScreen.SetShown(s.repeat.replay, false);
        }

        private static void PrevQuiz(LessonPlayerScreen s)
        {
            Step(s, s.quiz, 3);
            s.quiz.question.text = "Что делает <color=#E0577B>Айша</color> перед едой?";
            var items = new[] { ("quiz_tv", "Смотрит мультик"), ("quiz_plate", "Говорит «Бисмиллях»"), ("quiz_ball", "Играет в мяч") };
            for (int i = 0; i < 3; i++)
            {
                var c = s.quiz.cards[i];
                Pic(c.picture, c.pictureFit, items[i].Item1);
                c.caption.text = items[i].Item2;
            }
            s.quiz.cards[0].group.alpha = 0.45f;
            s.quiz.cards[1].correctGlow.SetActive(true);
            s.quiz.cards[1].check.gameObject.SetActive(true);
            s.quiz.toast.gameObject.SetActive(true);
            s.quiz.toastText.text = LessonStrings.T("quiz_correct");
        }

        private static void PrevDeed(LessonPlayerScreen s)
        {
            Step(s, s.deed, 4);
            Pic(s.deed.picture, s.deed.pictureFit, "cover_bismillah");
            s.deed.title.text = "Скажите «Бисмиллях» вместе перед обедом";
        }

        private static void PrevReward(LessonPlayerScreen s)
        {
            Step(s, s.reward, 5);
            s.reward.title.text = "Машаллах, Айша!";
            s.reward.chipStarText.text = "+1 звезда";
            s.reward.chipLeafText.text = "+1 листок";
            s.reward.again.gameObject.SetActive(false);
        }
    }
}
