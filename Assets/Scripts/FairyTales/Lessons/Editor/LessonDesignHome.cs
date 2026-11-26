using UnityEngine;
using UnityEngine.UI;
using static FairyTales.Lessons.EditorTools.UI;
using TA = TMPro.TextAlignmentOptions;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>
    /// Home screen variants in the Speech Blubs manner: painted backgrounds, chunky sticker
    /// tiles with a hand-drawn outline and a thick base, a white label inside the tile, a
    /// round badge on the corner (lock / play). Pictures first, almost no text.
    /// </summary>
    public static partial class LessonDesignScreens
    {
        private static readonly Color Outline = Pal.Hex("2B2547");
        private static readonly Color MintDark = Pal.Hex("3DBE95");

        private static readonly Color TYellow = Pal.Hex("FFE08A");
        private static readonly Color TPeach = Pal.Hex("FFC9A8");
        private static readonly Color TRose = Pal.Hex("FFC2D3");
        private static readonly Color TSky = Pal.Hex("A9DCF6");
        private static readonly Color TLav = Pal.Hex("D3C6FA");
        private static readonly Color TMint = Pal.Hex("BDEBD2");

        // ───────────────────────────── pieces ─────────────────────────────

        /// <summary>Crayon texture over a flat fill, so it reads painted, not vector.</summary>
        private static void Paper(Box p, Color tint, float alpha)
        {
            var b = Fill(p, "Paper");
            var img = AddImage(b, Shapes.Paper, new Color(tint.r, tint.g, tint.b, alpha));
            img.type = Image.Type.Tiled;
        }

        private static void Painted(Box r, string name, Color top, Color bottom, float paper = 0.16f)
        {
            Bg(r, name, top, bottom);
            Paper(r, Pal.Darker(bottom, 0.7f), paper);
        }

        private static Box SketchFrame(Box p, float cx, float cy, float w, float h, float r, A a = A.C)
        {
            float pad = 7f * r / Shapes.SketchRadius;
            var b = Node(p, "Outline", cx, cy, w + 2 * pad, h + 2 * pad, a);
            var img = AddImage(b, Shapes.Sketch, Outline);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Shapes.SketchRadius / r;
            return b;
        }

        private static void Sparkle(Box p, float cx, float cy, float size, A a = A.C, Color? c = null) =>
            Img(p, "Sparkle", Shapes.Sparkle, cx, cy, size, size, a, c ?? Color.white);

        private static Box CornerBadge(Box p, string name, float cx, float cy, float d, Color bg, Sprite icon, Color fg, A a, float k = 0.5f)
        {
            Shadow(p, cx, cy, d * 0.8f, d * 0.8f, 0.16f, 8, a);
            var b = Circle(p, name, bg, cx, cy, d, a);
            Img(b, "Icon", icon, d / 2 + (icon == Shapes.Triangle ? d * 0.04f : 0), d / 2, d * k, d * k, A.C, fg);
            return b;
        }

        /// <summary>
        /// A sticker tile: darker base under it (thickness), white rim around a picture
        /// (cover) or a coloured face with a picture on it (icon), a hand-drawn outline,
        /// a label inside the bottom-left corner, a lock or play badge on the corner.
        /// </summary>
        private static Box Tile(Box p, string name, float cx, float cy, float w, float h, Color fill, Sprite pic,
            bool cover, A a = A.C, float r = 52, string label = null, float labelSize = 32,
            bool locked = false, bool play = false, float picScale = 0.74f, Color? rim = null)
        {
            float depth = Mathf.Clamp(h * 0.035f, 10, 16);
            Shadow(p, cx, cy + depth, w, h, 0.18f, 16, a);
            Rect(p, name + " Base", Pal.Darker(fill, 0.68f), cx, cy + depth, w, h, r, a);
            SketchFrame(p, cx, cy + depth, w, h, r, a);

            var face = Rect(p, "Tile " + name, cover ? (rim ?? Color.white) : fill, cx, cy, w, h, r, a);
            face.Go.AddComponent<UnityEngine.UI.Button>();
            face.Go.GetComponent<Image>().raycastTarget = true;
            face.Go.AddComponent<Mask>().showMaskGraphic = true;
            if (cover)
            {
                const float rimW = 12f;
                Cover(face, "Picture", pic, w / 2, h / 2, w - 2 * rimW, h - 2 * rimW, r - rimW);
            }
            else
            {
                Paper(face, Pal.Darker(fill, 0.75f), 0.22f);
                Circle(face, "Glow", new Color(1, 1, 1, 0.35f), w / 2, h * 0.44f, Mathf.Min(w, h) * 0.8f);
                float s = Mathf.Min(w, h) * picScale;
                Img(face, "Picture", pic, w / 2, h / 2 - (label != null ? h * 0.07f : 0), s, s);
            }
            SketchFrame(p, cx, cy, w, h, r, a);

            if (label != null)
            {
                float lh = labelSize * 1.75f, lw = label.Length * labelSize * 0.56f + 50;
                float inset = cover ? 12f : 14f;
                var pill = Rect(face, "Label", new Color(1, 1, 1, 0.96f), inset + lw / 2, h - inset - lh / 2, lw, lh, lh / 2);
                Txt(pill, label, FontXB, labelSize, Pal.Ink, lw / 2, lh / 2 - 1, lw, lh);
            }
            if (locked)
                CornerBadge(p, "Lock", cx + w / 2 - 26, cy + h / 2 - 22, 96, Color.white, Shapes.Lock, MintDark, a, 0.52f);
            if (play)
                CornerBadge(p, "Play", cx + w / 2 - 34, cy + h / 2 - 30, 130, Pal.Rose, Shapes.Triangle, Color.white, a, 0.46f);
            return face;
        }

        /// <summary>White text with a soft dark copy under it, readable on painted bands.</summary>
        private static void BandTitle(Box p, string text, float x, float cy, float size = 50, A a = A.TL)
        {
            var sh = Txt(p, text, FontXB, size, new Color(0.15f, 0.1f, 0.3f, 0.22f), x + 900 + 3, cy + 4, 1800, 80, TA.Left, a);
            sh.name = "TitleShadow";
            Txt(p, text, FontXB, size, Color.white, x + 900, cy, 1800, 80, TA.Left, a);
        }

        private static void HomeHeader(Box r)
        {
            Head(r, "char_girl", 108, 100, 118, Pal.Lav, A.TL, 7);
            Pill(r, Art("reward_star"), "12", 292, 100, 168, A.TL);
            Pill(r, Art("leaf_green"), "3", 460, 100, 146, A.TL);

            const float w = 384, h = 84;
            Shadow(r, 1700, 100, w, h, 0.12f, 8, A.TR);
            var parents = Rect(r, "Button Parents", Color.white, 1700, 100, w, h, h / 2, A.TR);
            parents.Go.AddComponent<UnityEngine.UI.Button>();
            Txt(parents, "Для родителей", FontXB, 32, MintDark, 160, h / 2 - 1, 280, h);
            Img(parents, "Icon", Art("topic_family"), w - 52, h / 2, 64, 64);
        }

        private static void Clouds(Box r, int seed, float y0, float y1)
        {
            var rnd = new System.Random(seed);
            var holder = Fill(r, "Clouds");
            for (int c = 0; c < 4; c++)
            {
                float x = 200 + c * 480 + rnd.Next(-80, 80), y = y0 + (float)rnd.NextDouble() * (y1 - y0);
                for (int i = 0; i < 4; i++)
                    Circle(holder, "Puff", Pal.Hex("F4FAFF"), x + (i - 1.5f) * 70, y + (i % 2 == 0 ? 10 : -18), 120 + (i % 2) * 40);
            }
        }

        private static void Band(Box r, string name, Color c, float top, float paper = 0.2f)
        {
            Img(r, name + " Wave", Shapes.Wave, 960, top - 52, 2400, 128, A.T, c, false);
            var band = Rect(r, name, c, 960, top + (1600 - top) / 2, 2400, 1600 - top, 0, A.T);
            Paper(band, Pal.Darker(c, 0.65f), paper);
        }

        private static void NightSky(Box r, int seed, float y0, float y1)
        {
            var rnd = new System.Random(seed);
            var holder = Fill(r, "Stars");
            for (int i = 0; i < 18; i++)
                Sparkle(holder, 60 + (float)rnd.NextDouble() * 1800, y0 + (float)rnd.NextDouble() * (y1 - y0),
                    10 + rnd.Next(18), A.C, new Color(1, 0.95f, 0.75f, 0.35f + (float)rnd.NextDouble() * 0.5f));
        }

        // ───────────────────────────── screens ─────────────────────────────

        /// <summary>A: two big doors — day lessons and evening tales.</summary>
        private static void S_HomeDoors(Box r)
        {
            Painted(r, "sky_warm", Pal.Hex("BFE6FA"), Pal.Hex("FFF0D4"), 0.12f);
            Clouds(r, 3, 150, 330);
            Band(r, "Meadow", Pal.Hex("B9E08E"), 930, 0.22f);
            HomeHeader(r);

            Tile(r, "Lessons", 540, 590, 760, 600, Pal.Hex("FFD9A0"), Art("cover_salam"), true, A.C, 60, "Уроки", 46);
            var badge = Rect(r, "NewLesson", Pal.Rose, 330, 296, 300, 70, 35);
            Txt(badge, "Новый урок!", FontXB, 32, Color.white, 150, 34, 300, 70);
            Sparkle(r, 150, 330, 46); Sparkle(r, 930, 300, 34); Sparkle(r, 960, 880, 40);

            Tile(r, "Tales", 1380, 590, 760, 600, Pal.Night2, Art("cover_night"), true, A.C, 60, "Сказки", 46);
        }

        /// <summary>B: Speech Blubs home — painted bands scrolling down, a shelf of tiles on each.</summary>
        private static void S_HomeShelves(Box r)
        {
            Painted(r, "orange_band", Pal.Hex("FFD27E"), Pal.Hex("FFAE5C"), 0.2f);
            HomeHeader(r);

            BandTitle(r, "Урок дня для Айши", 140, 232);
            Sparkle(r, 120, 205, 40);
            Tile(r, "Bismillah", 520, 490, 760, 400, Pal.Rose, Art("cover_bismillah"), true, A.C, 52,
                "Бисмиллях", 34, play: true, rim: Pal.Hex("FF6F9A"));
            Sparkle(r, 150, 700, 30); Sparkle(r, 905, 285, 36); Sparkle(r, 890, 700, 24);
            Tile(r, "Salam", 1210, 490, 520, 400, TYellow, Art("cover_salam"), true, A.C, 52, "Салям", 34, locked: true);
            Tile(r, "Shukr", 1780, 490, 520, 400, TPeach, Art("cover_shukr"), true, A.C, 52, "Спасибо", 34, locked: true);

            Band(r, "Night", Pal.Hex("34307E"), 790);
            NightSky(r, 7, 800, 1060);
            BandTitle(r, "Сказки на ночь", 140, 862);
            Tile(r, "Night1", 400, 1110, 520, 380, Pal.Night2, Art("cover_night"), true);
            Tile(r, "Night2", 960, 1110, 380, 380, Pal.Hex("5B55B5"), Art("topic_mosque"), false);
            Tile(r, "Night3", 1400, 1110, 380, 380, Pal.Hex("7A5BC9"), Art("topic_animals"), false);
        }

        /// <summary>B, scrolled: lesson topics on a meadow band, tales on the night band.</summary>
        private static void S_HomeShelvesDown(Box r)
        {
            Painted(r, "meadow_band", Pal.Hex("CDEBA2"), Pal.Hex("A9DA82"), 0.22f);
            BandTitle(r, "Темы уроков", 140, 120);
            var topics = new (string art, string label, Color c, bool locked)[]
            {
                ("topic_dua", "Дуа", TYellow, false), ("topic_food", "Перед едой", TPeach, false),
                ("topic_family", "Семья", TRose, true), ("topic_animals", "Животные", TSky, true),
                ("topic_sleep", "Перед сном", TLav, true), ("topic_kindness", "Доброта", TMint, true),
            };
            for (int i = 0; i < topics.Length; i++)
            {
                var t = topics[i];
                Tile(r, t.label, 300 + i * 370, 340, 320, 320, t.c, Art(t.art), false, A.C, 48, t.label, 28, t.locked);
            }

            Band(r, "Night", Pal.Hex("34307E"), 600);
            NightSky(r, 11, 610, 1060);
            BandTitle(r, "Сказки на ночь", 140, 680);
            Sparkle(r, 520, 650, 30, A.TL, Pal.Hex("FFE9A8"));
            Tile(r, "Tale1", 420, 880, 560, 330, Pal.Night2, Art("cover_night"), true, A.C, 52, "Светлячок Нур", 30);
            Tile(r, "Tale2", 1000, 880, 400, 330, Pal.Hex("5B55B5"), Art("topic_mosque"), false, A.C, 52, "Луна Рамадана", 28, true);
            Tile(r, "Tale3", 1460, 880, 400, 330, Pal.Hex("7A5BC9"), Art("topic_animals"), false, A.C, 52, "Слеза верблюда", 28, true);
            Tile(r, "Tale4", 1920, 880, 400, 330, Pal.Hex("4A64C2"), Art("topic_kindness"), false, A.C, 52, "Тёплый хлеб", 28, true);
        }

        /// <summary>C: lesson of the day big on the left, the sections as tiles on the right.</summary>
        private static void S_HomeLessonFirst(Box r)
        {
            Painted(r, "lav_peach_c", Pal.Hex("E4D9FF"), Pal.Hex("FFE1CC"), 0.14f);
            HomeHeader(r);

            Tile(r, "LessonOfDay", 540, 610, 780, 700, Pal.Rose, Art("cover_bismillah"), true, A.C, 60,
                "Бисмиллях", 42, play: true);
            var badge = Rect(r, "Badge", Pal.Rose, 290, 262, 260, 70, 35);
            Txt(badge, "Урок дня", FontXB, 34, Color.white, 130, 34, 260, 70);
            Sparkle(r, 150, 300, 44, A.C, Pal.Hex("FFC94A")); Sparkle(r, 945, 290, 32, A.C, Pal.Hex("FFC94A"));

            Tile(r, "AllLessons", 1190, 420, 400, 340, TYellow, Art("topic_mosque"), false, A.C, 52, "Все уроки", 30);
            Tile(r, "Tales", 1630, 420, 400, 340, Pal.Night2, Art("cover_night"), true, A.C, 52, "Сказки", 30);
            Tile(r, "Tree", 1190, 820, 400, 340, TMint, Art("tree_full"), false, A.C, 52, "Дерево", 30);
            Tile(r, "Games", 1630, 820, 400, 340, TSky, Art("reward_star"), false, A.C, 52, "Игры", 30, true);
        }

        /// <summary>Inside «Уроки»: the topics as chunky tiles (Speech Blubs topic screen).</summary>
        private static void S_LessonTopics(Box r)
        {
            Painted(r, "meadow", Pal.Hex("DDF2B0"), Pal.Hex("BCE28C"), 0.22f);
            IconButton(r, "Home", Shapes.Home, Color.white, MintDark, 96, 92, 112, A.TL, 0.5f);
            Pill(r, Art("reward_star"), "12", 1780, 92, 168, A.TR);

            var topics = new (string art, string label, Color c, int state)[]
            {
                ("topic_dua", "Дуа", TYellow, 2), ("topic_food", "Перед едой", TPeach, 1),
                ("topic_family", "Семья", TRose, 0), ("topic_animals", "Животные", TSky, 0),
                ("topic_sleep", "Перед сном", TLav, 0), ("topic_kindness", "Доброта", TMint, 0),
            };
            for (int i = 0; i < topics.Length; i++)
            {
                var t = topics[i];
                float x = 560 + (i % 3) * 400, y = 340 + (i / 3) * 400;
                Tile(r, t.label, x, y, 340, 340, t.c, Art(t.art), false, A.C, 52, t.label, 30, t.state == 0);
                if (t.state == 2) Check(r, x + 144, y + 148, 92, Pal.Green);
                if (t.state == 1)
                {
                    var now = Rect(r, "Now", Pal.Rose, x - 60, y - 168, 190, 56, 28);
                    Txt(now, "Сейчас", FontXB, 28, Color.white, 95, 27, 190, 56);
                }
            }
        }
    }
}
