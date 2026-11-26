using System.Collections.Generic;
using FairyTales.IAP;
using FairyTales.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// «Все уроки» (design 30): every lesson as a sticker tile in course order — done
    /// ones with a tick, locked ones with a lock, the next one to play with ▶. The
    /// house button goes back to the lessons home. Built by LessonHomeBuilder.
    /// </summary>
    public class LessonListScreen : BaseScreen
    {
        public Button home;
        public TMP_Text stars;
        public ScrollRect scroll;
        public RectTransform content;
        /// <summary>Inactive template; one clone per lesson.</summary>
        public LessonTileView template;
        /// <summary>Shown when there is no catalog yet (first launch offline).</summary>
        public GameObject empty;

        private readonly List<LessonTileView> _tiles = new();
        private ScreenManager _sm;
        private Sprite _placeholder;

        private void Awake()
        {
            _sm = GetComponentInParent<ScreenManager>();
            _placeholder = template.picture.sprite;
            template.gameObject.SetActive(false);
            home.onClick.AddListener(() => _sm.Show<LessonHomeScreen>());
        }

        // Android Back: to the lessons home.
        private void Update()
        {
            if (IsVisible && Input.GetKeyDown(KeyCode.Escape)) _sm.Show<LessonHomeScreen>();
        }

        protected override void OnPrepare()
        {
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            stars.text = LessonProgressStore.Data.stars.ToString();
            Build();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void Build()
        {
            var catalog = LessonHomeScreen.Catalog ?? LessonHomeCache.LoadCatalog(Loc.Lang);
            var lessons = LessonHomeScreen.Ordered(catalog);
            var today = LessonHomeScreen.PickDay(catalog);
            bool subscribed = IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;
            var progress = LessonProgressStore.Data;
            empty.SetActive(lessons.Count == 0);

            while (_tiles.Count < lessons.Count)
            {
                var tile = Instantiate(template, content);
                tile.name = "Lesson " + _tiles.Count;
                _tiles.Add(tile);
            }
            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                bool on = i < lessons.Count;
                tile.gameObject.SetActive(on);
                if (!on) continue;

                var lesson = lessons[i];
                tile.SetLabel(lesson.title);
                tile.SetBadge(lesson.IsComingSoon || lesson.IsLocked(subscribed) ? LessonTileView.Badge.Lock
                    : progress.IsCompleted(lesson.id) ? LessonTileView.Badge.Done
                    : lesson == today ? LessonTileView.Badge.Play
                    : LessonTileView.Badge.None);
                var group = tile.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = lesson.IsComingSoon ? 0.6f : 1f;

                var now = LessonHomeCache.CoverNow(lesson.coverUrl);
                tile.SetPicture(now != null ? now : _placeholder);
                if (now == null && !string.IsNullOrEmpty(lesson.coverUrl))
                    StartCoroutine(LessonHomeCache.Cover(lesson.coverUrl, s => { if (s != null) tile.SetPicture(s); }));

                tile.button.onClick.RemoveAllListeners();
                tile.button.onClick.AddListener(() => LessonHomeScreen.OpenLesson(_sm, lesson,
                    LessonHomeScreen.Catalog ?? catalog));
            }
        }
    }
}
