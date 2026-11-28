using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// The picture being coloured. Two tools, and neither ever goes over a line:
    ///   • Fill — a tap pours the colour into the area under the finger; it spreads from
    ///     the finger in a circle.
    ///   • Crayon — the finger draws, but only inside the area where the stroke started.
    /// The «magic» crayon gives each area its colour from the original drawing.
    ///
    /// The paint is a texture the size of the page under the line art (<see cref="lines"/>);
    /// a transparent pixel is «not coloured yet» and shows the white card behind it. Every
    /// colour gets a crayon grain so the page looks drawn, like the rest of the art.
    /// </summary>
    public class ColoringCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public enum Tool { Fill, Crayon }

        public RawImage paint;
        public RawImage lines;

        public Tool tool = Tool.Fill;
        public Color32 color = new(232, 69, 60, 255);
        /// <summary>Each area in its original colour instead of <see cref="color"/>.</summary>
        public bool magic;
        public float crayonRadius = 26f;
        /// <summary>Off when another screen owns the input (the lesson task «Обведи» shows the page only).</summary>
        public bool interactive = true;

        /// <summary>After every fill, stroke and undo.</summary>
        public event Action Changed;

        public ColoringPage Page { get; private set; }
        public bool Dirty { get; private set; }
        public bool CanUndo => _undo.Count > 0;

        private int _w, _h;
        private ushort[] _area;
        private int[] _start;   // area k's pixels are _order[_start[k] .. _start[k + 1])
        private int[] _order;
        private Color32[] _original;
        private Texture2D _tex;
        private NativeArray<Color32> _px;
        private bool _upload;
        private static byte[] _grain;

        // A fill spreading from the finger.
        private int _fillArea = -1;
        private int[] _fillLeft;
        private int _fillCount;
        private Vector2 _fillFrom;
        private float _fillT, _fillTime, _fillReach;
        private Color32 _fillColor;
        private bool _fillMagic;

        // A crayon stroke.
        private int _pointer = int.MinValue;
        private int _strokeArea = -1;
        private Vector2 _last;
        private bool _strokeChanged;

        private struct Step
        {
            public int area;
            public Color32[] before;
        }
        private readonly List<Step> _undo = new();
        private long _undoBytes;
        private const int MaxUndo = 30;
        private const long MaxUndoBytes = 48L << 20;

        private static readonly Color32 Blank = new(255, 255, 255, 0);

        // ─────────────────────────── page ───────────────────────────

        /// <summary>Shows the page; <paramref name="saved"/> is the child's picture from an earlier visit (PNG) or null.</summary>
        public bool Open(ColoringPage page, byte[] saved)
        {
            Close();
            var map = ColoringPages.Areas(page);
            var lineArt = ColoringPages.Lines(page);
            if (map == null || lineArt == null) return false;

            Page = page;
            _w = page.width;
            _h = page.height;
            _area = map;
            _original = ColoringPages.OriginalColors(page);
            IndexAreas(page.areas);
            EnsureGrain();

            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                name = "Coloring " + page.id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            _px = _tex.GetPixelData<Color32>(0);
            for (int i = 0; i < _px.Length; i++) _px[i] = Blank;
            if (saved != null) Restore(saved);
            _tex.Apply(false);

            paint.texture = _tex;
            paint.color = Color.white;
            lines.texture = lineArt;
            Dirty = false;
            return true;
        }

        public void Close()
        {
            FinishFill();
            EndStroke();
            _undo.Clear();
            _undoBytes = 0;
            if (paint != null) paint.texture = null;
            if (_tex != null) Destroy(_tex);
            _tex = null;
            _px = default;
            _area = null;
            _order = null;
            _start = null;
            Page = null;
        }

        private void OnDestroy() => Close();

        private void IndexAreas(int areas)
        {
            _start = new int[areas + 2];
            for (int i = 0; i < _area.Length; i++) _start[Mathf.Min(_area[i], areas) + 1]++;
            for (int k = 1; k < _start.Length; k++) _start[k] += _start[k - 1];
            var fill = (int[])_start.Clone();
            _order = new int[_area.Length];
            for (int i = 0; i < _area.Length; i++) _order[fill[Mathf.Min(_area[i], areas)]++] = i;
        }

        private void Restore(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(png, false) || t.width != _w || t.height != _h) return;
                var src = t.GetPixels32();
                for (int i = 0; i < src.Length; i++) _px[i] = src[i];
            }
            finally
            {
                Destroy(t);
            }
        }

        // Crayon tooth: soft noise with a few streaks, tiled every 128 px.
        private static void EnsureGrain()
        {
            if (_grain != null) return;
            const int n = 128;
            var rnd = new System.Random(7);
            var raw = new float[n * n];
            for (int i = 0; i < raw.Length; i++) raw[i] = (float)rnd.NextDouble();
            _grain = new byte[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // A little horizontal smear makes it read as crayon, not as noise.
                float v = 0;
                for (int d = -2; d <= 2; d++) v += raw[y * n + ((x + d) & (n - 1))];
                v = v / 5f * 0.7f + raw[y * n + x] * 0.3f;
                _grain[y * n + x] = (byte)Mathf.Clamp(Mathf.RoundToInt((v - 0.2f) / 0.6f * 255f), 0, 255);
            }
        }

        private Color32 Shade(Color32 c, int i)
        {
            int x = i % _w, y = i / _w;
            int g = _grain[((y & 127) << 7) | (x & 127)];
            // Darker in the grain's valleys, a white fleck of paper on its peaks.
            float k = 1f - 0.10f * (g / 255f);
            float paper = g > 225 ? (g - 225) / 30f * 0.35f : 0f;
            return new Color32(
                (byte)(c.r * k + (255 - c.r * k) * paper),
                (byte)(c.g * k + (255 - c.g * k) * paper),
                (byte)(c.b * k + (255 - c.b * k) * paper), 255);
        }

        private bool Erasing(bool useMagic) => !useMagic && color.r == 255 && color.g == 255 && color.b == 255;

        private void Put(int i, Color32 c, bool useMagic)
        {
            if (useMagic) _px[i] = Shade(_original[Mathf.Min(_area[i], _original.Length - 1)], i);
            else if (Erasing(false)) _px[i] = Blank;
            else _px[i] = Shade(c, i);
        }

        // ─────────────────────────── input ───────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            if (Page == null || !interactive || _pointer != int.MinValue) return;
            if (!ToPixel(e, out var p)) return;
            int area = AreaAt(p);
            if (area <= 0) return;
            FinishFill();
            if (tool == Tool.Fill)
            {
                StartFill(area, p);
                return;
            }
            _pointer = e.pointerId;
            _strokeArea = area;
            _strokeChanged = false;
            Remember(area);
            _last = p;
            Stamp(p);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer || _strokeArea < 0) return;
            if (!ToPixel(e, out var p, clamp: true)) return;
            float step = Mathf.Max(2f, crayonRadius * 0.35f);
            float d = Vector2.Distance(_last, p);
            int n = Mathf.CeilToInt(d / step);
            for (int s = 1; s <= n; s++) Stamp(Vector2.Lerp(_last, p, s / (float)n));
            _last = p;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointer) EndStroke();
        }

        private void EndStroke()
        {
            if (_strokeArea >= 0)
            {
                if (_strokeChanged) { Dirty = true; Changed?.Invoke(); }
                else DropLastUndo();
            }
            _pointer = int.MinValue;
            _strokeArea = -1;
        }

        private bool ToPixel(PointerEventData e, out Vector2 p, bool clamp = false)
        {
            p = default;
            var rt = paint.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local))
                return false;
            var r = rt.rect;
            float u = (local.x - r.xMin) / r.width, v = (local.y - r.yMin) / r.height;
            if (!clamp && (u < 0 || u > 1 || v < 0 || v > 1)) return false;
            p = new Vector2(Mathf.Clamp(u, 0, 1) * (_w - 1), Mathf.Clamp(v, 0, 1) * (_h - 1));
            return true;
        }

        /// <summary>The area under the finger; on a line, the area the line belongs to.</summary>
        private int AreaAt(Vector2 p)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, _w - 1), y = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, _h - 1);
            return _area[y * _w + x];
        }

        // ─────────────────────────── fill ───────────────────────────

        private void StartFill(int area, Vector2 from)
        {
            Remember(area);
            Spread(_start[area], _start[area + 1], from, -1f, color, magic);
        }

        /// <summary>
        /// The whole drawing (not the outside) takes its original colours, spreading from
        /// <paramref name="from"/> (texture pixels, y up) over <paramref name="seconds"/>.
        /// </summary>
        public void Reveal(Vector2 from, float seconds)
        {
            if (Page == null || _start.Length < 4) return;
            FinishFill();
            Spread(_start[2], _start[_start.Length - 1], from, seconds, color, true);
        }

        /// <summary>Paints _order[a..b) in a circle growing from <paramref name="from"/>; time &lt; 0 = by size.</summary>
        private void Spread(int a, int b, Vector2 from, float time, Color32 c, bool useMagic)
        {
            _fillCount = b - a;
            if (_fillLeft == null || _fillLeft.Length < _fillCount) _fillLeft = new int[Mathf.Max(_fillCount, 1024)];
            Array.Copy(_order, a, _fillLeft, 0, _fillCount);
            // How far the farthest pixel is, so the circle reaches it right at the end.
            float reach = 0;
            for (int j = 0; j < _fillCount; j++)
            {
                int i = _fillLeft[j];
                float dx = i % _w - from.x, dy = i / _w - from.y;
                float d2 = dx * dx + dy * dy;
                if (d2 > reach) reach = d2;
            }
            _fillArea = Mathf.Max(0, a);
            _fillFrom = from;
            _fillReach = Mathf.Sqrt(reach) + 2f;
            _fillT = 0;
            _fillTime = time > 0 ? time : Mathf.Clamp(_fillReach / 1400f, 0.18f, 0.5f);
            _fillColor = c;
            _fillMagic = useMagic;
        }

        private void Update()
        {
            if (_fillArea < 0) return;
            _fillT += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_fillT / _fillTime);
            StepFill(k >= 1f ? float.MaxValue : _fillReach * (1f - (1f - k) * (1f - k)));
        }

        private void StepFill(float radius)
        {
            float r2 = radius >= float.MaxValue ? float.MaxValue : radius * radius;
            int keep = 0;
            for (int j = 0; j < _fillCount; j++)
            {
                int i = _fillLeft[j];
                float dx = i % _w - _fillFrom.x, dy = i / _w - _fillFrom.y;
                if (dx * dx + dy * dy <= r2) Put(i, _fillColor, _fillMagic);
                else _fillLeft[keep++] = i;
            }
            _fillCount = keep;
            _upload = true;
            if (_fillCount > 0) return;
            _fillArea = -1;
            Dirty = true;
            Changed?.Invoke();
        }

        /// <summary>Ends a spreading fill at once (before the next action, before saving).</summary>
        public void FinishFill()
        {
            if (_fillArea >= 0) StepFill(float.MaxValue);
        }

        // ─────────────────────────── crayon ───────────────────────────

        private void Stamp(Vector2 c)
        {
            float r = crayonRadius;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r)), x1 = Mathf.Min(_w - 1, Mathf.CeilToInt(c.x + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r)), y1 = Mathf.Min(_h - 1, Mathf.CeilToInt(c.y + r));
            float r2 = r * r, edge = (r * 0.72f) * (r * 0.72f);
            bool erase = Erasing(magic);
            for (int y = y0; y <= y1; y++)
            {
                float dy = y - c.y;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - c.x, d2 = dx * dx + dy * dy;
                    if (d2 > r2) continue;
                    int i = y * _w + x;
                    if (_area[i] != _strokeArea) continue;
                    // A ragged crayon edge: the rim skips the grain's valleys.
                    if (d2 > edge && !erase && _grain[((y & 127) << 7) | (x & 127)] < 110) continue;
                    Put(i, color, magic);
                    _strokeChanged = true;
                }
            }
            _upload = true;
        }

        // ─────────────────────────── undo ───────────────────────────

        private void Remember(int area)
        {
            int a = _start[area], b = _start[area + 1];
            var before = new Color32[b - a];
            for (int j = 0; j < before.Length; j++) before[j] = _px[_order[a + j]];
            _undo.Add(new Step { area = area, before = before });
            _undoBytes += before.Length * 4L;
            while (_undo.Count > MaxUndo || (_undoBytes > MaxUndoBytes && _undo.Count > 1))
            {
                _undoBytes -= _undo[0].before.Length * 4L;
                _undo.RemoveAt(0);
            }
        }

        private void DropLastUndo()
        {
            if (_undo.Count == 0) return;
            _undoBytes -= _undo[^1].before.Length * 4L;
            _undo.RemoveAt(_undo.Count - 1);
        }

        public void Undo()
        {
            if (Page == null) return;
            FinishFill();
            EndStroke();
            if (_undo.Count == 0) return;
            var s = _undo[^1];
            DropLastUndo();
            int a = _start[s.area];
            for (int j = 0; j < s.before.Length; j++) _px[_order[a + j]] = s.before[j];
            _upload = true;
            Dirty = true;
            Changed?.Invoke();
        }

        private void LateUpdate()
        {
            if (!_upload || _tex == null) return;
            _upload = false;
            _tex.Apply(false);
        }

        // ─────────────────────────── progress & saving ───────────────────────────

        /// <summary>
        /// Share of the drawing's areas the child has coloured (by count, not size: the
        /// small ones matter as much). Areas smaller than a fingertip and the outside don't count.
        /// </summary>
        public float Progress()
        {
            if (Page == null) return 0;
            int minSize = _w * _h / 400, total = 0, done = 0;
            for (int k = 2; k < _start.Length - 1; k++)
            {
                int a = _start[k], b = _start[k + 1];
                if (b - a < minSize) continue;
                total++;
                int painted = 0;
                for (int j = a; j < b; j += 7) if (_px[_order[j]].a != 0) painted++;
                if (painted * 7 >= (b - a) * 0.6f) done++;
            }
            return total == 0 ? 0 : done / (float)total;
        }

        public bool IsEmpty()
        {
            for (int i = 0; i < _px.Length; i += 3) if (_px[i].a != 0) return false;
            return true;
        }

        /// <summary>The picture as PNG, and a quarter-size thumbnail with the lines on it for the gallery.</summary>
        public (byte[] png, byte[] thumb) Encode()
        {
            FinishFill();
            if (_tex == null) return (null, null);
            if (IsEmpty()) return (null, null);
            var png = _tex.EncodeToPNG();
            return (png, Thumb(4));
        }

        private byte[] Thumb(int s)
        {
            int tw = _w / s, th = _h / s;
            var t = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            var o = t.GetPixelData<Color32>(0);
            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (int dy = 0; dy < s; dy++)
                for (int dx = 0; dx < s; dx++)
                {
                    var c = _px[(y * s + dy) * _w + x * s + dx];
                    r += c.r; g += c.g; b += c.b; a += c.a;
                }
                int n = s * s;
                o[y * tw + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
            }
            t.Apply(false);
            var bytes = t.EncodeToPNG();
            Destroy(t);
            return bytes;
        }
    }
}
