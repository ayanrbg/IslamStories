using System.Collections.Generic;
using DG.Tweening;
using FairyTales.Diagnostics;
using FairyTales.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// «Раскраски»: the gallery of pages (the child's own pictures on them once coloured)
    /// and the page itself — the picture on the left, the crayons on the right, the fill
    /// and crayon tools and «undo» above them. A finished page gets «Машаллах, как
    /// красиво!» with a sparkle burst. Pictures are kept between visits. Opened from the
    /// lessons home; built by ColoringBuilder (Nooria ▸ Lessons ▸ Build coloring).
    /// </summary>
    public class ColoringScreen : BaseScreen
    {
        [Header("Gallery")]
        public GameObject gallery;
        public Button home;
        public ScrollRect galleryScroll;
        public RectTransform cards;
        public ColoringCard cardTemplate;

        [Header("Page")]
        public GameObject board;
        public Button back;
        public ColoringCanvas canvas;
        public Button undo;
        public CanvasGroup undoGroup;
        public Button fillTool;
        public Button crayonTool;
        public Image fillToolFace;
        public Image crayonToolFace;
        public ColoringSwatch[] swatches = new ColoringSwatch[0];
        public ColoringSwatch magic;

        [Header("Trace first («Обведи и раскрась» from the home)")]
        public TraceSession tracer = new();
        /// <summary>The tools and crayons: hidden while the child traces.</summary>
        public CanvasGroup panel;

        [Header("Finished")]
        public RectTransform doneBanner;
        public TMP_Text doneText;
        public RectTransform sparkles;
        public Image sparkleTemplate;

        public Color toolOn = new(1f, 0.83f, 0.35f);
        public Color toolOff = Color.white;

        private ScreenManager _sm;
        private LessonVoice _voice;
        private readonly List<ColoringCard> _cards = new();
        private readonly List<Texture2D> _thumbs = new();
        private ColoringSwatch _selected;
        private bool _wasDone;
        private float _openedAt;
        /// <summary>Opened by the home's «Обведи» card: straight to a page, back goes home.</summary>
        private static bool _traceNext;
        private bool _fromHome;
        private const string TraceIndexKey = "coloring_trace_index";

        /// <summary>Simple shapes first, people last.</summary>
        private static readonly string[] TraceOrder =
            { "heart", "moon", "crown", "lantern", "gift", "mosque", "tv", "dua", "meal", "table", "camel", "family", "grandpa" };

        /// <summary>«Обведи»: the next page to trace and colour, one the child hasn't coloured yet if there is one.</summary>
        public static void OpenTrace(ScreenManager sm)
        {
            _traceNext = true;
            sm.Show<ColoringScreen>();
        }

        private static ColoringPage NextTracePage()
        {
            var pages = new List<ColoringPage>();
            foreach (var id in TraceOrder)
            {
                var p = ColoringPages.Find(id);
                if (p != null && p.CanTrace) pages.Add(p);
            }
            foreach (var p in ColoringPages.All)
                if (p.CanTrace && !pages.Contains(p)) pages.Add(p);
            if (pages.Count == 0) return null;
            int start = PlayerPrefs.GetInt(TraceIndexKey, 0);
            int pick = -1;
            for (int k = 0; k < pages.Count && pick < 0; k++)
            {
                int i = (start + k) % pages.Count;
                if (!System.IO.File.Exists(ColoringPages.PicturePath(pages[i].id))) pick = i;
            }
            if (pick < 0) pick = start % pages.Count;
            PlayerPrefs.SetInt(TraceIndexKey, pick + 1);
            PlayerPrefs.Save();
            return pages[pick];
        }
        private string Tween => "coloring_" + GetInstanceID();

        private void Awake()
        {
            _sm = GetComponentInParent<ScreenManager>();
            _voice = new LessonVoice(this);
            home.onClick.AddListener(() => _sm.Show<LessonHomeScreen>());
            back.onClick.AddListener(CloseBoard);
            undo.onClick.AddListener(() => canvas.Undo());
            fillTool.onClick.AddListener(() => SetTool(ColoringCanvas.Tool.Fill));
            crayonTool.onClick.AddListener(() => SetTool(ColoringCanvas.Tool.Crayon));
            foreach (var s in swatches)
            {
                var sw = s;
                sw.button.onClick.AddListener(() => Select(sw));
            }
            magic.button.onClick.AddListener(() => Select(magic));
            canvas.Changed += OnPictureChanged;
            tracer.Bind(this, () => false);
            tracer.Finished += OnTraced;
            cardTemplate.gameObject.SetActive(false);
            sparkleTemplate.gameObject.SetActive(false);
            doneBanner.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DOTween.Kill(Tween);
            ClearThumbs();
        }

        private void Update()
        {
            if (!IsVisible || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (board.activeSelf) CloseBoard();
            else _sm.Show<LessonHomeScreen>();
        }

        protected override void OnPrepare()
        {
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            board.SetActive(false);
            gallery.SetActive(true);
            _fromHome = false;
            if (_traceNext)
            {
                _traceNext = false;
                var page = NextTracePage();
                if (page != null) { _fromHome = true; OpenBoard(page, trace: true); return; }
            }
            DrawGallery();
        }

        protected override void OnShown() => Analytics.Log("coloring_view", ("pages", ColoringPages.All.Length));

        protected override void OnHidden()
        {
            tracer.Stop();
            Save();
            canvas.Close();
            board.SetActive(false);
            ClearThumbs();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && IsVisible) Save();
        }

        // ─────────────────────────── gallery ───────────────────────────

        private void DrawGallery()
        {
            var pages = ColoringPages.All;
            while (_cards.Count < pages.Length)
            {
                var c = Instantiate(cardTemplate, cards);
                c.gameObject.SetActive(true);
                _cards.Add(c);
            }
            ClearThumbs();
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool on = i < pages.Length;
                card.gameObject.SetActive(on);
                if (!on) continue;
                var page = pages[i];
                card.title.text = page.Title;
                card.lines.texture = ColoringPages.Lines(page);
                var thumb = ColoringPages.LoadThumb(page.id);
                if (thumb != null) _thumbs.Add(thumb);
                card.picture.texture = thumb;
                card.picture.enabled = thumb != null;
                card.button.onClick.RemoveAllListeners();
                card.button.onClick.AddListener(() => OpenBoard(page));
            }
            galleryScroll.StopMovement();
        }

        private void ClearThumbs()
        {
            foreach (var t in _thumbs) if (t != null) Destroy(t);
            _thumbs.Clear();
            foreach (var c in _cards) if (c != null) c.picture.texture = null;
        }

        // ─────────────────────────── the page ───────────────────────────

        private void OpenBoard(ColoringPage page) => OpenBoard(page, false);

        private void OpenBoard(ColoringPage page, bool trace)
        {
            if (!canvas.Open(page, ColoringPages.LoadPicture(page.id)))
            {
                Toast.Show(LessonStrings.T("coloring_broken"));
                return;
            }
            gallery.SetActive(false);
            board.SetActive(true);
            doneBanner.gameObject.SetActive(false);
            if (_selected == null) Select(swatches.Length > 0 ? swatches[0] : magic);
            else Select(_selected);
            SetTool(canvas.tool);
            _wasDone = canvas.Progress() >= 1f;
            _openedAt = Time.realtimeSinceStartup;
            RefreshUndo();
            canvas.interactive = true;
            SetPanel(true, false);
            if (trace && tracer.Begin(canvas, reveal: false)) SetPanel(false, false);
            Analytics.Log("coloring_open", ("page", page.id), ("started", !canvas.IsEmpty()), ("trace", trace));
        }

        private void SetPanel(bool on, bool animate)
        {
            panel.DOKill();
            panel.interactable = panel.blocksRaycasts = on;
            if (animate) panel.DOFade(on ? 1f : 0f, 0.35f);
            else panel.alpha = on ? 1f : 0f;
        }

        // Traced round: «Машаллах! Теперь раскрась» and the crayons come in.
        private void OnTraced()
        {
            Analytics.Log("trace_done", ("lesson", "practice"), ("page", canvas.Page.id), ("seconds", Mathf.RoundToInt(tracer.Seconds)));
            _voice.PlayBundled(LessonVoice.Correct);
            ShowBanner(LessonStrings.T("coloring_trace_done"));
            Sparkles.Burst(sparkles, sparkleTemplate, 18, Tween);
            canvas.interactive = true;
            SetPanel(true, true);
        }

        private void CloseBoard()
        {
            tracer.Stop();
            if (_fromHome)
            {
                // Came for «Обведи» from the home: back is back home (the picture is kept).
                _fromHome = false;
                Save();
                _sm.Show<LessonHomeScreen>();
                return;
            }
            Save();
            canvas.Close();
            DOTween.Kill(Tween);
            board.SetActive(false);
            gallery.SetActive(true);
            DrawGallery();
        }

        private void Save()
        {
            if (canvas.Page == null || !canvas.Dirty) return;
            var (png, thumb) = canvas.Encode();
            ColoringPages.SavePicture(canvas.Page.id, png, thumb);
        }

        private void SetTool(ColoringCanvas.Tool tool)
        {
            canvas.tool = tool;
            fillToolFace.color = tool == ColoringCanvas.Tool.Fill ? toolOn : toolOff;
            crayonToolFace.color = tool == ColoringCanvas.Tool.Crayon ? toolOn : toolOff;
            Pop(tool == ColoringCanvas.Tool.Fill ? fillTool.transform : crayonTool.transform);
        }

        private void Select(ColoringSwatch s)
        {
            if (_selected != null && _selected != s) _selected.SetSelected(false);
            _selected = s;
            s.SetSelected(true);
            canvas.magic = s == magic;
            if (s != magic) canvas.color = s.color;
        }

        private static void Pop(Transform t)
        {
            t.DOKill();
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.6f);
        }

        private void RefreshUndo()
        {
            undoGroup.alpha = canvas.CanUndo ? 1f : 0.4f;
            undo.interactable = canvas.CanUndo;
        }

        private void OnPictureChanged()
        {
            RefreshUndo();
            if (_wasDone || canvas.Progress() < 1f) return;
            _wasDone = true;
            Celebrate();
            Analytics.Log("coloring_complete", ("page", canvas.Page.id),
                ("seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - _openedAt)));
        }

        // «Машаллах, как красиво!» drops in over the picture with a burst of sparkles, then goes.
        private void Celebrate()
        {
            _voice.PlayBundled(LessonVoice.Correct);
            ShowBanner(LessonStrings.T("coloring_done"));
            Sparkles.Burst(sparkles, sparkleTemplate, 22, Tween);
        }

        // Drops in over the picture, stays a moment, goes.
        private void ShowBanner(string text)
        {
            DOTween.Kill(Tween);
            if (doneText != null) doneText.text = text;
            var b = doneBanner;
            b.gameObject.SetActive(true);
            b.localScale = Vector3.one * 0.6f;
            var group = b.GetComponent<CanvasGroup>();
            group.alpha = 0;
            DOTween.Sequence().SetId(Tween)
                .Append(b.DOScale(1f, 0.45f).SetEase(Ease.OutBack))
                .Join(group.DOFade(1f, 0.2f))
                .AppendInterval(2.2f)
                .Append(group.DOFade(0f, 0.35f))
                .OnComplete(() => b.gameObject.SetActive(false));
        }
    }
}
