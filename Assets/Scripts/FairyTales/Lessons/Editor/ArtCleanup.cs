using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>
    /// Clean-up of the stand-in art cut from generated sheets: a figure keeps only its own
    /// shape — bits of a neighbour's hand or scarf left at the edge of the cut are erased.
    /// The picture keeps its size, so measured anchors (neck, chin) stay valid.
    ///
    ///   Nooria ▸ Lessons ▸ Clean stray bits from bodies and characters
    /// </summary>
    public static class ArtCleanup
    {
        [MenuItem("Nooria/Lessons/Clean stray bits from bodies and characters")]
        public static string CleanAll()
        {
            var files = new List<string>();
            files.AddRange(Directory.GetFiles("Assets/UI/Lessons/Avatar2D", "body_*.png"));
            files.AddRange(Directory.GetFiles("Assets/UI/Lessons/Art", "char_*.png"));
            var report = new List<string>();
            foreach (var f in files)
            {
                int removed = KeepFigure(f);
                if (removed > 0) report.Add($"{Path.GetFileName(f)}: −{removed} px");
            }
            AssetDatabase.Refresh();
            return report.Count == 0 ? "nothing to clean" : string.Join(", ", report);
        }

        /// <summary>Erases opaque pieces not connected to the figure; returns the pixels erased.</summary>
        public static int KeepFigure(string path, float minShare = 0.08f)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path), false)) return 0;
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var label = new int[w * h];
            var sizes = new List<int> { 0 };
            var stack = new Stack<int>();
            for (int s = 0; s < px.Length; s++)
            {
                if (label[s] != 0 || px[s].a < 24) continue;
                int id = sizes.Count, count = 0;
                sizes.Add(0);
                label[s] = id;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop(), x = i % w, y = i / w;
                    count++;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        int j = yy * w + xx;
                        if (label[j] == 0 && px[j].a >= 24) { label[j] = id; stack.Push(j); }
                    }
                }
                sizes[id] = count;
            }
            int largest = 0;
            foreach (var c in sizes) largest = Mathf.Max(largest, c);
            int removed = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                // Faint pixels belong to whatever opaque piece is next to them; judged by that piece.
                int id = label[i];
                if (id == 0) id = Neighbour(label, i, w, h);
                if (id != 0 && sizes[id] >= largest * minShare) continue;
                px[i] = new Color32(0, 0, 0, 0);
                removed++;
            }
            if (removed > 0)
            {
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            Object.DestroyImmediate(tex);
            return removed;
        }

        private static int Neighbour(int[] label, int i, int w, int h)
        {
            int x = i % w, y = i / w;
            for (int r = 1; r <= 2; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                if (label[yy * w + xx] != 0) return label[yy * w + xx];
            }
            return 0;
        }
    }
}
