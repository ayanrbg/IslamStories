using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>
    /// Makes the friend's head for a lesson (the «friendHead» media of the repeat step) from a
    /// face photo of the child actor: the same head cutter as the app, then the standard
    /// framing the app expects — 512×768, chin at (256, 512), face 256 px wide — so no anchors
    /// have to travel with the picture.
    ///
    ///   Nooria ▸ Lessons ▸ Make friend head from photo…
    ///
    /// Upload the PNG to the lesson's media as friend_head.png (it keeps its transparency).
    /// </summary>
    public static class FriendHeadTool
    {
        public const int Width = 512, Height = 768;

        [MenuItem("Nooria/Lessons/Make friend head from photo…")]
        public static void Menu()
        {
            var src = EditorUtility.OpenFilePanel("Face photo of the friend", "", "jpg,jpeg,png");
            if (string.IsNullOrEmpty(src)) return;
            var dst = EditorUtility.SaveFilePanel("Save the friend head", Path.GetDirectoryName(src), "friend_head", "png");
            if (string.IsNullOrEmpty(dst)) return;
            Debug.Log(Make(src, dst));
        }

        /// <summary>Cuts the head out of <paramref name="photoPath"/> and writes the framed PNG.</summary>
        public static string Make(string photoPath, string outPath)
        {
            var photo = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!photo.LoadImage(File.ReadAllBytes(photoPath), false)) return "can't read " + photoPath;
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/ThirdParty/MediaPipe/selfie_multiclass_fp16.sentis");
            using var cutter = new HeadCutter(model);
            var cut = cutter.Cut(photo);
            Object.DestroyImmediate(photo);
            if (cut == null) return "no face found in " + photoPath;
            var framed = Frame(cut);
            File.WriteAllBytes(outPath, framed.EncodeToPNG());
            Object.DestroyImmediate(framed);
            Object.DestroyImmediate(cut.Texture);
            return $"friend head → {outPath} (hijab: {cut.Covered})";
        }

        /// <summary>The cut-out scaled and moved into the standard frame.</summary>
        public static Texture2D Frame(HeadCut cut)
        {
            var src = cut.Texture;
            float faceW = RepeatStep.FriendHeadFace * Width;              // 256 px
            float k = faceW / (cut.FaceWidth * src.width);                 // source px → frame px
            var chin = new Vector2(RepeatStep.FriendHeadChin.x * Width, RepeatStep.FriendHeadChin.y * Height); // y from the top
            var srcChin = new Vector2(cut.Chin.x * src.width, cut.Chin.y * src.height);

            var px = src.GetPixels32();
            var outPx = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    // Frame pixel → source pixel (both y from the top here).
                    float sx = srcChin.x + (x + 0.5f - chin.x) / k - 0.5f;
                    float sy = srcChin.y + (y + 0.5f - chin.y) / k - 0.5f;
                    if (sx < -1 || sy < -1 || sx > src.width || sy > src.height) continue;
                    outPx[(Height - 1 - y) * Width + x] = Bilinear(px, src.width, src.height, sx, src.height - 1 - sy);
                }
            }
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx);
            tex.Apply();
            return tex;
        }

        private static Color32 Bilinear(Color32[] p, int w, int h, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            Color Get(int xx, int yy) => xx < 0 || yy < 0 || xx >= w || yy >= h ? new Color(0, 0, 0, 0) : (Color)p[yy * w + xx];
            // Premultiplied, so transparent neighbours don't darken the edge.
            Color Pm(Color c) => new(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            var c00 = Pm(Get(x0, y0)); var c10 = Pm(Get(x0 + 1, y0));
            var c01 = Pm(Get(x0, y0 + 1)); var c11 = Pm(Get(x0 + 1, y0 + 1));
            var c = Color.Lerp(Color.Lerp(c00, c10, fx), Color.Lerp(c01, c11, fx), fy);
            if (c.a > 1e-4f) { c.r /= c.a; c.g /= c.a; c.b /= c.a; }
            return c;
        }
    }
}
