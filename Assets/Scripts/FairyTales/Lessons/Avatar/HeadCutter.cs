using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>A head cut out of a photo, ready to sit on a drawn body.</summary>
    public class HeadCut
    {
        /// <summary>RGBA, transparent around the head; readable.</summary>
        public Texture2D Texture;
        /// <summary>Bottom of the chin, normalized in the texture (y from the TOP).</summary>
        public Vector2 Chin;
        /// <summary>Width of the face (cheek to cheek) as a share of the texture width.</summary>
        public float FaceWidth;
        /// <summary>The head is covered (hijab): the cut goes down to the shoulders.</summary>
        public bool Covered;
    }

    /// <summary>
    /// Cuts the child's head out of a photo the way Speech Blubs does — hair and all, no
    /// oval — on the phone, with MediaPipe Selfie Multiclass (Inference Engine, 256×256;
    /// classes: background, hair, body skin, face skin, clothes, others).
    ///
    /// Pass 1 finds the face in the whole picture; pass 2 looks again at a square around the
    /// head for a sharp mask. Kept: hair, face, and what sits on the head (cap, scarf). The
    /// neck and the clothes go — except a hijab, which is cut at the shoulders (its drape
    /// stays). Nothing leaves the phone.
    /// </summary>
    public sealed class HeadCutter : IDisposable
    {
        private const int N = 256;
        private const int Classes = 6;
        private const int Background = 0, Hair = 1, BodySkin = 2, FaceSkin = 3, Clothes = 4, Others = 5;
        /// <summary>Side of the cut-out before trimming.</summary>
        private const int Out = 512;

        private readonly Model _model;
        private Worker _worker;

        public HeadCutter(ModelAsset asset)
        {
            _model = ModelLoader.Load(asset);
            // Compute shaders where the phone has them; the CPU path (Burst) works everywhere.
            var backend = SystemInfo.supportsComputeShaders ? BackendType.GPUCompute : BackendType.CPU;
            _worker = new Worker(_model, backend);
        }

        public void Dispose()
        {
            _worker?.Dispose();
            _worker = null;
        }

        /// <summary>The head, or null when no face was found. <paramref name="photo"/> must be readable.</summary>
        public HeadCut Cut(Texture2D photo)
        {
            if (photo == null || !photo.isReadable) return null;
            float W = photo.width, H = photo.height;

            // ── pass 1: the whole picture, letterboxed into the square ──
            float side1 = Mathf.Max(W, H);
            var box1 = new Rect((W - side1) / 2f, (H - side1) / 2f, side1, side1); // pixels, y from the top
            var p1 = Run(photo, box1);
            var face1 = FaceBlob(p1);
            if (face1 == null || face1.Count < N * N * 0.004f) return null;
            Bounds(face1, out int fx0, out int fy0, out int fx1, out int fy1);
            float s1 = side1 / N;
            float faceW = (fx1 - fx0 + 1) * s1, faceH = (fy1 - fy0 + 1) * s1;
            float fcx = box1.x + (fx0 + fx1 + 1) / 2f * s1;
            float fTop = box1.y + fy0 * s1;

            // ── pass 2: a square around the head (hair above, shoulders below) ──
            float side2 = 3.0f * Mathf.Max(faceH, faceW * 1.15f);
            var box2 = new Rect(fcx - side2 / 2f, fTop - 1.05f * faceH, side2, side2);
            var p = Run(photo, box2);
            var face = FaceBlob(p);
            if (face == null || face.Count < N * N * 0.01f) return null;
            Bounds(face, out fx0, out fy0, out fx1, out fy1);
            float gFaceW = 0, chinX = 0;
            int chinY = fy1, bottomRows = 0;
            var inFace = new bool[N * N];
            foreach (var i in face) inFace[i] = true;
            for (int y = fy0; y <= fy1; y++)
            {
                int a = -1, b = -1;
                for (int x = fx0; x <= fx1; x++) if (inFace[y * N + x]) { if (a < 0) a = x; b = x; }
                if (a < 0) continue;
                gFaceW = Mathf.Max(gFaceW, b - a + 1);
                if (y >= fy1 - 4) { chinX += (a + b + 1) / 2f; bottomRows++; }
            }
            chinX = bottomRows > 0 ? chinX / bottomRows : (fx0 + fx1 + 1) / 2f;
            float gFaceH = fy1 - fy0 + 1;

            // Hijab: cloth hugs the cheeks on both sides. A cap sits only on top, hair falls
            // beside the face, so the sides tell a hijab from a kufi.
            int sideW = Mathf.Max(2, Mathf.RoundToInt(gFaceW * 0.22f));
            int midY = (fy0 + fy1) / 2;
            int cloth = 0, band = 0;
            for (int y = midY; y <= fy1; y++)
            {
                for (int x = fx0 - sideW; x < fx0; x++) Count(x, y);
                for (int x = fx1 + 1; x <= fx1 + sideW; x++) Count(x, y);
            }
            void Count(int x, int y)
            {
                if (x < 0 || x >= N) return;
                band++;
                int k = ArgMax(p, y * N + x);
                if (k == Clothes || k == Others) cloth++;
            }
            bool covered = band > 0 && cloth > band * 0.45f;

            // ── keep-probability on the 256 grid ──
            var keep = new float[N * N];
            float drapeBottom = chinY + gFaceH * 0.7f, drapeHalf = gFaceW * 1.3f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x, o = i * Classes;
                    float hairP = p[o + Hair], faceP = p[o + FaceSkin], clothP = p[o + Clothes], otherP = p[o + Others];
                    float k;
                    if (y <= chinY) k = hairP + faceP + otherP + clothP;           // cap, scarf framing the face
                    else
                    {
                        k = hairP;                                                 // long hair over the shoulders
                        if (y <= chinY + 2) k += faceP;
                        if (covered)
                        {
                            // Hijab down to the shoulders, with a soft round edge (no straight cut).
                            float ex = (x - chinX) / drapeHalf, ey = (y - chinY) / (drapeBottom - chinY);
                            k += (clothP + otherP) * (1f - Smooth(0.65f, 1f, ex * ex + ey * ey));
                        }
                    }
                    keep[i] = k;
                }
            var solid = LargestBlob(keep, 0.5f);

            // ── the cut-out at Out×Out over box2 ──
            var src = photo.GetPixels32();
            var pixels = new Color32[Out * Out];
            int minX = Out, minY = Out, maxX = -1, maxY = -1;
            for (int oy = 0; oy < Out; oy++)
            {
                float gy = (oy + 0.5f) / Out * N - 0.5f;          // grid row (y from the top)
                for (int ox = 0; ox < Out; ox++)
                {
                    float gx = (ox + 0.5f) / Out * N - 0.5f;
                    int ni = Mathf.Clamp(Mathf.RoundToInt(gy), 0, N - 1) * N + Mathf.Clamp(Mathf.RoundToInt(gx), 0, N - 1);
                    if (!solid[ni]) continue;
                    float a = Smooth(0.4f, 0.75f, Bilinear(keep, gx, gy));
                    if (a <= 0.01f) continue;
                    // Photo pixel (texture rows go bottom-up).
                    float px = box2.x + (ox + 0.5f) / Out * box2.width;
                    float py = box2.y + (oy + 0.5f) / Out * box2.height;
                    if (px < 0 || py < 0 || px >= W || py >= H) continue;
                    var c = Sample(src, photo.width, photo.height, px, H - py);
                    c.a = (byte)(a * 255);
                    int ty = Out - 1 - oy;                         // output rows bottom-up too
                    pixels[ty * Out + ox] = c;
                    if (ox < minX) minX = ox; if (ox > maxX) maxX = ox;
                    if (oy < minY) minY = oy; if (oy > maxY) maxY = oy;
                }
            }
            if (maxX < 0) return null;

            // Trim.
            int pad = 4;
            minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
            maxX = Mathf.Min(Out - 1, maxX + pad); maxY = Mathf.Min(Out - 1, maxY + pad);
            int tw = maxX - minX + 1, th = maxY - minY + 1;
            var trimmed = new Color32[tw * th];
            for (int y = 0; y < th; y++)
            {
                int srcRow = Out - 1 - (minY + y);                // top-down y → texture row
                int dstRow = th - 1 - y;
                Array.Copy(pixels, srcRow * Out + minX, trimmed, dstRow * tw, tw);
            }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(trimmed);
            tex.Apply();

            float k2 = (float)Out / N;
            return new HeadCut
            {
                Texture = tex,
                Chin = new Vector2((chinX * k2 - minX) / tw, ((chinY + 1) * k2 - minY) / th),
                FaceWidth = gFaceW * k2 / tw,
                Covered = covered,
            };
        }

        // ─────────────────────────── model ───────────────────────────

        /// <summary>Class probabilities [N*N*Classes] for a square of the photo (pixels, y from the top).</summary>
        private float[] Run(Texture2D photo, Rect box)
        {
            var src = photo.GetPixels32();
            int w = photo.width, h = photo.height;
            var input = new float[N * N * 3];
            for (int y = 0; y < N; y++)
            {
                float py = box.y + (y + 0.5f) / N * box.height;
                for (int x = 0; x < N; x++)
                {
                    float px = box.x + (x + 0.5f) / N * box.width;
                    int i = (y * N + x) * 3;
                    if (px < 0 || py < 0 || px >= w || py >= h) { input[i] = input[i + 1] = input[i + 2] = 0.5f; continue; }
                    var c = Sample(src, w, h, px, h - py);
                    input[i] = c.r / 255f; input[i + 1] = c.g / 255f; input[i + 2] = c.b / 255f;
                }
            }
            using var tensor = new Tensor<float>(new TensorShape(1, N, N, 3), input);
            _worker.Schedule(tensor);
            using var output = (_worker.PeekOutput() as Tensor<float>).ReadbackAndClone();
            var logits = output.DownloadToArray();
            // Softmax per pixel (the model gives raw scores).
            for (int i = 0; i < N * N; i++)
            {
                int o = i * Classes;
                float m = logits[o];
                for (int k = 1; k < Classes; k++) m = Mathf.Max(m, logits[o + k]);
                float sum = 0;
                for (int k = 0; k < Classes; k++) { logits[o + k] = Mathf.Exp(logits[o + k] - m); sum += logits[o + k]; }
                for (int k = 0; k < Classes; k++) logits[o + k] /= sum;
            }
            return logits;
        }

        // ─────────────────────────── mask helpers ───────────────────────────

        private static int ArgMax(float[] p, int i)
        {
            int o = i * Classes, best = 0;
            for (int k = 1; k < Classes; k++) if (p[o + k] > p[o + best]) best = k;
            return best;
        }

        /// <summary>Largest connected area where face skin wins.</summary>
        private static List<int> FaceBlob(float[] p)
        {
            var seen = new bool[N * N];
            List<int> best = null;
            var stack = new Stack<int>();
            for (int s = 0; s < N * N; s++)
            {
                if (seen[s] || ArgMax(p, s) != FaceSkin) continue;
                var blob = new List<int>();
                seen[s] = true;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop(), x = i % N, y = i / N;
                    blob.Add(i);
                    void Visit(int j) { if (!seen[j] && ArgMax(p, j) == FaceSkin) { seen[j] = true; stack.Push(j); } }
                    if (x > 0) Visit(i - 1);
                    if (x < N - 1) Visit(i + 1);
                    if (y > 0) Visit(i - N);
                    if (y < N - 1) Visit(i + N);
                }
                if (best == null || blob.Count > best.Count) best = blob;
            }
            return best;
        }

        private static bool[] LargestBlob(float[] v, float threshold)
        {
            var label = new int[N * N];
            int bestLabel = 0, bestCount = 0, next = 0;
            var stack = new Stack<int>();
            for (int s = 0; s < N * N; s++)
            {
                if (label[s] != 0 || v[s] < threshold) continue;
                next++;
                int count = 0;
                label[s] = next;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop(), x = i % N, y = i / N;
                    count++;
                    void Visit(int j) { if (label[j] == 0 && v[j] >= threshold) { label[j] = next; stack.Push(j); } }
                    if (x > 0) Visit(i - 1);
                    if (x < N - 1) Visit(i + 1);
                    if (y > 0) Visit(i - N);
                    if (y < N - 1) Visit(i + N);
                }
                if (count > bestCount) { bestCount = count; bestLabel = next; }
            }
            // Grow by one cell so the soft edge around the blob is not cut off.
            var solid = new bool[N * N];
            for (int i = 0; i < N * N; i++)
            {
                if (label[i] != bestLabel) continue;
                int x = i % N, y = i / N;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && yy >= 0 && xx < N && yy < N) solid[yy * N + xx] = true;
                }
            }
            return solid;
        }

        private static void Bounds(List<int> blob, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = N; y0 = N; x1 = 0; y1 = 0;
            foreach (var i in blob)
            {
                int x = i % N, y = i / N;
                if (x < x0) x0 = x; if (x > x1) x1 = x;
                if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        }

        private static float Bilinear(float[] g, float x, float y)
        {
            x = Mathf.Clamp(x, 0, N - 1); y = Mathf.Clamp(y, 0, N - 1);
            int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, N - 1), y1 = Mathf.Min(y0 + 1, N - 1);
            float fx = x - x0, fy = y - y0;
            float a = Mathf.Lerp(g[y0 * N + x0], g[y0 * N + x1], fx);
            float b = Mathf.Lerp(g[y1 * N + x0], g[y1 * N + x1], fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static float Smooth(float e0, float e1, float v)
        {
            float t = Mathf.Clamp01((v - e0) / (e1 - e0));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Bilinear sample of a pixel array; (x, y) in pixels, y up (texture rows).</summary>
        private static Color32 Sample(Color32[] src, int w, int h, float x, float y)
        {
            x = Mathf.Clamp(x - 0.5f, 0, w - 1); y = Mathf.Clamp(y - 0.5f, 0, h - 1);
            int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = x - x0, fy = y - y0;
            var a = Color32.Lerp(src[y0 * w + x0], src[y0 * w + x1], fx);
            var b = Color32.Lerp(src[y1 * w + x0], src[y1 * w + x1], fx);
            return Color32.Lerp(a, b, fy);
        }
    }
}
