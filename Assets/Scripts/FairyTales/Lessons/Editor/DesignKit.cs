using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>Nooria 2.0 day palette (the overview page's colours) plus the night one of the tales.</summary>
    internal static class Pal
    {
        public static Color Hex(string hex, float a = 1f)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            c.a = a;
            return c;
        }

        public static readonly Color Ink = Hex("2D2352");
        public static readonly Color Sub = Hex("6C6391");
        public static readonly Color White = Color.white;
        public static readonly Color Lav = Hex("CDBEF6");
        public static readonly Color LavSoft = Hex("F3EEFF");
        public static readonly Color LavDeep = Hex("7B5BD6");
        public static readonly Color Rose = Hex("E0577B");
        public static readonly Color RoseDark = Hex("B83E60");
        public static readonly Color RoseSoft = Hex("FAD3DE");
        public static readonly Color Peach = Hex("FFE3CF");
        public static readonly Color Yellow = Hex("FFD45A");
        public static readonly Color Orange = Hex("FFB23F");
        public static readonly Color Teal = Hex("79D3B9");
        public static readonly Color Sky = Hex("CFE9FB");
        public static readonly Color Mint = Hex("DDF3E6");
        public static readonly Color Green = Hex("3FB673");
        public static readonly Color GreenDark = Hex("2E9A5D");
        public static readonly Color Night = Hex("2E2763");
        public static readonly Color Night2 = Hex("5843A8");
        public static readonly Color NightCard = Hex("3D3375");
        public static readonly Color Purple = Hex("B45CFA");
        public static readonly Color PurpleDark = Hex("8F3FD6");
        /// <summary>Crayon outline of the sticker tiles.</summary>
        public static readonly Color Outline = Hex("2B2547");
        public static readonly Color MintDark = Hex("3DBE95");

        public static Color Darker(Color c, float k = 0.78f) => new(c.r * k, c.g * k, c.b * k, c.a);
    }

    /// <summary>Procedural UI shapes, written once as PNG sprites under Assets/UI/Lessons/Shapes.</summary>
    internal static class Shapes
    {
        public const string Dir = "Assets/UI/Lessons/Shapes";
        public const float RoundedRadius = 48f;

        public static Sprite Rounded => Load("rounded");
        public static Sprite Circle => Load("circle");
        public static Sprite Ring => Load("ring");
        public static Sprite DashedRing => Load("ring_dashed");
        public static Sprite Triangle => Load("triangle");
        public static Sprite Shadow => Load("shadow");
        public static Sprite Check => Load("check");
        public static Sprite Cross => Load("cross");
        public static Sprite Leaf => Load("leaf");
        public static Sprite Lock => Load("lock");
        public static Sprite Speaker => Load("speaker");
        public static Sprite Arrow => Load("arrow");
        public static Sprite Gear => Load("gear");
        /// <summary>Hand-drawn outline of a rounded tile (crayon stroke, uneven width); 9-sliced, corner radius 64.</summary>
        public static Sprite Sketch => Load("sketch");
        public const float SketchRadius = 64f;
        /// <summary>Tileable crayon/paper texture to lay over flat fills.</summary>
        public static Sprite Paper => Load("paper");
        /// <summary>Wavy top edge of a band (filled below the wave).</summary>
        public static Sprite Wave => Load("wave");
        public static Sprite Sparkle => Load("sparkle");
        public static Sprite Home => Load("home");

        private static readonly Dictionary<string, Sprite> Cache = new();

        private static Sprite Load(string name)
        {
            if (Cache.TryGetValue(name, out var s) && s != null) return s;
            s = AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");
            Cache[name] = s;
            return s;
        }

        public static void Ensure()
        {
            Directory.CreateDirectory(Dir);
            Write("rounded", 128, 128, (x, y) => RoundRect(x, y, 64, 64, 64, 64, 48), 56);
            Write("circle", 256, 256, (x, y) => Len(x - 128, y - 128) - 126);
            Write("ring", 256, 256, (x, y) => Mathf.Abs(Len(x - 128, y - 128) - 116) - 10);
            Write("ring_dashed", 256, 256, (x, y) =>
            {
                float d = Mathf.Abs(Len(x - 128, y - 128) - 118) - 7;
                float ang = Mathf.Atan2(y - 128, x - 128) * Mathf.Rad2Deg + 180f;
                return (ang % 15f) < 8f ? d : 99f;
            });
            Write("triangle", 128, 128, (x, y) =>
            {
                // Rounded play triangle pointing right.
                var a = new Vector2(30, 14); var b = new Vector2(30, 114); var c = new Vector2(116, 64);
                return Tri(new Vector2(x, y), a, b, c) - 6;
            });
            Write("shadow", 192, 192, (x, y) =>
            {
                float d = RoundRect(x, y, 96, 96, 48, 48, 40);
                return d; // alpha falls off softly below, see ShadowAlpha
            }, 95, shadow: true);
            Write("check", 128, 128, (x, y) => Mathf.Min(
                Seg(new Vector2(x, y), new Vector2(28, 66), new Vector2(54, 92)),
                Seg(new Vector2(x, y), new Vector2(54, 92), new Vector2(102, 38))) - 11);
            Write("cross", 128, 128, (x, y) => Mathf.Min(
                Seg(new Vector2(x, y), new Vector2(34, 34), new Vector2(94, 94)),
                Seg(new Vector2(x, y), new Vector2(94, 34), new Vector2(34, 94))) - 10);
            Write("leaf", 128, 128, (x, y) =>
            {
                // Two circles' intersection: a lens, tilted 45°.
                var p = Rot(new Vector2(x - 64, y - 64), 45f);
                float d1 = Len(p.x - 34, p.y) - 70, d2 = Len(p.x + 34, p.y) - 70;
                return Mathf.Max(d1, d2);
            });
            Write("lock", 128, 128, (x, y) =>
            {
                float body = RoundRect(x, y, 64, 82, 40, 32, 10);
                float shackle = Mathf.Abs(Len(x - 64, y - 48) - 24) - 8;
                if (y > 52) shackle = 99f;
                return Mathf.Min(body, shackle);
            });
            Write("speaker", 128, 128, (x, y) =>
            {
                float box = RoundRect(x, y, 34, 64, 14, 18, 4);
                float cone = Tri(new Vector2(x, y), new Vector2(34, 64), new Vector2(66, 26), new Vector2(66, 102)) - 2;
                float w1 = Mathf.Abs(Len(x - 62, y - 64) - 26) - 5; if (x < 74) w1 = 99f;
                float w2 = Mathf.Abs(Len(x - 62, y - 64) - 46) - 5; if (x < 84) w2 = 99f;
                return Mathf.Min(Mathf.Min(box, cone), Mathf.Min(w1, w2));
            });
            Write("arrow", 128, 128, (x, y) => Mathf.Min(
                Seg(new Vector2(x, y), new Vector2(76, 30), new Vector2(42, 64)),
                Seg(new Vector2(x, y), new Vector2(42, 64), new Vector2(76, 98))) - 10);
            Write("gear", 128, 128, (x, y) =>
            {
                float r = Len(x - 64, y - 64);
                float ang = Mathf.Atan2(y - 64, x - 64);
                float teeth = 46 + 8 * Mathf.Clamp(Mathf.Cos(ang * 8) * 3, -1, 1);
                return Mathf.Max(r - teeth, 18 - r);
            });
            WriteA("sketch", 256, 256, (x, y) =>
            {
                float d = RoundRect(x, y, 128, 128, 121, 121, 64);
                float hw = 4.4f + 1.6f * VNoise(x / 11f, y / 11f, 24);
                return Mathf.Clamp01(hw - Mathf.Abs(d) + 0.5f) * (0.88f + 0.12f * Hash((int)x, (int)y));
            }, 100);
            WriteA("paper", 256, 256, (x, y) =>
                0.5f * VNoise(x / 32f, y / 32f, 8) + 0.3f * VNoise(x / 8f, y / 8f, 32) + 0.2f * Hash((int)x, (int)y));
            WriteA("wave", 1024, 128, (x, y) =>
            {
                float edge = 64 + 22 * Mathf.Sin(x / 1024f * Mathf.PI * 4) + 1.5f * VNoise(x / 20f, 0, 51);
                return Mathf.Clamp01(y - edge + 0.5f);
            });
            WriteA("sparkle", 128, 128, (x, y) =>
            {
                float s = Mathf.Sqrt(Mathf.Abs(x - 64)) + Mathf.Sqrt(Mathf.Abs(y - 64));
                return Mathf.Clamp01((7.6f - s) * 5f);
            });
            Write("home", 128, 128, (x, y) =>
            {
                float roof = Tri(new Vector2(x, y), new Vector2(64, 18), new Vector2(16, 62), new Vector2(112, 62)) - 6;
                float body = RoundRect(x, y, 64, 82, 34, 28, 8);
                float door = RoundRect(x, y, 64, 96, 11, 16, 5);
                return Mathf.Max(Mathf.Min(roof, body), -door);
            });
            AssetDatabase.Refresh();
            foreach (var (name, border) in new[] { ("rounded", 56), ("shadow", 95) })
                SetBorder($"{Dir}/{name}.png", border);
            Cache.Clear();
        }

        /// <summary>Vertical gradient sprite (top → bottom), cached as a PNG.</summary>
        public static Sprite Gradient(string name, Color top, Color bottom)
        {
            var path = $"{Dir}/grad_{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(4, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                {
                    var c = Color.Lerp(bottom, top, y / 255f);
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                Import(path, 0);
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Radial glow sprite: centre colour fading to the edge colour.</summary>
        public static Sprite Radial(string name, Color inner, Color outer)
        {
            var path = $"{Dir}/radial_{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float t = Mathf.Clamp01(Len(x - 128, y - 128) / 128f);
                    tex.SetPixel(x, y, Color.Lerp(inner, outer, t * t * (3 - 2 * t)));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                Import(path, 0);
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ── drawing ──

        private static void Write(string name, int w, int h, Func<float, float, float> sdf, int border = 0, bool shadow = false)
        {
            var path = $"{Dir}/{name}.png";
            if (File.Exists(path)) return;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = sdf(x + 0.5f, h - y - 0.5f); // y down, like design space
                float a = shadow
                    ? Mathf.Clamp01(1f - (d + 40f) / 80f) * 0.85f
                    : Mathf.Clamp01(0.5f - d);
                a = shadow ? a * a : a;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            Import(path, border);
        }

        /// <summary>Like Write, but the function gives the alpha directly (textures, not shapes).</summary>
        private static void WriteA(string name, int w, int h, Func<float, float, float> alpha, int border = 0)
        {
            var path = $"{Dir}/{name}.png";
            if (File.Exists(path)) return;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, h - y - 0.5f)) * 255));
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            Import(path, border, 1024);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        /// <summary>Smooth value noise, tileable with the given period in cells.</summary>
        private static float VNoise(float x, float y, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int W(int v) => ((v % period) + period) % period;
            float a = Hash(W(xi), W(yi)), b = Hash(W(xi + 1), W(yi));
            float c = Hash(W(xi), W(yi + 1)), d = Hash(W(xi + 1), W(yi + 1));
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static void Import(string path, int border, int maxSize = 512, bool compress = false)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = compress ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            if (border > 0) ti.spriteBorder = new Vector4(border, border, border, border);
            ti.SaveAndReimport();
        }

        private static void SetBorder(string path, int border)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null || ti.spriteBorder.x == border) return;
            ti.spriteBorder = new Vector4(border, border, border, border);
            ti.SaveAndReimport();
        }

        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float RoundRect(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - (hw - r), qy = Mathf.Abs(y - cy) - (hh - r);
            return Len(Mathf.Max(qx, 0), Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        private static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
            bool inside = Side(p, a, b) == Side(p, b, c) && Side(p, b, c) == Side(p, c, a);
            return inside ? -d : d;
        }

        private static bool Side(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) > 0;

        private static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }

    /// <summary>The parts of a sticker tile a screen fills at runtime.</summary>
    internal struct TileParts
    {
        public Box Holder, Face;
        public Image Base, Picture;
        public AspectRatioFitter Fit;
        public UnityEngine.UI.Button Button;
        public TextMeshProUGUI Label;
        public RectTransform LabelPill;
        public GameObject Lock, Play, Done;
    }

    /// <summary>Where a child sticks inside its parent when the parent resizes.</summary>
    internal enum A { TL, T, TR, L, C, R, BL, B, BR, Fill }

    /// <summary>A UI node plus its design size, so children can be placed in design pixels.</summary>
    internal struct Box
    {
        public RectTransform Rt;
        public float W, H;
        public Transform T => Rt;
        public GameObject Go => Rt.gameObject;
    }

    /// <summary>
    /// Builds uGUI in design pixels (1920×1080, y down). Every element is given by its
    /// CENTRE (cx, cy) inside the parent box and an anchor that decides what it follows
    /// when the screen is wider than 16:9 (phones are ~19.5:9).
    /// </summary>
    internal static class UI
    {
        public static TMP_FontAsset FontXB, FontB, FontM, FontAr;

        public static void LoadFonts()
        {
            FontXB = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/TTRoundsNeue-ExtraBold SDF.asset");
            FontB = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/TTRoundsNeue-Bold SDF.asset");
            FontM = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/TTRoundsNeue-Medium SDF.asset");
            FontAr = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoNaskhArabic-Bold Dynamic.asset");
        }

        public static Box Root(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return new Box { Rt = rt, W = 1920, H = 1080 };
        }

        public static Box Node(Box parent, string name, float cx, float cy, float w, float h, A a = A.C)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.T, false);
            var rt = (RectTransform)go.transform;
            Place(rt, parent, a, cx, cy, w, h);
            return new Box { Rt = rt, W = w, H = h };
        }

        /// <summary>Full-size child of the parent.</summary>
        public static Box Fill(Box parent, string name)
        {
            var b = Node(parent, name, parent.W / 2, parent.H / 2, parent.W, parent.H, A.Fill);
            return b;
        }

        public static void Place(RectTransform rt, Box parent, A a, float cx, float cy, float w, float h)
        {
            if (a == A.Fill)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                float l = cx - w / 2, r = parent.W - (cx + w / 2), t = cy - h / 2, b = parent.H - (cy + h / 2);
                rt.offsetMin = new Vector2(l, b);
                rt.offsetMax = new Vector2(-r, -t);
                return;
            }
            var anchor = a switch
            {
                A.TL => new Vector2(0, 1), A.T => new Vector2(0.5f, 1), A.TR => new Vector2(1, 1),
                A.L => new Vector2(0, 0.5f), A.C => new Vector2(0.5f, 0.5f), A.R => new Vector2(1, 0.5f),
                A.BL => new Vector2(0, 0), A.B => new Vector2(0.5f, 0), _ => new Vector2(1, 0),
            };
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            float ax = anchor.x * parent.W, ay = (1 - anchor.y) * parent.H;
            rt.anchoredPosition = new Vector2(cx - ax, -(cy - ay));
            rt.sizeDelta = new Vector2(w, h);
        }

        public static Image AddImage(Box b, Sprite s, Color c, bool preserve = false)
        {
            var img = b.Go.AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.preserveAspect = preserve;
            img.raycastTarget = false;
            return img;
        }

        public static Box Img(Box p, string name, Sprite s, float cx, float cy, float w, float h, A a = A.C, Color? c = null, bool preserve = true)
        {
            var b = Node(p, name, cx, cy, w, h, a);
            AddImage(b, s, c ?? Color.white, preserve);
            return b;
        }

        /// <summary>Rounded rectangle; radius in design pixels.</summary>
        public static Box Rect(Box p, string name, Color c, float cx, float cy, float w, float h, float radius = 40, A a = A.C)
        {
            var b = Node(p, name, cx, cy, w, h, a);
            var img = AddImage(b, radius > 0 ? Shapes.Rounded : null, c);
            if (radius > 0)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = Shapes.RoundedRadius / Mathf.Min(radius, Mathf.Min(w, h) / 2f);
            }
            return b;
        }

        public static Box Circle(Box p, string name, Color c, float cx, float cy, float d, A a = A.C)
        {
            var b = Node(p, name, cx, cy, d, d, a);
            AddImage(b, Shapes.Circle, c);
            return b;
        }

        /// <summary>Soft drop shadow behind a rounded rect.</summary>
        public static Box Shadow(Box p, float cx, float cy, float w, float h, float alpha = 0.18f, float dy = 14, A a = A.C)
        {
            var b = Node(p, "Shadow", cx, cy + dy, w + 70, h + 70, a);
            var img = AddImage(b, Shapes.Shadow, new Color(0.18f, 0.12f, 0.35f, alpha));
            img.type = Image.Type.Sliced;
            return b;
        }

        public static Box Card(Box p, string name, float cx, float cy, float w, float h, float radius = 44, Color? c = null, float shadow = 0.16f, A a = A.C)
        {
            if (shadow > 0) Shadow(p, cx, cy, w, h, shadow, 14, a);
            return Rect(p, name, c ?? Color.white, cx, cy, w, h, radius, a);
        }

        /// <summary>Picture filling a rounded frame edge to edge (cropped, like CSS cover).</summary>
        public static Box Cover(Box p, string name, Sprite s, float cx, float cy, float w, float h, float radius = 40, A a = A.C)
        {
            var frame = Rect(p, name, Color.white, cx, cy, w, h, radius, a);
            var mask = frame.Go.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var pic = Fill(frame, "Picture");
            var img = AddImage(pic, s, Color.white);
            if (s != null)
            {
                var fit = pic.Go.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = s.rect.width / s.rect.height;
            }
            return frame;
        }

        /// <summary>
        /// Circle cut from a full-body character: the head and shoulders. headY is where the
        /// face centre sits in the sprite (0 top … 1 bottom), zoom how much of the sprite width fills the circle.
        /// </summary>
        public static Box Bust(Box p, string name, Sprite s, float cx, float cy, float d, Color bg, float headY = 0.2f, float zoom = 1.6f, A a = A.C, float ring = 0)
        {
            if (ring > 0) Circle(p, name + "Ring", Color.white, cx, cy, d + ring * 2, a);
            var frame = Circle(p, name, bg, cx, cy, d, a);
            frame.Go.AddComponent<Mask>().showMaskGraphic = true;
            if (s == null) return frame;
            float iw = d * zoom, ih = iw * s.rect.height / s.rect.width;
            Img(frame, "Picture", s, d / 2, d / 2 + ih * (0.5f - headY), iw, ih, A.C, null, false);
            return frame;
        }

        public static TextMeshProUGUI Txt(Box p, string text, TMP_FontAsset font, float size, Color c,
            float cx, float cy, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.Center, A a = A.C)
        {
            var b = Node(p, "Text", cx, cy, w, h, a);
            var t = b.Go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            t.text = text;
            return t;
        }

        /// <summary>The app's raised button: a darker base under the face, label centred.</summary>
        public static Box Button(Box p, string label, Color c, float cx, float cy, float w, float h, A a = A.C,
            Color? text = null, float size = 44, Sprite icon = null)
        {
            Shadow(p, cx, cy + 6, w, h, 0.14f, 14, a);
            Rect(p, label + "Base", Pal.Darker(c), cx, cy + 9, w, h, h / 2, a);
            var face = Rect(p, "Button " + label, c, cx, cy, w, h, h / 2, a);
            face.Go.AddComponent<UnityEngine.UI.Button>();
            face.Go.GetComponent<Image>().raycastTarget = true;
            float tx = w / 2, tw = w - 40;
            if (icon != null)
            {
                // Icon + label as one centred group; label width estimated from its length.
                float isz = h * 0.42f, gap = 18f;
                tw = Mathf.Min(label.Length * size * 0.58f, w - isz - gap - 40);
                float left = (w - (isz + gap + tw)) / 2;
                Img(face, "Icon", icon, left + isz / 2, h / 2, isz, isz, A.C, text ?? Color.white);
                tx = left + isz + gap + tw / 2;
            }
            Txt(face, label, FontXB, size, text ?? Color.white, tx, h / 2 - 2, tw + 20, h);
            return face;
        }

        /// <summary>Round icon button (close, speaker, back).</summary>
        public static Box IconButton(Box p, string name, Sprite icon, Color bg, Color fg, float cx, float cy, float d, A a = A.C, float iconScale = 0.46f)
        {
            Shadow(p, cx, cy, d * 0.8f, d * 0.8f, 0.12f, 8, a);
            var b = Circle(p, name, bg, cx, cy, d, a);
            b.Go.AddComponent<UnityEngine.UI.Button>();
            Img(b, "Icon", icon, d / 2, d / 2, d * iconScale, d * iconScale, A.C, fg);
            return b;
        }

        /// <summary>White pill with an icon and a label (stars, streak).</summary>
        public static Box Pill(Box p, Sprite icon, string label, float cx, float cy, float w, A a = A.C, Color? bg = null, Color? fg = null, float h = 76)
        {
            Shadow(p, cx, cy, w, h, 0.10f, 8, a);
            var b = Rect(p, "Pill " + label, bg ?? Color.white, cx, cy, w, h, h / 2, a);
            if (icon != null) Img(b, "Icon", icon, 44, h / 2, h * 0.72f, h * 0.72f);
            Txt(b, label, FontXB, h * 0.44f, fg ?? Pal.Ink, icon != null ? (w + 60) / 2 : w / 2, h / 2 - 1, w - 70, h);
            return b;
        }

        /// <summary>Thin progress bar.</summary>
        public static Box Progress(Box p, float cx, float cy, float w, float h, float value, Color fill, Color track, A a = A.C)
        {
            var b = Rect(p, "Progress", track, cx, cy, w, h, h / 2, a);
            Rect(b, "Fill", fill, w * value / 2, h / 2, Mathf.Max(h, w * value), h, h / 2, A.L);
            return b;
        }

        // ── Sticker tiles (the Speech Blubs look) ──────────────────────────

        /// <summary>Crayon texture over a flat fill, so it reads painted, not vector.</summary>
        public static Box Paper(Box p, Color tint, float alpha)
        {
            var b = Fill(p, "Paper");
            var img = AddImage(b, Shapes.Paper, new Color(tint.r, tint.g, tint.b, alpha));
            img.type = Image.Type.Tiled;
            return b;
        }

        /// <summary>Hand-drawn outline around a rounded rect of radius <paramref name="r"/>.</summary>
        public static Box SketchFrame(Box p, float cx, float cy, float w, float h, float r, A a = A.C)
        {
            float pad = 7f * r / Shapes.SketchRadius;
            var b = Node(p, "Outline", cx, cy, w + 2 * pad, h + 2 * pad, a);
            var img = AddImage(b, Shapes.Sketch, Pal.Outline);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Shapes.SketchRadius / r;
            return b;
        }

        public static Box Sparkle(Box p, float cx, float cy, float size, A a = A.C, Color? c = null) =>
            Img(p, "Sparkle", Shapes.Sparkle, cx, cy, size, size, a, c ?? Color.white);

        /// <summary>Round badge hanging on a tile's corner (lock, play, done).</summary>
        public static Box CornerBadge(Box p, string name, float cx, float cy, float d, Color bg, Sprite icon, Color fg, A a, float k = 0.5f)
        {
            var holder = Node(p, name, cx, cy, d + 40, d + 40, a);
            Shadow(holder, (d + 40) / 2, (d + 40) / 2, d * 0.8f, d * 0.8f, 0.16f, 8);
            var b = Circle(holder, "Circle", bg, (d + 40) / 2, (d + 40) / 2, d);
            Img(b, "Icon", icon, d / 2 + (icon == Shapes.Triangle ? d * 0.04f : 0), d / 2, d * k, d * k, A.C, fg);
            return holder;
        }

        /// <summary>
        /// A sticker tile: darker base under it (thickness), white rim around a picture
        /// (cover) or a coloured face with a picture on it (icon), a hand-drawn outline,
        /// a label inside the bottom-left corner, a lock / play / done badge on the corner.
        /// Everything sits in one holder, so a runtime template can be cloned.
        /// </summary>
        public static TileParts Tile(Box p, string name, float cx, float cy, float w, float h, Color fill, Sprite pic,
            bool cover, A a = A.C, float r = 52, string label = null, float labelSize = 32,
            bool locked = false, bool play = false, float picScale = 0.74f, Color? rim = null,
            bool done = false, bool allBadges = false)
        {
            const float m = 80f;
            var holder = Node(p, name, cx, cy, w + 2 * m, h + 2 * m, a);
            float x = w / 2 + m, y = h / 2 + m;
            var t = new TileParts { Holder = holder };

            float depth = Mathf.Clamp(h * 0.035f, 10, 16);
            Shadow(holder, x, y + depth, w, h, 0.18f, 16);
            var baseRect = Rect(holder, "Base", Pal.Darker(fill, 0.68f), x, y + depth, w, h, r);
            t.Base = baseRect.Go.GetComponent<Image>();
            SketchFrame(holder, x, y + depth, w, h, r);

            var face = Rect(holder, "Face", cover ? (rim ?? Color.white) : fill, x, y, w, h, r);
            t.Face = face;
            t.Button = face.Go.AddComponent<UnityEngine.UI.Button>();
            face.Go.GetComponent<Image>().raycastTarget = true;
            face.Go.AddComponent<Mask>().showMaskGraphic = true;
            if (cover)
            {
                const float rimW = 12f;
                var frame = Cover(face, "Frame", pic, w / 2, h / 2, w - 2 * rimW, h - 2 * rimW, r - rimW);
                var picture = frame.Rt.Find("Picture");
                t.Picture = picture.GetComponent<Image>();
                t.Fit = picture.GetComponent<AspectRatioFitter>();
            }
            else
            {
                Paper(face, Pal.Darker(fill, 0.75f), 0.22f);
                Circle(face, "Glow", new Color(1, 1, 1, 0.35f), w / 2, h * 0.44f, Mathf.Min(w, h) * 0.8f);
                float s = Mathf.Min(w, h) * picScale;
                t.Picture = Img(face, "Picture", pic, w / 2, h / 2 - (label != null ? h * 0.07f : 0), s, s)
                    .Go.GetComponent<Image>();
            }
            SketchFrame(holder, x, y, w, h, r);

            if (label != null)
            {
                float lh = labelSize * 1.75f, lw = label.Length * labelSize * 0.56f + 50;
                float inset = cover ? 12f : 14f;
                var pill = Rect(face, "Label", new Color(1, 1, 1, 0.96f), inset + lw / 2, h - inset - lh / 2, lw, lh, lh / 2);
                var text = Txt(pill, label, FontXB, labelSize, Pal.Ink, lw / 2, lh / 2 - 1, lw, lh);
                text.enableWordWrapping = false;
                text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                t.Label = text;
                t.LabelPill = pill.Rt;
            }
            if (locked || allBadges)
                t.Lock = CornerBadge(holder, "Lock", x + w / 2 - 26, y + h / 2 - 22, 96, Color.white, Shapes.Lock, Pal.MintDark, A.C, 0.52f).Go;
            if (play || allBadges)
                t.Play = CornerBadge(holder, "Play", x + w / 2 - 34, y + h / 2 - 30, 130, Pal.Rose, Shapes.Triangle, Color.white, A.C, 0.46f).Go;
            if (done || allBadges)
                t.Done = CornerBadge(holder, "Done", x + w / 2 - 26, y + h / 2 - 22, 96, Pal.Green, Shapes.Check, Color.white, A.C, 0.56f).Go;
            if (allBadges)
            {
                t.Lock.SetActive(locked);
                t.Play.SetActive(play);
                t.Done.SetActive(done);
            }
            return t;
        }

        /// <summary>Square confetti and dots scattered over an area, from a fixed seed.</summary>
        public static void Confetti(Box p, int seed, int count, float x0, float y0, float x1, float y1)
        {
            var rnd = new System.Random(seed);
            var cols = new[] { Pal.Rose, Pal.Yellow, Pal.Teal, Pal.LavDeep, Pal.Orange, Pal.Green, Pal.Sky };
            var holder = Fill(p, "Confetti");
            for (int i = 0; i < count; i++)
            {
                float x = x0 + (float)rnd.NextDouble() * (x1 - x0), y = y0 + (float)rnd.NextDouble() * (y1 - y0);
                var c = cols[rnd.Next(cols.Length)];
                Box b = rnd.Next(3) == 0
                    ? Circle(holder, "Dot", c, x, y, 14 + rnd.Next(14))
                    : Rect(holder, "Bit", c, x, y, 14 + rnd.Next(12), 26 + rnd.Next(14), 5);
                b.Rt.localRotation = Quaternion.Euler(0, 0, rnd.Next(360));
            }
        }
    }
}
