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

            // ── pass 2: a tighter square around the head — more of the model's 256 points on it ──
            // Hair or a cap above the face (~0.6 of its height), a hijab's drape below the chin.
            float side2 = Mathf.Max(2.45f * faceH, 3.1f * faceW);
            var box2 = new Rect(fcx - side2 / 2f, fTop - 0.68f * faceH, side2, side2);
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
            float s2 = side2 / N;                                   // photo pixels per grid cell
            var src = photo.GetPixels32();

            // Hijab: cloth hugs the cheeks on both sides. A cap sits only on top, hair falls
            // beside the face, so the sides tell a hijab from a kufi. The cloth seen there also
            // gives the hijab's colour.
            int sideW = Mathf.Max(2, Mathf.RoundToInt(gFaceW * 0.22f));
            int midY = (fy0 + fy1) / 2;
            int cloth = 0, band = 0;
            var hijabSum = Vector3.zero;
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
                if (k != Clothes && k != Others) return;
                cloth++;
                hijabSum += Rgb(src, photo.width, photo.height, box2.x + (x + 0.5f) * s2, box2.y + (y + 0.5f) * s2);
            }
            bool covered = band > 0 && cloth > band * 0.45f;
            var hijabColor = cloth > 0 ? hijabSum / cloth : Vector3.zero;

            // How much a grid cell looks like the hijab. The tint decides, brightness barely:
            // folds and shade on the scarf pass, a grey wall's shadow or a white shirt don't.
            float hijabL = (hijabColor.x + hijabColor.y + hijabColor.z) / 3f;
            var hijabTint = hijabColor - new Vector3(hijabL, hijabL, hijabL);
            float HijabLike(int x, int y)
            {
                var c = Rgb(src, photo.width, photo.height, box2.x + (x + 0.5f) * s2, box2.y + (y + 0.5f) * s2);
                float l = (c.x + c.y + c.z) / 3f;
                var tint = c - new Vector3(l, l, l);
                float d2 = 0.1f * (l - hijabL) * (l - hijabL) + 2f * (tint - hijabTint).sqrMagnitude;
                return Mathf.Exp(-d2 / (2f * 0.1f * 0.1f));
            }

            // ── keep-probability on the 256 grid ──
            var keep = new float[N * N];
            // The drape ends in a soft round edge — inside the photo, so the photo's own edge
            // never makes a straight cut.
            float photoBottom = (H - box2.y) / s2 - 2f;
            float drapeBottom = Mathf.Min(chinY + gFaceH * 0.7f, photoBottom);
            float drapeHalf = gFaceW * 1.3f;
            float capLine = fy0 + gFaceH * 0.35f;                  // above the eyes: a cap, a scarf's top
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x, o = i * Classes;
                    float hairP = p[o + Hair], faceP = p[o + FaceSkin], skinP = p[o + BodySkin];
                    float clothP = p[o + Clothes], otherP = p[o + Others];
                    float k;
                    if (y <= chinY)
                    {
                        k = hairP + faceP;
                        // Ears are "body skin" to the model: keep skin beside the face at ear height,
                        // never the neck beside the jaw.
                        if (y >= fy0 + gFaceH * 0.22f && y <= fy0 + gFaceH * 0.62f
                            && x >= fx0 - gFaceW * 0.3f && x <= fx1 + gFaceW * 0.3f) k += skinP;
                        if (covered) k += (clothP + otherP) * HijabLike(x, y);   // the scarf, not the wall's shadow
                        else if (y < capLine) k += clothP + otherP;              // a cap; never a collar
                    }
                    else
                    {
                        k = hairP;                                               // long hair over the shoulders
                        if (y <= chinY + 2) k += faceP;
                        if (covered && y < drapeBottom)
                        {
                            // Hijab down to the shoulders, with a soft round edge (no straight cut).
                            float ex = (x - chinX) / drapeHalf, ey = (y - chinY) / Mathf.Max(1f, drapeBottom - chinY);
                            k += (clothP + otherP) * HijabLike(x, y) * (1f - Smooth(0.9f, 1f, ex * ex + ey * ey));
                        }
                    }
                    keep[i] = Mathf.Clamp01(k);
                }
            var solid = LargestBlob(keep, 0.5f);
            Close(solid, 3);                                        // fill small bites in the outline
            // Holes inside the head (shade under a scarf, dark hair between scarf and face) are
            // part of it: whatever the outside can't reach is filled.
            // (Tested on the kept cells, not on solid: solid is grown by a cell around them.)
            var core = new bool[N * N];
            for (int i = 0; i < core.Length; i++) core[i] = solid[i] && keep[i] >= 0.5f;
            var hole = Holes(core);
            // A gap open at the top (dark hair between a scarf and the forehead) isn't a hole to
            // the border test: rows across the face and the scarf are filled side to side.
            int spanTop = Mathf.Max(0, Mathf.RoundToInt(fy0 - gFaceH * 0.6f));
            for (int y = spanTop; y <= chinY; y++)
            {
                int a = -1, b = -1;
                for (int x = 0; x < N; x++) if (core[y * N + x]) { if (a < 0) a = x; b = x; }
                for (int x = a + 1; x < b; x++) if (!core[y * N + x]) hole[y * N + x] = true;
            }
            for (int i = 0; i < hole.Length; i++) if (hole[i]) { solid[i] = true; keep[i] = 1f; }

            // ── coarse alpha at Out×Out over box2, then fitted to the photo's real edges ──
            var guide = new Vector3[Out * Out];
            var coarse = new float[Out * Out];
            var near = new bool[Out * Out];
            var grown = Grow(solid, 3);                             // room for the edge to move
            for (int oy = 0; oy < Out; oy++)
            {
                float gy = (oy + 0.5f) / Out * N - 0.5f;          // grid row (y from the top)
                float py = box2.y + (oy + 0.5f) / Out * box2.height;
                for (int ox = 0; ox < Out; ox++)
                {
                    float gx = (ox + 0.5f) / Out * N - 0.5f;
                    float px = box2.x + (ox + 0.5f) / Out * box2.width;
                    int o = oy * Out + ox;
                    guide[o] = Rgb(src, photo.width, photo.height, px, py);
                    int ni = Mathf.Clamp(Mathf.RoundToInt(gy), 0, N - 1) * N + Mathf.Clamp(Mathf.RoundToInt(gx), 0, N - 1);
                    bool inside = px >= 0 && py >= 0 && px < W && py < H;
                    near[o] = inside && grown[ni];
                    coarse[o] = inside && solid[ni] ? Smooth(0.35f, 0.7f, Bilinear(keep, gx, gy)) : 0f;
                }
            }
            var alpha = GuidedFilter(guide, coarse, Out, Out, Out / 64, 1e-4f, out var contrast);
            // Where the photo has no edge to follow (a white cap on a pale wall), keep the model's line.
            for (int o = 0; o < alpha.Length; o++)
                alpha[o] = Mathf.Lerp(coarse[o], alpha[o], Mathf.Clamp01(contrast[o] / 0.003f));
            // A filled hole stays filled even where its colour differs from the scarf around it.
            var holeCore = Shrink(Grow(hole, 1), 2);
            for (int oy = 0; oy < Out; oy++)
                for (int ox = 0; ox < Out; ox++)
                {
                    int gx = Mathf.Clamp(ox * N / Out, 0, N - 1), gy = Mathf.Clamp(oy * N / Out, 0, N - 1);
                    if (holeCore[gy * N + gx]) alpha[oy * Out + ox] = 1f;
                }

            // A clean cut, not a feathered one: the mask is evened out (so knitted caps and
            // fabric texture don't make it ragged), cut at the middle, then given a ~2 px
            // anti-aliased edge like a hand-made cut-out.
            BoxMean(alpha, Out, Out, 2);
            for (int o = 0; o < alpha.Length; o++) alpha[o] = Smooth(0.44f, 0.56f, alpha[o]);
            BoxMean(alpha, Out, Out, 1);

            var pixels = new Color32[Out * Out];
            int minX = Out, minY = Out, maxX = -1, maxY = -1;
            for (int oy = 0; oy < Out; oy++)
            {
                for (int ox = 0; ox < Out; ox++)
                {
                    int o = oy * Out + ox;
                    if (!near[o]) continue;
                    float a = alpha[o];
                    if (a <= 0.02f) continue;
                    var g = guide[o];
                    var c = new Color32((byte)(g.x * 255f), (byte)(g.y * 255f), (byte)(g.z * 255f), (byte)(a * 255f));
                    int ty = Out - 1 - oy;                         // output rows bottom-up
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

        // ─────────────────────────── edge fitting ───────────────────────────

        /// <summary>
        /// Colour guided filter (He, Sun, Tang): the coarse mask <paramref name="p"/> follows the
        /// edges of the picture <paramref name="I"/> — hair against a wall, a scarf against a
        /// shirt — instead of the model's blocky 256-grid outline. O(pixels), box sums.
        /// </summary>
        private static float[] GuidedFilter(Vector3[] I, float[] p, int w, int h, int r, float eps, out float[] contrast)
        {
            contrast = new float[w * h];
            int n = w * h;
            var ch = new float[13][];
            for (int c = 0; c < 13; c++) ch[c] = new float[n];
            for (int i = 0; i < n; i++)
            {
                var v = I[i];
                float q = p[i];
                ch[0][i] = v.x; ch[1][i] = v.y; ch[2][i] = v.z; ch[3][i] = q;
                ch[4][i] = v.x * v.x; ch[5][i] = v.x * v.y; ch[6][i] = v.x * v.z;
                ch[7][i] = v.y * v.y; ch[8][i] = v.y * v.z; ch[9][i] = v.z * v.z;
                ch[10][i] = v.x * q; ch[11][i] = v.y * q; ch[12][i] = v.z * q;
            }
            for (int c = 0; c < 13; c++) BoxMean(ch[c], w, h, r);

            var a = new float[4][];
            for (int c = 0; c < 4; c++) a[c] = new float[n];
            for (int i = 0; i < n; i++)
            {
                float mr = ch[0][i], mg = ch[1][i], mb = ch[2][i], mp = ch[3][i];
                float rr = ch[4][i] - mr * mr + eps, rg = ch[5][i] - mr * mg, rb = ch[6][i] - mr * mb;
                float gg = ch[7][i] - mg * mg + eps, gb = ch[8][i] - mg * mb, bb = ch[9][i] - mb * mb + eps;
                float cr = ch[10][i] - mr * mp, cg = ch[11][i] - mg * mp, cb = ch[12][i] - mb * mp;
                contrast[i] = rr + gg + bb - 3f * eps;       // local colour variance
                // Inverse of the symmetric 3×3 covariance.
                float i00 = gg * bb - gb * gb, i01 = gb * rb - rg * bb, i02 = rg * gb - gg * rb;
                float i11 = rr * bb - rb * rb, i12 = rb * rg - rr * gb, i22 = rr * gg - rg * rg;
                float det = rr * i00 + rg * i01 + rb * i02;
                if (Mathf.Abs(det) < 1e-12f) { a[0][i] = a[1][i] = a[2][i] = 0; a[3][i] = mp; continue; }
                float inv = 1f / det;
                float ar = (i00 * cr + i01 * cg + i02 * cb) * inv;
                float ag = (i01 * cr + i11 * cg + i12 * cb) * inv;
                float ab = (i02 * cr + i12 * cg + i22 * cb) * inv;
                a[0][i] = ar; a[1][i] = ag; a[2][i] = ab;
                a[3][i] = mp - ar * mr - ag * mg - ab * mb;
            }
            for (int c = 0; c < 4; c++) BoxMean(a[c], w, h, r);

            var q2 = new float[n];
            for (int i = 0; i < n; i++)
            {
                var v = I[i];
                q2[i] = Mathf.Clamp01(a[0][i] * v.x + a[1][i] * v.y + a[2][i] * v.z + a[3][i]);
            }
            return q2;
        }

        /// <summary>In-place mean over a (2r+1)² window, clipped at the borders (two running-sum passes).</summary>
        private static void BoxMean(float[] v, int w, int h, int r)
        {
            var tmp = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                double sum = 0;
                int count = 0;
                for (int x = 0; x <= r && x < w; x++) { sum += v[row + x]; count++; }
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = (float)(sum / count);
                    int add = x + r + 1, sub = x - r;
                    if (add < w) { sum += v[row + add]; count++; }
                    if (sub >= 0) { sum -= v[row + sub]; count--; }
                }
            }
            for (int x = 0; x < w; x++)
            {
                double sum = 0;
                int count = 0;
                for (int y = 0; y <= r && y < h; y++) { sum += tmp[y * w + x]; count++; }
                for (int y = 0; y < h; y++)
                {
                    v[y * w + x] = (float)(sum / count);
                    int add = y + r + 1, sub = y - r;
                    if (add < h) { sum += tmp[add * w + x]; count++; }
                    if (sub >= 0) { sum -= tmp[sub * w + x]; count--; }
                }
            }
        }

        /// <summary>Cells outside <paramref name="m"/> that the picture's border can't reach.</summary>
        private static bool[] Holes(bool[] m)
        {
            var outside = new bool[N * N];
            var stack = new Stack<int>();
            for (int i = 0; i < N; i++)
            {
                Seed(i); Seed((N - 1) * N + i); Seed(i * N); Seed(i * N + N - 1);
            }
            void Seed(int j) { if (!m[j] && !outside[j]) { outside[j] = true; stack.Push(j); } }
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % N, y = i / N;
                if (x > 0) Seed(i - 1);
                if (x < N - 1) Seed(i + 1);
                if (y > 0) Seed(i - N);
                if (y < N - 1) Seed(i + N);
            }
            var holes = new bool[N * N];
            for (int i = 0; i < N * N; i++) holes[i] = !m[i] && !outside[i];
            return holes;
        }

        /// <summary>Morphological closing on the grid: grow then shrink by <paramref name="r"/> cells.</summary>
        private static void Close(bool[] m, int r)
        {
            var grown = Grow(m, r);
            var shrunk = Shrink(grown, r);
            Array.Copy(shrunk, m, m.Length);
        }

        private static bool[] Grow(bool[] m, int r)
        {
            var o = new bool[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (!m[y * N + x]) continue;
                    for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy > r * r) continue;
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < N && yy < N) o[yy * N + xx] = true;
                    }
                }
            return o;
        }

        private static bool[] Shrink(bool[] m, int r)
        {
            var o = new bool[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (!m[y * N + x]) continue;
                    bool all = true;
                    for (int dy = -r; dy <= r && all; dy++)
                    for (int dx = -r; dx <= r && all; dx++)
                    {
                        if (dx * dx + dy * dy > r * r) continue;
                        int xx = x + dx, yy = y + dy;
                        // The picture's border counts as inside, so a head touching it stays whole.
                        if (xx < 0 || yy < 0 || xx >= N || yy >= N) continue;
                        all = m[yy * N + xx];
                    }
                    o[y * N + x] = all;
                }
            return o;
        }

        /// <summary>Photo colour 0..1 at (x, y) in pixels, y from the TOP (outside → grey).</summary>
        private static Vector3 Rgb(Color32[] src, int w, int h, float x, float yTop)
        {
            if (x < 0 || yTop < 0 || x >= w || yTop >= h) return new Vector3(0.5f, 0.5f, 0.5f);
            var c = Sample(src, w, h, x, h - yTop);
            return new Vector3(c.r / 255f, c.g / 255f, c.b / 255f);
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
