using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using FairyTales.Diagnostics;
using FairyTales.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// «Дерево добрых дел» (design 14 in the sticker style of the home). The tree grows
    /// with the leaves — a sprout, a sapling, a young tree, a big one — and the leaves
    /// sit on its crown, gold ones for the deeds a parent marked. On the right: how far
    /// to the next growth, the deeds waiting for the parent's tick («Отметить», behind
    /// the parental gate) and the leaves, newest first.
    ///
    /// Leaves earned since the last visit pop onto the tree. Opened from the home's
    /// tree tile and from the lesson reward («Посмотреть дерево»). Built by
    /// LessonHomeBuilder.
    /// </summary>
    public class LessonTreeScreen : BaseScreen
    {
        [Header("Top")]
        public Button home;
        public TMP_Text stars;
        public TMP_Text leaves;

        [Header("Tree")]
        /// <summary>From under the title to the bottom; the big tree fills its height.</summary>
        public RectTransform treeArea;
        /// <summary>Bottom-centre pivot on the ground line; sized from the stage sprite.</summary>
        public Image tree;
        /// <summary>Fills the tree picture: leaves are placed in its 0…1 space.</summary>
        public RectTransform leafLayer;
        /// <summary>Inactive; one clone per leaf on the crown.</summary>
        public Image leafImage;
        public Sprite[] stages = Array.Empty<Sprite>();
        public Sprite leafGreen;
        public Sprite leafGold;
        public float treeMaxHeight = 840f;

        [Header("Growth")]
        public Image growthIcon;
        public TMP_Text growthText;
        public RectTransform growthFill;

        [Header("List")]
        public ScrollRect scroll;
        public RectTransform content;
        public GameObject waitingHeader;
        public LessonTreeRow deedTemplate;
        public GameObject leavesHeader;
        public LessonTreeRow leafRowTemplate;
        public GameObject empty;

        private const string SeenKey = "lessons_tree_seen";
        private const int MaxRows = 40;

        /// <summary>Leaves needed for each stage: sprout, sapling, young tree, big tree.</summary>
        private static readonly int[] StageAt = { 0, 1, 3, 6 };
        private static readonly float[] StageHeight = { 0.36f, 0.52f, 0.74f, 1f };

        // Free spots on the crown, in the picture's 0…1 space (y down), spread out in
        // this order (farthest-point picks over the crown's pixels, clear of the fruit).
        private static readonly Vector2[] SpotsYoung =
        {
            new(0.508f, 0.262f), new(0.096f, 0.427f), new(0.836f, 0.480f), new(0.831f, 0.213f),
            new(0.232f, 0.160f), new(0.492f, 0.071f), new(0.605f, 0.431f), new(0.384f, 0.396f),
        };
        private static readonly Vector2[] SpotsBig =
        {
            new(0.494f, 0.254f), new(0.832f, 0.529f), new(0.258f, 0.573f), new(0.075f, 0.294f),
            new(0.848f, 0.183f), new(0.258f, 0.111f), new(0.537f, 0.477f), new(0.574f, 0.056f),
            new(0.736f, 0.354f), new(0.301f, 0.374f), new(0.113f, 0.449f), new(0.929f, 0.398f),
            new(0.430f, 0.131f), new(0.682f, 0.155f), new(0.671f, 0.569f), new(0.574f, 0.358f),
        };

        private ScreenManager _sm;
        private readonly List<Image> _leafPool = new();
        private readonly List<LessonTreeRow> _deedRows = new();
        private readonly List<LessonTreeRow> _leafRows = new();
        private readonly List<RectTransform> _spotRects = new();
        private string Tween => "lesson_tree_" + GetInstanceID();

        private void Awake()
        {
            _sm = GetComponentInParent<ScreenManager>();
            home.onClick.AddListener(() => _sm.Show<LessonHomeScreen>());
            leafImage.gameObject.SetActive(false);
            deedTemplate.gameObject.SetActive(false);
            leafRowTemplate.gameObject.SetActive(false);
            // The reward's «Посмотреть дерево» comes here.
            LessonPlayerScreen.OpenTree = () => _sm.Show<LessonTreeScreen>();
        }

        private void OnDestroy() => DOTween.Kill(Tween);

        // Android Back: to the lessons home.
        private void Update()
        {
            if (IsVisible && Input.GetKeyDown(KeyCode.Escape)) _sm.Show<LessonHomeScreen>();
        }

        protected override void OnPrepare()
        {
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            DOTween.Kill(Tween);
            // Leaves earned since the last visit start hidden and pop in once the screen is up.
            Draw(PlayerPrefs.GetInt(SeenKey, 0));
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnShown()
        {
            Analytics.Log("lessons_tree_view", ("leaves", LessonProgressStore.Data.LeafCount));
            PopNew();
        }

        // ─────────────────────────── drawing ───────────────────────────

        private static int StageOf(int leafCount)
        {
            int s = 0;
            for (int i = 0; i < StageAt.Length; i++) if (leafCount >= StageAt[i]) s = i;
            return s;
        }

        /// <param name="seen">Leaves already shown on an earlier visit; the rest start hidden and pop in on <see cref="PopNew"/>.</param>
        private void Draw(int seen)
        {
            var p = LessonProgressStore.Data;
            stars.text = p.stars.ToString();
            leaves.text = p.LeafCount.ToString();
            DrawTree(p, seen);
            DrawGrowth(p.LeafCount);
            DrawList(p);
        }

        private void DrawTree(LessonProgress p, int seen)
        {
            int count = p.LeafCount;
            int stage = Mathf.Min(StageOf(count), stages.Length - 1);
            var sprite = stages.Length > 0 ? stages[stage] : tree.sprite;
            tree.sprite = sprite;
            float maxH = treeArea != null && treeArea.rect.height > 100f ? Mathf.Min(treeMaxHeight, treeArea.rect.height) : treeMaxHeight;
            float h = maxH * StageHeight[Mathf.Min(stage, StageHeight.Length - 1)];
            tree.rectTransform.sizeDelta = new Vector2(h * sprite.rect.width / sprite.rect.height, h);
            tree.rectTransform.localScale = Vector3.one;

            // The leaves of the young and the big tree; the sprout and the sapling are their own leaves.
            var spots = stage >= 3 ? SpotsBig : stage == 2 ? SpotsYoung : Array.Empty<Vector2>();
            int shown = Mathf.Min(count, spots.Length);
            int first = count - shown; // the newest ones when there are more leaves than spots
            float size = h * (stage >= 3 ? 0.105f : 0.125f);
            _spotRects.Clear();
            for (int i = 0; i < Mathf.Max(shown, _leafPool.Count); i++)
            {
                if (i >= _leafPool.Count)
                {
                    var leaf = Instantiate(leafImage, leafLayer);
                    leaf.name = "Leaf " + i;
                    _leafPool.Add(leaf);
                }
                var img = _leafPool[i];
                bool on = i < shown;
                img.gameObject.SetActive(on);
                if (!on) continue;

                var record = p.leaves[first + i];
                img.sprite = record.kind == LeafKind.Lesson ? leafGreen : leafGold;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(spots[i].x, 1f - spots[i].y);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(size * img.sprite.rect.width / img.sprite.rect.height, size);
                // A steady tilt per spot, so leaves don't all point the same way.
                rt.localEulerAngles = new Vector3(0, 0, Mathf.Sin(i * 2.39f) * 34f);
                bool fresh = first + i >= seen;
                rt.localScale = fresh ? Vector3.zero : Vector3.one;
                _spotRects.Add(rt);
            }
        }

        private void DrawGrowth(int count)
        {
            int stage = StageOf(count);
            bool top = stage >= StageAt.Length - 1;
            int next = top ? stage : stage + 1;
            if (stages.Length > 0) growthIcon.sprite = stages[Mathf.Min(next, stages.Length - 1)];
            growthText.text = top
                ? LessonStrings.T("tree_big")
                : LessonStrings.T("tree_grow", LessonStrings.LeavesCount(StageAt[next] - count));
            float from = StageAt[stage], to = top ? from : StageAt[next];
            float value = top ? 1f : Mathf.Clamp01((count - from) / Mathf.Max(1f, to - from));
            var track = (RectTransform)growthFill.parent;
            float w = track.rect.width;
            growthFill.sizeDelta = new Vector2(Mathf.Max(growthFill.rect.height, w * value), growthFill.sizeDelta.y);
            growthFill.gameObject.SetActive(value > 0f);
        }

        private void DrawList(LessonProgress p)
        {
            var today = DateTime.Now;
            string todayKey = LessonProgress.Day(today), yesterdayKey = LessonProgress.Day(today.AddDays(-1));

            // Deeds waiting for the parent's tick.
            var deeds = p.pendingDeeds;
            waitingHeader.SetActive(deeds.Count > 0);
            waitingHeader.transform.SetAsLastSibling();
            Fill(_deedRows, deedTemplate, deeds.Count, (row, i) =>
            {
                var deed = deeds[deeds.Count - 1 - i];
                row.icon.sprite = leafGold;
                row.title.text = TaleText.Resolve(string.IsNullOrEmpty(deed.title) ? deed.leafLabel : deed.title);
                row.button.onClick.RemoveAllListeners();
                row.button.onClick.AddListener(() => OnMark(deed, row));
            });

            // The leaves, newest first.
            int n = Mathf.Min(p.leaves.Count, MaxRows);
            leavesHeader.SetActive(n > 0);
            leavesHeader.transform.SetAsLastSibling();
            Fill(_leafRows, leafRowTemplate, n, (row, i) =>
            {
                var leaf = p.leaves[p.leaves.Count - 1 - i];
                bool deed = leaf.kind != LeafKind.Lesson;
                row.icon.sprite = deed ? leafGold : leafGreen;
                row.title.text = TaleText.Resolve(leaf.label ?? "");
                bool fresh = !deed && leaf.date == todayKey;
                row.note.text = deed ? LessonStrings.T("leaf_mum")
                    : fresh ? LessonStrings.T("leaf_new")
                    : leaf.date == yesterdayKey ? LessonStrings.T("leaf_yesterday")
                    : ShortDate(leaf.date);
                row.note.color = fresh ? new Color(0.18f, 0.6f, 0.36f) : new Color(0.42f, 0.39f, 0.57f);
                row.note.fontStyle = fresh ? FontStyles.Bold : FontStyles.Normal;
            });

            empty.SetActive(p.leaves.Count == 0 && deeds.Count == 0);
            empty.transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void Fill(List<LessonTreeRow> rows, LessonTreeRow template, int count, Action<LessonTreeRow, int> bind)
        {
            while (rows.Count < count)
            {
                var row = Instantiate(template, content);
                row.name = template.name.Replace("Template", "") + rows.Count;
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < count;
                rows[i].gameObject.SetActive(on);
                if (!on) continue;
                rows[i].transform.SetAsLastSibling();
                bind(rows[i], i);
            }
        }

        private static string ShortDate(string day)
            => DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToString("dd.MM", CultureInfo.InvariantCulture)
                : day ?? "";

        // ─────────────────────────── new leaves ───────────────────────────

        /// <summary>Leaves earned since the last visit pop onto the crown one after another.</summary>
        private void PopNew()
        {
            int count = LessonProgressStore.Data.LeafCount;
            int seen = Mathf.Min(PlayerPrefs.GetInt(SeenKey, 0), count);
            PlayerPrefs.SetInt(SeenKey, count);
            PlayerPrefs.Save();

            int fresh = count - seen;
            if (fresh <= 0) { ShowAll(); return; }

            // The tree grew since last time: it springs up first.
            if (StageOf(seen) < StageOf(count))
            {
                tree.rectTransform.localScale = new Vector3(0.82f, 0.82f, 1f);
                tree.rectTransform.DOScale(1f, 0.6f).SetEase(Ease.OutBack).SetId(Tween);
            }
            float delay = 0.35f;
            for (int i = 0; i < _spotRects.Count; i++)
            {
                var rt = _spotRects[i];
                if (rt.localScale.x > 0.5f) continue;
                var angle = rt.localEulerAngles;
                rt.localEulerAngles = angle + new Vector3(0, 0, -40f);
                rt.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetDelay(delay).SetId(Tween);
                rt.DOLocalRotate(angle, 0.6f).SetEase(Ease.OutBack).SetDelay(delay).SetId(Tween);
                delay += 0.22f;
            }
        }

        private void ShowAll()
        {
            foreach (var rt in _spotRects) rt.localScale = Vector3.one;
        }

        // ─────────────────────────── parent's tick ───────────────────────────

        private void OnMark(PendingDeed deed, LessonTreeRow row)
        {
            // The tick is the parent's: behind the parental gate so the child can't set it.
            ChildGatePopup.Show(() =>
            {
                bool awarded = LessonProgressStore.MarkDeed(deed.lessonId, deed.leafLabel);
                Analytics.Log("lesson_deed_marked", ("lesson", deed.lessonId), ("where", "tree"));
                if (awarded) Toast.Show(LessonStrings.Leaves(1) + " · " + LessonStrings.Stars(1));
                Draw(PlayerPrefs.GetInt(SeenKey, 0));
                PopNew();
            });
        }
    }
}
