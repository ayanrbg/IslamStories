using System;
using System.IO;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>A coloring page from Resources/Coloring/pages.json (made by Tools/coloring/pages.js).</summary>
    [Serializable]
    public class ColoringPage
    {
        public string id;
        public ColoringTitle title;
        public int width;
        public int height;
        /// <summary>Areas a tap can fill; area 1 is the outside of the drawing.</summary>
        public int areas;
        /// <summary>Each area's colour in the original drawing, "#rrggbb" (index 0 = area 1).</summary>
        public string[] colors;

        public string Title => LessonStrings.Lang switch
        {
            "kz" => title?.kz,
            "en" => title?.en,
            _ => title?.ru,
        } ?? id;
    }

    [Serializable]
    public class ColoringTitle
    {
        public string ru, kz, en;
    }

    /// <summary>
    /// The coloring pages bundled with the app and the child's pictures. A page is three
    /// files in Resources/Coloring: <c>&lt;id&gt;_lines.png</c> (the line art),
    /// <c>&lt;id&gt;_regions.bytes</c> (a lossless PNG with the area number of every pixel
    /// in R + G·256) and the entry in <c>pages.json</c>. The child's picture is saved to
    /// <c>persistentDataPath/coloring/&lt;id&gt;.png</c> with a small <c>_thumb.png</c> for
    /// the gallery; a transparent pixel there means «not coloured yet».
    /// </summary>
    public static class ColoringPages
    {
        [Serializable]
        private class PageList
        {
            public ColoringPage[] pages;
        }

        private static ColoringPage[] _pages;

        public static ColoringPage[] All
        {
            get
            {
                if (_pages != null) return _pages;
                var json = Resources.Load<TextAsset>("Coloring/pages");
                _pages = json != null ? JsonUtility.FromJson<PageList>(json.text)?.pages : null;
                _pages ??= Array.Empty<ColoringPage>();
                return _pages;
            }
        }

        public static Texture2D Lines(ColoringPage page) => Resources.Load<Texture2D>($"Coloring/{page.id}_lines");

        /// <summary>The area of every pixel, rows bottom-up like a texture; null when the file is missing or broken.</summary>
        public static ushort[] Areas(ColoringPage page)
        {
            var asset = Resources.Load<TextAsset>($"Coloring/{page.id}_regions");
            if (asset == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(asset.bytes, false) || tex.width != page.width || tex.height != page.height)
                {
                    Debug.LogWarning($"[Coloring] bad area map for {page.id}");
                    return null;
                }
                var px = tex.GetPixels32();
                var map = new ushort[px.Length];
                for (int i = 0; i < px.Length; i++) map[i] = (ushort)(px[i].r | (px[i].g << 8));
                return map;
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
                Resources.UnloadAsset(asset);
            }
        }

        public static Color32[] OriginalColors(ColoringPage page)
        {
            var c = new Color32[page.areas + 1];
            c[0] = new Color32(255, 255, 255, 255);
            for (int k = 1; k <= page.areas; k++)
            {
                var hex = page.colors != null && k - 1 < page.colors.Length ? page.colors[k - 1] : null;
                c[k] = hex != null && ColorUtility.TryParseHtmlString(hex, out var col) ? (Color32)col : new Color32(255, 255, 255, 255);
                c[k].a = 255;
            }
            return c;
        }

        // ─────────────────────────── the child's pictures ───────────────────────────

        private static string Dir => Path.Combine(Application.persistentDataPath, "coloring");
        public static string PicturePath(string id) => Path.Combine(Dir, id + ".png");
        public static string ThumbPath(string id) => Path.Combine(Dir, id + "_thumb.png");

        public static byte[] LoadPicture(string id)
        {
            try
            {
                var p = PicturePath(id);
                return File.Exists(p) ? File.ReadAllBytes(p) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Coloring] read {id}: {e.Message}");
                return null;
            }
        }

        /// <summary>Writes the picture and its thumbnail; a picture with nothing on it is deleted instead.</summary>
        public static void SavePicture(string id, byte[] png, byte[] thumbPng)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (png == null)
                {
                    File.Delete(PicturePath(id));
                    File.Delete(ThumbPath(id));
                    return;
                }
                // Through a temp file, so a killed app never leaves half a picture.
                var tmp = PicturePath(id) + ".tmp";
                File.WriteAllBytes(tmp, png);
                if (File.Exists(PicturePath(id))) File.Delete(PicturePath(id));
                File.Move(tmp, PicturePath(id));
                if (thumbPng != null) File.WriteAllBytes(ThumbPath(id), thumbPng);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Coloring] save {id}: {e.Message}");
            }
        }

        /// <summary>The gallery picture of a page the child has coloured, or null.</summary>
        public static Texture2D LoadThumb(string id)
        {
            try
            {
                var p = ThumbPath(id);
                if (!File.Exists(p)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                if (tex.LoadImage(File.ReadAllBytes(p), true)) return tex;
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Coloring] thumb {id}: {e.Message}");
            }
            return null;
        }
    }
}
