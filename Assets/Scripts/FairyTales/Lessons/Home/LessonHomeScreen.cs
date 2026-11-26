using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// The app's home while lessons exist in the app language (Nooria 2.0, design 29
    /// «Урок дня + разделы»): the lesson of the day big on the left, the sections as
    /// sticker tiles on the right — all lessons, the tales library, the Tree, games.
    /// The child's avatar, stars and leaves on top; «Для родителей» behind the gate.
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
        public Button parents;

        [Header("Tiles")]
        public LessonTileView day;
        public LessonTileView all;
        public LessonTileView tales;
        public LessonTileView tree;
        public LessonTileView games;

        /// <summary>The catalog the home last drew (cached or fresh) — the lessons list reads it too.</summary>
        public static LessonCatalog Catalog { get; private set; }

        private static bool _registered;
        private ScreenManager _sm;
        private LessonSummary _day;
        private Sprite _dayArt;
        private bool _loading;

        private static bool Subscribed => IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;

        private void Awake()
        {
            _sm = GetComponentInParent<ScreenManager>();
            // Runs while ScreenManager warms its screens up, before it picks the first one.
            ScreenManager.Home = () => LessonLang.IsAvailable(Loc.Lang) ? typeof(LessonHomeScreen) : null;
            _dayArt = day.picture.sprite;

            day.button.onClick.AddListener(OnDay);
            all.button.onClick.AddListener(() => _sm.Show<LessonListScreen>());
            tales.button.onClick.AddListener(() => _sm.Show<LibraryScreen>());
            tree.button.onClick.AddListener(() => Toast.Show(LessonStrings.T("home_tree_soon")));
            games.button.onClick.AddListener(() => Toast.Show(LessonStrings.T("home_soon")));
            parents.onClick.AddListener(() => ChildGatePopup.Show(() => _sm.Show<PersonalizationScreen>()));
            avatarButton.onClick.AddListener(() => AvatarScreen.Open(AvatarPerson.Child));
            LessonProgressStore.Changed += OnProgressChanged;
        }

        private void OnDestroy() => LessonProgressStore.Changed -= OnProgressChanged;

        private void OnProgressChanged()
        {
            if (this == null || !IsVisible) return;
            RefreshStats();
            Render();
        }

        protected override void OnPrepare()
        {
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            all.SetLabel(LessonStrings.T("home_all"));
            tales.SetLabel(LessonStrings.T("home_tales"));
            tree.SetLabel(LessonStrings.T("home_tree"));
            games.SetLabel(LessonStrings.T("home_games"));
            games.SetBadge(LessonTileView.Badge.Lock);
            all.SetBadge(LessonTileView.Badge.None);
            tales.SetBadge(LessonTileView.Badge.None);
            tree.SetBadge(LessonTileView.Badge.None);
            RefreshStats();
            if (Catalog == null || Catalog.lang != LessonLang.ContentLang(Loc.Lang))
                Catalog = LessonHomeCache.LoadCatalog(Loc.Lang);
            Render();
        }

        protected override void OnShown()
        {
            Analytics.Log("lessons_home_view");
            if (!_loading) StartCoroutine(Load());
        }

        private void RefreshStats()
        {
            var p = LessonProgressStore.Data;
            stars.text = p.stars.ToString();
            leaves.text = p.LeafCount.ToString();
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
