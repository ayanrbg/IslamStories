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

        [Header("Finished")]
        public RectTransform doneBanner;
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
            DrawGallery();
        }

        protected override void OnShown() => Analytics.Log("coloring_view", ("pages", ColoringPages.All.Length));

        protected override void OnHidden()
        {
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

        private void OpenBoard(ColoringPage page)
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
            Analytics.Log("coloring_open", ("page", page.id), ("started", !canvas.IsEmpty()));
        }

        private void CloseBoard()
        {
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
            DOTween.Kill(Tween);
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

            for (int i = sparkles.childCount - 1; i >= 0; i--)
                if (sparkles.GetChild(i) != sparkleTemplate.transform) Destroy(sparkles.GetChild(i).gameObject);
            var colors = new[] { new Color(1f, 0.8f, 0.25f), new Color(1f, 0.55f, 0.7f), new Color(0.45f, 0.8f, 1f), new Color(0.6f, 0.9f, 0.5f) };
            var size = sparkles.rect.size;
            for (int i = 0; i < 22; i++)
            {
                var s = Instantiate(sparkleTemplate, sparkles);
                s.gameObject.SetActive(true);
                s.color = colors[i % colors.Length];
                var rt = s.rectTransform;
                rt.anchoredPosition = Vector2.zero;
                float ang = i / 22f * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                float dist = Random.Range(0.3f, 0.5f) * Mathf.Min(size.x, size.y) + Random.Range(0, 120f);
                var to = new Vector2(Mathf.Cos(ang) * dist * 1.3f, Mathf.Sin(ang) * dist);
                float sc = Random.Range(0.6f, 1.3f);
                rt.localScale = Vector3.zero;
                float delay = Random.Range(0f, 0.15f);
                DOTween.Sequence().SetId(Tween)
                    .AppendInterval(delay)
                    .Append(rt.DOAnchorPos(to, 0.9f).SetEase(Ease.OutCubic))
                    .Join(rt.DOScale(sc, 0.35f).SetEase(Ease.OutBack))
                    .Join(rt.DORotate(new Vector3(0, 0, Random.Range(-180f, 180f)), 0.9f))
                    .Append(s.DOFade(0f, 0.4f))
                    .OnComplete(() => { if (s != null) Destroy(s.gameObject); });
            }
        }
    }
}
