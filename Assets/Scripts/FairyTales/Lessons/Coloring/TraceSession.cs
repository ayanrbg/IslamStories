using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Tracing a coloring page's outline («Обведи»), shared by the lesson step and the
    /// «Обведи и раскрась» practice. The outline is a dotted path; a green dot waits where to
    /// go next; the finger pulls a thick crayon line along — the PATH, never the raw finger,
    /// so the line comes out neat even from a wobbly hand. Wandering off loses nothing.
    ///
    /// Made for small hands: a finger-wide tolerance, either direction from the start, no
    /// cutting across (the finger can only pull the line a little way ahead), a pointing hand
    /// at the start and whenever the child stalls, a wider path after a while without progress.
    /// </summary>
    [Serializable]
    public class TraceSession
    {
        public RawImage guide;
        public RawImage ink;
        public TracePad pad;
        /// <summary>Children of the picture, placed in its local space.</summary>
        public RectTransform dot;
        public RectTransform hand;

        public Color guideColor = new(0.48f, 0.36f, 0.84f, 0.75f);
        public Color inkColor = new(0.88f, 0.34f, 0.48f, 1f);
        [Range(0.1f, 1f)] public float linesWhileTracing = 0.4f;
        /// <summary>How far from the path a finger still counts, page pixels (the page is 1280 wide).</summary>
        public float tolerance = 62f;
        /// <summary>How many points ahead a finger may pull the line (points are ~14 px apart).</summary>
        public int lookAhead = 7;

        /// <summary>Once round; the picture has started to change (reveal or plain lines).</summary>
        public event Action Finished;

        public bool Running => _path != null && !_done;
        public float Seconds => Time.realtimeSinceStartup - _startedAt;
        /// <summary>The path's last point in texture pixels (y up): where a reveal starts.</summary>
        public Vector2 EndPixel => _path == null ? Vector2.zero : new Vector2(_path[^1].x, _page.height - _path[^1].y);

        private MonoBehaviour _host;
        private Func<bool> _paused;
        private ColoringCanvas _picture;
        private ColoringPage _page;
        private Vector2[] _path;
        private int _reached;
        private bool _done, _chose, _reveal;
        private float _lastProgress, _startedAt;
        private Texture2D _guideTex, _inkTex;
        private Color32[] _inkPx;
        private int _tw, _th;
        private Coroutine _hints;
        private const int Scale = 2; // guide and ink at half the page's resolution

        private bool Paused => _paused != null && _paused();

        public void Bind(MonoBehaviour host, Func<bool> paused)
        {
            _host = host;
            _paused = paused;
            pad.Down += OnFinger;
            pad.Drag += OnFinger;
            guide.enabled = ink.enabled = false;
            dot.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
            pad.gameObject.SetActive(false); // it would catch the fingers meant for the picture
        }

        /// <summary>
        /// Starts on a page already open in <paramref name="picture"/>. <paramref name="reveal"/>:
        /// at the end the drawing takes its own colours; otherwise only the lines come back.
        /// </summary>
        public bool Begin(ColoringCanvas picture, bool reveal)
        {
            Stop();
            _picture = picture;
            _page = picture.Page;
            if (_page == null || !_page.CanTrace) return false;
            _reveal = reveal;
            picture.interactive = false;
            picture.lines.color = new Color(1, 1, 1, linesWhileTracing);

            _path = new Vector2[_page.trace.Length / 2];
            for (int i = 0; i < _path.Length; i++) _path[i] = new Vector2(_page.trace[2 * i], _page.trace[2 * i + 1]);
            _reached = 0;
            _done = false;
            _chose = false;
            _startedAt = _lastProgress = Time.realtimeSinceStartup;

            DrawGuide();
            ClearInk();
            pad.gameObject.SetActive(true);
            dot.gameObject.SetActive(true);
            PlaceDot();
            dot.localScale = Vector3.one;
            dot.DOScale(1.2f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(this);
            hand.gameObject.SetActive(false);
            _hints = _host.StartCoroutine(Hints());
            return true;
        }

        /// <summary>Ends tracing at once and hides its layers (the picture stays as it is).</summary>
        public void Stop()
        {
            if (_hints != null && _host != null) _host.StopCoroutine(_hints);
            _hints = null;
            DOTween.Kill(this);
            if (_guideTex != null) UnityEngine.Object.Destroy(_guideTex);
            if (_inkTex != null) UnityEngine.Object.Destroy(_inkTex);
            _guideTex = _inkTex = null;
            // Not texture = null alone: a RawImage without a texture draws a white box.
            guide.texture = ink.texture = null;
            guide.enabled = ink.enabled = false;
            dot.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
            if (pad != null) pad.gameObject.SetActive(false);
            if (_picture != null && _picture.lines != null) _picture.lines.color = Color.white;
            _path = null;
            _picture = null;
        }

        // ─────────────────────────── the finger ───────────────────────────

        private void OnFinger(PointerEventData e)
        {
            if (_done || _path == null || Paused) return;
            var rt = _picture.paint.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
            var r = rt.rect;
            var f = new Vector2((local.x - r.xMin) / r.width * _page.width, (1f - (local.y - r.yMin) / r.height) * _page.height);

            // A child stuck for a while gets a wider path and a longer reach.
            bool help = Time.realtimeSinceStartup - _lastProgress > 12f;
            float tol = tolerance * (help ? 1.5f : 1f);
            int ahead = help ? lookAhead + 4 : lookAhead;
            float tol2 = tol * tol;
            int n = _path.Length;

            // Either way round from the start. Near the start the finger is close to both
            // sides, so wait until it has clearly gone one way.
            if (!_chose)
            {
                int fwd = Mathf.Max(0, Farthest(f, 1, ahead, tol2)), back = 0;
                for (int k = 1; k <= ahead; k++)
                    if ((_path[n - k] - f).sqrMagnitude <= tol2) back = k;
                if (Mathf.Abs(fwd - back) < 3) return;
                if (back > fwd) Array.Reverse(_path, 1, n - 1);
                _chose = true;
            }

            int to = Farthest(f, _reached + 1, _reached + ahead, tol2);
            if (to <= _reached) return;
            InkAlong(_reached, to);
            _reached = to;
            _lastProgress = Time.realtimeSinceStartup;
            hand.gameObject.SetActive(false);
            if (_reached >= n - 2) { InkAlong(_reached, n - 1); InkSegment(_path[n - 1], _path[0]); Finish(); }
            else PlaceDot();
        }

        /// <summary>The farthest path point in [from, to] within reach of the finger, or -1.</summary>
        private int Farthest(Vector2 f, int from, int to, float tol2)
        {
            int best = -1;
            to = Mathf.Min(to, _path.Length - 1);
            for (int i = from; i <= to; i++)
                if ((_path[i] - f).sqrMagnitude <= tol2) best = i;
            return best;
        }

        private void PlaceDot()
        {
            int i = Mathf.Min(_reached + 1, _path.Length - 1);
            dot.anchoredPosition = ToLocal(_path[i]);
        }

        private Vector2 ToLocal(Vector2 page)
        {
            var r = _picture.paint.rectTransform.rect;
            return new Vector2((page.x / _page.width - 0.5f) * r.width, (0.5f - page.y / _page.height) * r.height);
        }

        // The pointing hand slides along the next bit of the path: at the start, and after 5 s without progress.
        private IEnumerator Hints()
        {
            float wait = 0.8f;
            while (!_done)
            {
                float t = 0;
                while (t < wait) { if (!Paused) t += Time.unscaledDeltaTime; yield return null; }
                if (_done) yield break;
                bool idle = Time.realtimeSinceStartup - _lastProgress > 5f || !_chose;
                if (!idle || Paused) { wait = 1f; continue; }
                yield return ShowHand();
                wait = 2.5f;
            }
        }

        private IEnumerator ShowHand()
        {
            int a = Mathf.Min(_reached + 1, _path.Length - 1), b = Mathf.Min(_reached + 12, _path.Length - 1);
            var img = hand.GetComponent<Image>();
            hand.gameObject.SetActive(true);
            hand.anchoredPosition = ToLocal(_path[a]);
            img.color = new Color(1, 1, 1, 0);
            img.DOFade(1f, 0.2f).SetId(this);
            yield return new WaitForSecondsRealtime(0.25f);
            const float dur = 1.3f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                if (_done || _path == null || !hand.gameObject.activeSelf) yield break;
                float k = Mathf.SmoothStep(0, 1, t / dur);
                float idx = Mathf.Lerp(a, b, k);
                int i0 = Mathf.FloorToInt(idx), i1 = Mathf.Min(i0 + 1, _path.Length - 1);
                hand.anchoredPosition = ToLocal(Vector2.Lerp(_path[i0], _path[i1], idx - i0));
                yield return null;
            }
            img.DOFade(0f, 0.25f).SetId(this);
            yield return new WaitForSecondsRealtime(0.3f);
            hand.gameObject.SetActive(false);
        }

        // ─────────────────────────── drawing ───────────────────────────

        private void DrawGuide()
        {
            _tw = _page.width / Scale;
            _th = _page.height / Scale;
            _guideTex = NewTex("Trace guide");
            var px = new Color32[_tw * _th];
            Color32 c = guideColor;
            // Dots every third point (~42 px): reads as «follow me», and the crayon line covers them.
            for (int i = 0; i < _path.Length; i += 3) Segment(px, _path[i], _path[i], 7f, c);
            _guideTex.SetPixels32(px);
            _guideTex.Apply(false);
            guide.texture = _guideTex;
            guide.color = Color.white;
            guide.enabled = true;
        }

        private void ClearInk()
        {
            _inkTex = NewTex("Trace ink");
            _inkPx = new Color32[_tw * _th];
            _inkTex.SetPixels32(_inkPx);
            _inkTex.Apply(false);
            ink.texture = _inkTex;
            ink.color = Color.white;
            ink.enabled = true;
        }

        private void InkAlong(int from, int to)
        {
            for (int i = from; i < to; i++) Segment(_inkPx, _path[i], _path[i + 1], 11f, inkColor);
            _inkTex.SetPixels32(_inkPx);
            _inkTex.Apply(false);
        }

        private void InkSegment(Vector2 a, Vector2 b)
        {
            Segment(_inkPx, a, b, 11f, inkColor);
            _inkTex.SetPixels32(_inkPx);
            _inkTex.Apply(false);
        }

        private Texture2D NewTex(string name) => new(_tw, _th, TextureFormat.RGBA32, false)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };

        /// <summary>A round-capped line between two page points into a half-size buffer (y up), soft edged.</summary>
        private void Segment(Color32[] px, Vector2 a, Vector2 b, float radius, Color32 c)
        {
            Vector2 A = new(a.x / Scale, (_page.height - a.y) / Scale), B = new(b.x / Scale, (_page.height - b.y) / Scale);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, B.x) - radius - 1));
            int x1 = Mathf.Min(_tw - 1, Mathf.CeilToInt(Mathf.Max(A.x, B.x) + radius + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, B.y) - radius - 1));
            int y1 = Mathf.Min(_th - 1, Mathf.CeilToInt(Mathf.Max(A.y, B.y) + radius + 1));
            var ab = B - A;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(p - A, ab) / len2);
                float d = (p - (A + ab * t)).magnitude;
                float cover = Mathf.Clamp01(radius + 0.5f - d);
                if (cover <= 0) continue;
                int i = y * _tw + x;
                byte alpha = (byte)(c.a * cover);
                if (alpha > px[i].a) px[i] = new Color32(c.r, c.g, c.b, alpha);
            }
        }

        // ─────────────────────────── finished ───────────────────────────

        private void Finish()
        {
            _done = true;
            if (_hints != null) _host.StopCoroutine(_hints);
            _hints = null;
            DOTween.Kill(dot);
            dot.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
            guide.enabled = false;
            pad.gameObject.SetActive(false);
            // The crayon line gives way to the real outline; with a reveal the colours pour in from the end of the path.
            _picture.lines.DOFade(1f, 0.5f).SetId(this);
            ink.DOFade(0f, 0.9f).SetDelay(0.5f).SetId(this).OnComplete(() => ink.enabled = false);
            if (_reveal) _picture.Reveal(EndPixel, 1.1f);
            Finished?.Invoke();
        }
    }
}
