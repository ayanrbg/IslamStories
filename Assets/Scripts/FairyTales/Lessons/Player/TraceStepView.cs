using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// «Обведи»: a coloring page with its outline drawn as a dashed path. A green dot waits
    /// where to go next; the child runs a finger along the path and a thick crayon line
    /// follows — the PATH, never the raw finger, so the line comes out neat even from a
    /// wobbly hand. Wandering off loses nothing: the dot just waits. Once round, the drawing
    /// fills with its colours, «Машаллах, получилось!», and the next arrow shows.
    ///
    /// Made for small hands: a finger-wide tolerance, either direction from the start, a
    /// pointing hand that shows the way at the start and whenever the child stalls, and a
    /// wider tolerance after a while without progress.
    /// </summary>
    [Serializable]
    public class TraceStepView : LessonStepView
    {
        public TMP_Text title;
        public Button listen;
        /// <summary>Shows the page (input off); the reveal colours it.</summary>
        public ColoringCanvas picture;
        public RawImage guide;
        public RawImage ink;
        public TracePad pad;
        /// <summary>Children of the picture, placed in its local space.</summary>
        public RectTransform dot;
        public RectTransform hand;
        public RectTransform doneBanner;
        public RectTransform sparkles;
        public Image sparkleTemplate;
        public Button next;

        public Color guideColor = new(0.48f, 0.36f, 0.84f, 0.55f);
        public Color inkColor = new(0.88f, 0.34f, 0.48f, 1f);
        [Range(0.1f, 1f)] public float linesWhileTracing = 0.4f;

        /// <summary>How far from the path a finger still counts, page pixels (the page is 1280 wide).</summary>
        public float tolerance = 62f;
        /// <summary>How many points ahead a finger may jump (points are ~14 px apart): no cutting across.</summary>
        public int lookAhead = 7;

        private TraceStep _step;
        private ColoringPage _page;
        private Vector2[] _path;
        private int _reached;
        private bool _done, _chose;
        private float _lastProgress, _startedAt;
        private Texture2D _guideTex, _inkTex;
        private Color32[] _inkPx;
        private int _tw, _th;
        private const int Scale = 2; // guide and ink at half the page's resolution

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            listen.onClick.AddListener(() => { if (_step != null) P.Speak(NarrationKey.Trace(_step)); });
            next.onClick.AddListener(() => P.Next());
            pad.Down += OnFinger;
            pad.Drag += OnFinger;
            sparkleTemplate.gameObject.SetActive(false);
        }

        public override void Begin(LessonStep step)
        {
            _step = (TraceStep)step;
            _page = ColoringPages.Find(_step.page);
            if (_page == null || !_page.CanTrace || !picture.Open(_page, null))
            {
                Debug.LogWarning($"[Lesson] trace: no page \"{_step.page}\"");
                P.Next();
                return;
            }
            picture.interactive = false;
            picture.paint.raycastTarget = false;
            picture.lines.color = new Color(1, 1, 1, linesWhileTracing);

            _path = new Vector2[_page.trace.Length / 2];
            for (int i = 0; i < _path.Length; i++) _path[i] = new Vector2(_page.trace[2 * i], _page.trace[2 * i + 1]);
            _reached = 0;
            _done = false;
            _chose = false;
            _startedAt = _lastProgress = Time.realtimeSinceStartup;

            title.text = P.Text(_step.text, highlightName: true);
            title.alpha = 1f;
            LessonPlayerScreen.SetShown(next, false);
            doneBanner.gameObject.SetActive(false);
            DrawGuide();
            ClearInk();
            dot.gameObject.SetActive(true);
            PlaceDot();
            dot.localScale = Vector3.one;
            dot.DOScale(1.2f, 0.55f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(P.TweenId);
            hand.gameObject.SetActive(false);
            P.StepRoutine(Hints());
            P.Speak(NarrationKey.Trace(_step));
        }

        public override void End()
        {
            picture.Close();
            picture.lines.color = Color.white;
            if (_guideTex != null) UnityEngine.Object.Destroy(_guideTex);
            if (_inkTex != null) UnityEngine.Object.Destroy(_inkTex);
            _guideTex = _inkTex = null;
            guide.texture = ink.texture = null;
            guide.enabled = ink.enabled = false;
            _step = null;
        }

        // ─────────────────────────── the finger ───────────────────────────

        private void OnFinger(PointerEventData e)
        {
            if (_done || _path == null || P.Paused) return;
            var rt = picture.paint.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
            var r = rt.rect;
            var f = new Vector2((local.x - r.xMin) / r.width * _page.width, (1f - (local.y - r.yMin) / r.height) * _page.height);

            // A child stuck for a while gets a wider path and a longer reach.
            bool help = Time.realtimeSinceStartup - _lastProgress > 12f;
            float tol = tolerance * (help ? 1.5f : 1f);
            int ahead = help ? lookAhead + 4 : lookAhead;
            float tol2 = tol * tol;
            int n = _path.Length;

            // The first move decides the direction: round either way from the start.
            // Near the start the finger is close to both sides, so wait until it has clearly gone one way.
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
            var r = picture.paint.rectTransform.rect;
            return new Vector2((page.x / _page.width - 0.5f) * r.width, (0.5f - page.y / _page.height) * r.height);
        }

        // The pointing hand slides along the next bit of the path: at the start, and after 5 s without progress.
        private IEnumerator Hints()
        {
            float wait = 0.8f;
            while (!_done)
            {
                float t = 0;
                while (t < wait) { if (!P.Paused) t += Time.unscaledDeltaTime; yield return null; }
                if (_done) yield break;
                bool idle = Time.realtimeSinceStartup - _lastProgress > 5f || !_chose;
                if (!idle || P.Paused) { wait = 1f; continue; }
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
            img.DOFade(1f, 0.2f).SetId(P.TweenId);
            yield return new WaitForSecondsRealtime(0.25f);
            const float dur = 1.3f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                if (_done || !hand.gameObject.activeSelf) yield break;
                float k = Mathf.SmoothStep(0, 1, t / dur);
                float idx = Mathf.Lerp(a, b, k);
                int i0 = Mathf.FloorToInt(idx), i1 = Mathf.Min(i0 + 1, _path.Length - 1);
                hand.anchoredPosition = ToLocal(Vector2.Lerp(_path[i0], _path[i1], idx - i0));
                yield return null;
            }
            img.DOFade(0f, 0.25f).SetId(P.TweenId);
            yield return new WaitForSecondsRealtime(0.3f);
            hand.gameObject.SetActive(false);
        }

        // ─────────────────────────── drawing ───────────────────────────

        private void DrawGuide()
        {
            _tw = _page.width / Scale;
            _th = _page.height / Scale;
            if (_guideTex == null || _guideTex.width != _tw || _guideTex.height != _th)
            {
                if (_guideTex != null) UnityEngine.Object.Destroy(_guideTex);
                _guideTex = NewTex("Trace guide");
            }
            var px = new Color32[_tw * _th];
            Color32 c = guideColor;
            // Dots every third point (~42 px): reads as «follow me», and the crayon line covers them.
            for (int i = 0; i < _path.Length; i += 3) Segment(px, _path[i], _path[i], 7f, c);
            _guideTex.SetPixels32(px);
            _guideTex.Apply(false);
            guide.texture = _guideTex;
            guide.enabled = true;
        }

        private void ClearInk()
        {
            if (_inkTex == null || _inkTex.width != _tw || _inkTex.height != _th)
            {
                if (_inkTex != null) UnityEngine.Object.Destroy(_inkTex);
                _inkTex = NewTex("Trace ink");
            }
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
            DOTween.Kill(dot);
            dot.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
            // Not texture = null: a RawImage without a texture draws a white box over the picture.
            guide.enabled = false;
            Diagnostics.Analytics.Log("trace_done", ("lesson", P.Lesson.id), ("page", _page.id),
                ("seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - _startedAt)));

            // The crayon line gives way to the real outline, and the colours pour in from the end of the path.
            var end = _path[_path.Length - 1];
            picture.lines.DOFade(1f, 0.5f).SetId(P.TweenId);
            ink.DOFade(0f, 0.9f).SetDelay(0.5f).SetId(P.TweenId);
            picture.Reveal(new Vector2(end.x, _page.height - end.y), 1.1f);
            P.Voice.PlayBundled(LessonVoice.Correct);
            Banner();
            Burst();
            P.ShowAfter(next, 1.4f);
        }

        // «Машаллах, получилось!» takes the task line's place.
        private void Banner()
        {
            title.DOFade(0f, 0.2f).SetId(P.TweenId);
            var b = doneBanner;
            b.gameObject.SetActive(true);
            b.localScale = Vector3.one * 0.6f;
            b.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetId(P.TweenId);
        }

        private void Burst()
        {
            for (int i = sparkles.childCount - 1; i >= 0; i--)
                if (sparkles.GetChild(i) != sparkleTemplate.transform) UnityEngine.Object.Destroy(sparkles.GetChild(i).gameObject);
            var colors = new[] { new Color(1f, 0.8f, 0.25f), new Color(1f, 0.55f, 0.7f), new Color(0.45f, 0.8f, 1f), new Color(0.6f, 0.9f, 0.5f) };
            var size = sparkles.rect.size;
            for (int i = 0; i < 18; i++)
            {
                var s = UnityEngine.Object.Instantiate(sparkleTemplate, sparkles);
                s.gameObject.SetActive(true);
                s.color = colors[i % colors.Length];
                var rt = s.rectTransform;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.zero;
                float ang = i / 18f * Mathf.PI * 2f + UnityEngine.Random.Range(-0.15f, 0.15f);
                float dist = UnityEngine.Random.Range(0.35f, 0.55f) * Mathf.Min(size.x, size.y);
                var to = new Vector2(Mathf.Cos(ang) * dist * 1.3f, Mathf.Sin(ang) * dist);
                DOTween.Sequence().SetId(P.TweenId)
                    .AppendInterval(UnityEngine.Random.Range(0f, 0.15f))
                    .Append(rt.DOAnchorPos(to, 0.9f).SetEase(Ease.OutCubic))
                    .Join(rt.DOScale(UnityEngine.Random.Range(0.6f, 1.3f), 0.35f).SetEase(Ease.OutBack))
                    .Append(s.DOFade(0f, 0.4f))
                    .OnComplete(() => { if (s != null) UnityEngine.Object.Destroy(s.gameObject); });
            }
        }
    }
}
