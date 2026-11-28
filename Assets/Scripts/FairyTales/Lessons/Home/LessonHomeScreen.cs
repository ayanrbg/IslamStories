using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using FairyTales.Api;
using FairyTales.Diagnostics;
using FairyTales.IAP;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using FairyTales.UI.Onboarding;
using FairyTales.UI.Payment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// The app's home while lessons exist in the app language, laid out like Speech Blubs'
    /// home: a page that scrolls down through rows of sticker cards that scroll sideways.
    ///   • «Адиль, это для тебя!» — the lesson of the day with the child themself on the card
    ///     (their own head on the drawn body), then «Обведи», coloring, tales, the Tree;
    ///   • «Моя семья» — the child and mum as their avatars, «Изменить»;
    ///   • a row per topic with its lessons (tick, lock, ▶).
    /// On top: the child's avatar, stars, leaves, the streak, «Открыть все уроки» for those
    /// without a subscription, «Для родителей» behind the gate.
    ///
    /// ScreenManager opens it instead of the library (<see cref="ScreenManager.Home"/>,
    /// set here), and the library's home button comes back to it. The layout is built
    /// in code by LessonHomeBuilder (Nooria ▸ Lessons ▸ Build lessons home).
    /// </summary>
    public class LessonHomeScreen : BaseScreen
    {
        [Header("Top")]
        public AvatarView avatar;
        public Button avatarButton;
        public TMP_Text stars;
        public TMP_Text leaves;
        /// <summary>Days in a row with a lesson; hidden while there is no streak.</summary>
        public GameObject streakChip;
        public TMP_Text streak;
        public Button parents;
        /// <summary>«Открыть все уроки» → the paywall; hidden for subscribers.</summary>
        public Button unlock;

        [Header("Picks")]
        public ScrollRect page;
        public TMP_Text picksTitle;
        public LessonTileView day;
        public LessonTileView trace;
        public LessonTileView coloring;
        public LessonTileView tales;
        public LessonTileView tree;
        /// <summary>«Ждёт отметки: N» on the tree tile while deeds wait for the parent's tick.</summary>
        public GameObject deedsChip;
        public TMP_Text deedsChipText;

        [Header("Family")]
        public Button familyEdit;
        public LessonTileView childCard;
        public LessonTileView momCard;
        public LessonTileView dadCard;

        [Header("Topics")]
        /// <summary>Parent of the rows (the page's content); topic rows are added after the family.</summary>
        public RectTransform sections;
        /// <summary>Inactive; one clone per topic.</summary>
        public LessonHomeSection topicTemplate;
        /// <summary>Topic pictures, picked by a word in the topic id (topic_food for "food"…); the last one for the rest.</summary>
        public Sprite[] topicIcons = Array.Empty<Sprite>();

        [Header("Avatar offer")]
        /// <summary>«Сделаем тебя героем!» — once, on the first visit, while the child has no face of their own.</summary>
        public GameObject avatarOffer;
        public RectTransform avatarOfferCard;
        public Button avatarOfferGo;
        public Button avatarOfferLater;

        /// <summary>The catalog the home last drew (cached or fresh) — the lessons list reads it too.</summary>
        public static LessonCatalog Catalog { get; private set; }

        private static bool _registered;
        private ScreenManager _sm;
        private LessonSummary _day;
        private Sprite _dayArt;
        private Sprite _coverPlaceholder;
        private readonly List<LessonHomeSection> _topics = new();
        private bool _loading;
        private const string AvatarOfferKey = "lessons_avatar_offered";

        private static bool Subscribed => IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;

        private void Awake()
        {
            _sm = GetComponentInParent<ScreenManager>();
            // Runs while ScreenManager warms its screens up, before it picks the first one.
            ScreenManager.Home = () => LessonLang.IsAvailable(Loc.Lang) ? typeof(LessonHomeScreen) : null;
            _dayArt = day.picture.sprite;

            _coverPlaceholder = topicTemplate.tileTemplate.picture.sprite;
            topicTemplate.gameObject.SetActive(false);
            day.button.onClick.AddListener(OnDay);
            trace.button.onClick.AddListener(() => ColoringScreen.OpenTrace(_sm));
            coloring.button.onClick.AddListener(() => _sm.Show<ColoringScreen>());
            tales.button.onClick.AddListener(() => _sm.Show<LibraryScreen>());
            tree.button.onClick.AddListener(() => _sm.Show<LessonTreeScreen>());
            unlock.onClick.AddListener(() => PaymentScreen.Open(_sm, typeof(LessonHomeScreen), source: "home_unlock"));
            familyEdit.onClick.AddListener(() => AvatarScreen.Open(AvatarPerson.Child));
            childCard.button.onClick.AddListener(() => AvatarScreen.Open(AvatarPerson.Child));
            momCard.button.onClick.AddListener(() => AvatarScreen.Open(AvatarPerson.Mom));
            dadCard.button.onClick.AddListener(() => Toast.Show(LessonStrings.T("home_soon")));
            parents.onClick.AddListener(() => ChildGatePopup.Show(() => _sm.Show<PersonalizationScreen>()));
            avatarButton.onClick.AddListener(() => AvatarScreen.Open(AvatarPerson.Child));
            avatarOfferGo.onClick.AddListener(() => { HideOffer(); AvatarScreen.Open(AvatarPerson.Child); });
            avatarOfferLater.onClick.AddListener(HideOffer);
            avatarOffer.SetActive(false);
            LessonProgressStore.Changed += OnProgressChanged;
        }

        private void OnDestroy()
        {
            LessonProgressStore.Changed -= OnProgressChanged;
            DOTween.Kill(this);
            DOTween.Kill(Life);
        }

        private void OnProgressChanged()
        {
            if (this == null || !IsVisible) return;
            RefreshStats();
            Render();
        }

        protected override void OnPrepare()
        {
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            var name = TaleText.ChildName;
            picksTitle.text = string.IsNullOrWhiteSpace(name)
                ? LessonStrings.T("home_picks_you")
                : LessonStrings.T("home_picks", name.Trim());
            foreach (var (tile, key) in new[] { (trace, "home_trace"), (coloring, "home_coloring"), (tales, "home_tales"), (tree, "home_tree") })
            {
                tile.SetLabel(LessonStrings.T(key));
                tile.SetBadge(LessonTileView.Badge.None);
            }
            childCard.SetLabel(string.IsNullOrWhiteSpace(name) ? LessonStrings.T("home_me") : name.Trim());
            momCard.SetLabel(LessonStrings.T("home_mom"));
            dadCard.SetLabel(LessonStrings.T("home_dad"));
            childCard.SetBadge(LessonTileView.Badge.None);
            momCard.SetBadge(LessonTileView.Badge.None);
            dadCard.SetBadge(LessonTileView.Badge.Lock);
            unlock.transform.parent.gameObject.SetActive(!Subscribed);
            page.StopMovement();
            page.verticalNormalizedPosition = 1f;
            // Small already while the screen slides in; they pop up once it is shown.
            DOTween.Kill(Life);
            foreach (var card in PopCards()) card.localScale = Vector3.one * PopFrom;
            RefreshStats();
            if (Catalog == null || Catalog.lang != LessonLang.ContentLang(Loc.Lang))
                Catalog = LessonHomeCache.LoadCatalog(Loc.Lang);
            Render();
        }

        protected override void OnShown()
        {
            Analytics.Log("lessons_home_view");
            Animate();
            if (!_loading) StartCoroutine(Load());
            MaybeOfferAvatar();
        }

        protected override void OnHidden()
        {
            avatarOffer.SetActive(false);
            DOTween.Kill(Life);
        }

        private void RefreshStats()
        {
            var p = LessonProgressStore.Data;
            stars.text = p.stars.ToString();
            leaves.text = p.LeafCount.ToString();
            int days = LessonProgressStore.StreakDays;
            if (streakChip != null) streakChip.SetActive(days > 0);
            if (streak != null) streak.text = days.ToString();
            int waiting = p.pendingDeeds.Count;
            deedsChip.SetActive(waiting > 0);
            if (waiting > 0) deedsChipText.text = LessonStrings.T("home_deeds", waiting);
        }

        // ─────────────────────────── avatar offer ───────────────────────────

        private static bool HasOwnFace
        {
            get
            {
                var kind = AvatarStore.Get(AvatarPerson.Child).kind;
                return kind == AvatarStore.KindFace || kind == AvatarStore.KindPhoto;
            }
        }

        private void MaybeOfferAvatar()
        {
            if (PlayerPrefs.GetInt(AvatarOfferKey, 0) == 1 || HasOwnFace) return;
            PlayerPrefs.SetInt(AvatarOfferKey, 1);
            PlayerPrefs.Save();
            Analytics.Log("lessons_avatar_offer");

            avatarOffer.SetActive(true);
            var group = avatarOffer.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.DOFade(1f, 0.3f).SetDelay(0.5f).SetId(this);
            avatarOfferCard.localScale = new Vector3(0.7f, 0.7f, 1f);
            avatarOfferCard.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetDelay(0.5f).SetId(this);
        }

        private void HideOffer()
        {
            DOTween.Kill(this);
            var group = avatarOffer.GetComponent<CanvasGroup>();
            group.DOFade(0f, 0.2f).SetId(this).OnComplete(() => avatarOffer.SetActive(false));
        }

        // ─────────────────────────── data ───────────────────────────

        /// <summary>Registers with the server once per launch (the library does the same; the second call is harmless).</summary>
        public static IEnumerator EnsureRegistered()
        {
            if (_registered) yield break;
            var api = FindAnyObjectByType<ApiClient>();
            if (api == null) yield break;
            yield return new AuthService(api).Register(AuthService.GetOrCreateUserId(),
                PlayerPrefs.GetString("ft_childName", ""), PlayerPrefs.GetString("ft_gender", "male"),
                PlayerPrefs.GetString("ft_lang", "ru"),
                onSuccess: _ => _registered = true, onError: _ => _registered = true);
            if (IAPManager.Instance != null) IAPManager.Instance.RefreshEntitlement();
        }

        private IEnumerator Load()
        {
            _loading = true;
            yield return LessonCache.EnsureBundledInstalled();
            yield return EnsureRegistered();

            var api = FindAnyObjectByType<ApiClient>();
            var lang = Loc.Lang;
            if (api != null)
                yield return new LessonService(api).GetCatalog(lang, catalog =>
                {
                    if (Loc.Lang != lang) return;
                    Catalog = catalog;
                    LessonHomeCache.SaveCatalog(lang, catalog);
                });
            _loading = false;
            Render();
        }

        /// <summary>Published lessons in course order (topic, then lesson), coming-soon ones last.</summary>
        public static List<LessonSummary> Ordered(LessonCatalog catalog)
        {
            if (catalog?.lessons == null) return new List<LessonSummary>();
            var topicOrder = (catalog.topics ?? Array.Empty<LessonTopic>())
                .Where(t => t != null && t.id != null)
                .GroupBy(t => t.id).ToDictionary(g => g.Key, g => g.First().sortOrder);
            return catalog.lessons.Where(l => l != null && !string.IsNullOrEmpty(l.id))
                .OrderBy(l => l.IsComingSoon ? 1 : 0)
                .ThenBy(l => l.topic != null && topicOrder.TryGetValue(l.topic, out var o) ? o : int.MaxValue)
                .ThenBy(l => l.sortOrder)
                .ToList();
        }

        /// <summary>The lesson to offer today: the one left halfway, else the first not done yet, else the first.</summary>
        public static LessonSummary PickDay(LessonCatalog catalog)
        {
            var list = Ordered(catalog).Where(l => !l.IsComingSoon).ToList();
            if (list.Count == 0) return null;
            var progress = LessonProgressStore.Data;
            var unfinished = progress.unfinished?.lessonId;
            return list.FirstOrDefault(l => l.id == unfinished)
                   ?? list.FirstOrDefault(l => !progress.IsCompleted(l.id))
                   ?? list[0];
        }

        // ─────────────────────────── drawing ───────────────────────────

        private void Render()
        {
            RenderTopics();
            _day = PickDay(Catalog);
            if (_day == null)
            {
                day.SetLabel(LessonStrings.T("home_lesson_day"));
                day.picture.sprite = _dayArt;
                day.SetBadge(LessonTileView.Badge.Play);
                return;
            }

            day.SetLabel(_day.title);
            day.SetBadge(_day.IsLocked(Subscribed) ? LessonTileView.Badge.Lock : LessonTileView.Badge.Play);
            var now = LessonHomeCache.CoverNow(_day.coverUrl);
            day.SetPicture(now != null ? now : _dayArt);
            if (now == null && !string.IsNullOrEmpty(_day.coverUrl) && isActiveAndEnabled)
            {
                var want = _day;
                StartCoroutine(LessonHomeCache.Cover(want.coverUrl, s => { if (s != null && _day == want) day.SetPicture(s); }));
            }
        }

        /// <summary>A row per topic in course order; lessons without a topic share a «Уроки» row.</summary>
        private void RenderTopics()
        {
            var lessons = Ordered(Catalog);
            var topics = (Catalog?.topics ?? Array.Empty<LessonTopic>()).Where(t => t != null && t.id != null)
                .GroupBy(t => t.id).Select(g => g.First()).ToDictionary(t => t.id);
            var groups = new List<(string title, List<LessonSummary> items)>();
            foreach (var l in lessons)
            {
                var title = l.topic != null && topics.TryGetValue(l.topic, out var t) && !string.IsNullOrEmpty(t.title)
                    ? t.title : LessonStrings.T("home_lessons");
                int at = groups.FindIndex(x => x.title == title);
                if (at < 0) groups.Add((title, new List<LessonSummary> { l }));
                else groups[at].items.Add(l);
            }

            var today = PickDay(Catalog);
            var progress = LessonProgressStore.Data;
            while (_topics.Count < groups.Count)
            {
                var sec = Instantiate(topicTemplate, sections);
                sec.tileTemplate.gameObject.SetActive(false);
                _topics.Add(sec);
            }
            for (int i = 0; i < _topics.Count; i++)
            {
                var sec = _topics[i];
                bool on = i < groups.Count;
                sec.gameObject.SetActive(on);
                if (!on) continue;
                sec.name = "Topic " + groups[i].title;
                sec.title.text = groups[i].title;
                if (sec.icon != null) sec.icon.sprite = TopicIcon(groups[i].items[0].topic);
                var items = groups[i].items;
                var tiles = sec.row.GetComponentsInChildren<LessonTileView>(true).Where(x => x != sec.tileTemplate).ToList();
                while (tiles.Count < items.Count) tiles.Add(Instantiate(sec.tileTemplate, sec.row));
                for (int k = 0; k < tiles.Count; k++)
                {
                    var tile = tiles[k];
                    bool show = k < items.Count;
                    tile.gameObject.SetActive(show);
                    if (!show) continue;
                    var lesson = items[k];
                    tile.SetLabel(lesson.title);
                    tile.SetBadge(lesson.IsComingSoon || lesson.IsLocked(Subscribed) ? LessonTileView.Badge.Lock
                        : progress.IsCompleted(lesson.id) ? LessonTileView.Badge.Done
                        : lesson == today ? LessonTileView.Badge.Play
                        : LessonTileView.Badge.None);
                    var group = tile.GetComponent<CanvasGroup>();
                    if (group != null) group.alpha = lesson.IsComingSoon ? 0.6f : 1f;
                    var now = LessonHomeCache.CoverNow(lesson.coverUrl);
                    tile.SetPicture(now != null ? now : _coverPlaceholder);
                    if (now == null && !string.IsNullOrEmpty(lesson.coverUrl) && isActiveAndEnabled)
                        StartCoroutine(LessonHomeCache.Cover(lesson.coverUrl, sp => { if (sp != null && tile != null) tile.SetPicture(sp); }));
                    tile.button.onClick.RemoveAllListeners();
                    tile.button.onClick.AddListener(() => OpenLesson(_sm, lesson, Catalog));
                }
            }
        }

        private static readonly (string word, string sprite)[] TopicWords =
        {
            ("food", "topic_food"), ("eat", "topic_food"), ("meal", "topic_food"), ("manner", "topic_kindness"),
            ("polite", "topic_kindness"), ("kind", "topic_kindness"), ("greet", "topic_kindness"), ("salam", "topic_kindness"),
            ("dua", "topic_dua"), ("prayer", "topic_dua"), ("namaz", "topic_dua"), ("mosque", "topic_mosque"),
            ("family", "topic_family"), ("mom", "topic_family"), ("sleep", "topic_sleep"), ("night", "topic_sleep"),
            ("animal", "topic_animals"),
        };

        private Sprite TopicIcon(string topicId)
        {
            if (topicIcons == null || topicIcons.Length == 0) return null;
            var id = (topicId ?? "").ToLowerInvariant();
            foreach (var (word, sprite) in TopicWords)
                if (id.Contains(word))
                {
                    var hit = topicIcons.FirstOrDefault(x => x != null && x.name == sprite);
                    if (hit != null) return hit;
                }
            return topicIcons[^1];
        }

        // ─────────────────────────── life ───────────────────────────

        /// <summary>
        /// The small motion that makes the home feel alive: the cards pop in one after another,
        /// the child on the lesson card bobs, its sticker wiggles, ▶ breathes, sparkles twinkle.
        /// </summary>
        private void Animate()
        {
            DOTween.Kill(Life);
            float delay = 0.05f;
            foreach (var card in PopCards())
            {
                card.localScale = Vector3.one * PopFrom;
                card.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(delay).SetId(Life);
                delay += 0.06f;
            }

            var child = day.transform.Find("Face/Child");
            if (child != null)
            {
                var rt = (RectTransform)child;
                var y = rt.anchoredPosition.y;
                rt.DOAnchorPosY(y + 9f, 1.1f).From(new Vector2(rt.anchoredPosition.x, y)).SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine).SetId(Life);
            }
            var badge = day.transform.Find("DayBadge");
            if (badge != null)
                badge.DOLocalRotate(new Vector3(0, 0, 2f), 0.9f).From(new Vector3(0, 0, 11f)).SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine).SetId(Life);
            if (day.play != null)
            {
                var play = day.play.transform;
                play.localScale = Vector3.one;
                play.DOScale(1.08f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(Life);
            }

            var rnd = new System.Random(3);
            foreach (var img in GetComponentsInChildren<Image>())
            {
                if (img.name != "Sparkle") continue;
                var t = img.transform;
                t.localScale = Vector3.one;
                t.DOScale(0.55f, 0.8f + (float)rnd.NextDouble() * 0.8f).SetDelay((float)rnd.NextDouble() * 1.2f)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(Life);
            }
        }

        private string Life => "lesson_home_life_" + GetInstanceID();
        private const float PopFrom = 0.86f;

        private IEnumerable<Transform> PopCards()
        {
            foreach (var row in new[] { day.transform.parent, childCard.transform.parent })
            {
                if (row == null) continue;
                for (int i = 0; i < row.childCount; i++)
                    if (row.GetChild(i).gameObject.activeSelf) yield return row.GetChild(i);
            }
        }

        // ─────────────────────────── opening ───────────────────────────

        private void OnDay()
        {
            if (_day != null) { OpenLesson(_sm, _day, Catalog); return; }

            // No catalog yet (first launch offline): the lesson shipped with the app.
            var bundled = LessonCache.BundledIds.FirstOrDefault();
            if (bundled != null)
                LessonPlayerScreen.Open(new LessonLaunch { Id = bundled, Subscribed = Subscribed });
            else
            {
                Toast.Show(LessonStrings.T("home_no_lessons"));
                if (!_loading) StartCoroutine(Load());
            }
        }

        /// <summary>Opens a lesson from the home or the list: the paywall when locked, «soon» when not out yet.</summary>
        public static void OpenLesson(ScreenManager sm, LessonSummary lesson, LessonCatalog catalog)
        {
            if (lesson == null || sm == null) return;
            if (lesson.IsComingSoon) { Toast.Show(LessonStrings.T("home_soon")); return; }
            if (lesson.IsLocked(Subscribed))
            {
                PaymentScreen.Open(sm, sm.Current != null ? sm.Current.GetType() : typeof(LessonHomeScreen), source: "lesson");
                return;
            }

            var list = Ordered(catalog).Where(l => !l.IsComingSoon).ToList();
            int i = list.FindIndex(l => l.id == lesson.id);
            Analytics.Log("lesson_open", ("lesson", lesson.id));
            LessonPlayerScreen.Open(new LessonLaunch
            {
                Id = lesson.id,
                KnownVersion = lesson.contentVersion,
                Title = lesson.title,
                Cover = LessonHomeCache.CoverNow(lesson.coverUrl),
                Next = i >= 0 && i + 1 < list.Count ? list[i + 1] : null,
                Subscribed = Subscribed,
            });
        }
    }
}
