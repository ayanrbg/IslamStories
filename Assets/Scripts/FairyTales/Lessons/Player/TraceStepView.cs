using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// «Обведи» in a lesson: the child traces a coloring page's outline (<see cref="TraceSession"/>);
    /// once round, the drawing fills with its colours, «Машаллах, получилось!» takes the task
    /// line's place with a burst of sparkles, and the next arrow shows.
    /// </summary>
    [Serializable]
    public class TraceStepView : LessonStepView
    {
        public TMP_Text title;
        public Button listen;
        /// <summary>Shows the page (input off); the reveal colours it.</summary>
        public ColoringCanvas picture;
        public TraceSession tracer = new();
        public RectTransform doneBanner;
        public RectTransform sparkles;
        public Image sparkleTemplate;
        public Button next;

        private TraceStep _step;

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            listen.onClick.AddListener(() => { if (_step != null) P.Speak(NarrationKey.Trace(_step)); });
            next.onClick.AddListener(() => P.Next());
            tracer.Bind(player, () => P.Paused);
            tracer.Finished += OnTraced;
            sparkleTemplate.gameObject.SetActive(false);
        }

        public override void Begin(LessonStep step)
        {
            _step = (TraceStep)step;
            var page = ColoringPages.Find(_step.page);
            if (page == null || !page.CanTrace || !picture.Open(page, null) || !tracer.Begin(picture, reveal: true))
            {
                Debug.LogWarning($"[Lesson] trace: no page \"{_step.page}\"");
                picture.Close();
                P.Next();
                return;
            }
            picture.paint.raycastTarget = false;
            title.text = P.Text(_step.text, highlightName: true);
            title.alpha = 1f;
            LessonPlayerScreen.SetShown(next, false);
            doneBanner.gameObject.SetActive(false);
            P.Speak(NarrationKey.Trace(_step));
        }

        public override void End()
        {
            tracer.Stop();
            picture.Close();
            _step = null;
        }

        private void OnTraced()
        {
            if (_step == null) return;
            Diagnostics.Analytics.Log("trace_done", ("lesson", P.Lesson.id), ("page", _step.page),
                ("seconds", Mathf.RoundToInt(tracer.Seconds)));
            P.Voice.PlayBundled(LessonVoice.Correct);
            // «Машаллах, получилось!» takes the task line's place.
            title.DOFade(0f, 0.2f).SetId(P.TweenId);
            doneBanner.gameObject.SetActive(true);
            doneBanner.localScale = Vector3.one * 0.6f;
            doneBanner.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetId(P.TweenId);
            Sparkles.Burst(sparkles, sparkleTemplate, 18, P.TweenId);
            P.ShowAfter(next, 1.4f);
        }
    }

    /// <summary>A burst of coloured sparkles from the middle of <c>area</c> (praise moments).</summary>
    public static class Sparkles
    {
        private static readonly Color[] Colors =
            { new(1f, 0.8f, 0.25f), new(1f, 0.55f, 0.7f), new(0.45f, 0.8f, 1f), new(0.6f, 0.9f, 0.5f) };

        public static void Burst(RectTransform area, Image template, int count, object tweenId)
        {
            for (int i = area.childCount - 1; i >= 0; i--)
                if (area.GetChild(i) != template.transform) UnityEngine.Object.Destroy(area.GetChild(i).gameObject);
            var size = area.rect.size;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.Object.Instantiate(template, area);
                s.gameObject.SetActive(true);
                s.color = Colors[i % Colors.Length];
                var rt = s.rectTransform;
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.zero;
                float ang = i / (float)count * Mathf.PI * 2f + UnityEngine.Random.Range(-0.15f, 0.15f);
                float dist = UnityEngine.Random.Range(0.35f, 0.55f) * Mathf.Min(size.x, size.y);
                var to = new Vector2(Mathf.Cos(ang) * dist * 1.3f, Mathf.Sin(ang) * dist);
                DOTween.Sequence().SetId(tweenId)
                    .AppendInterval(UnityEngine.Random.Range(0f, 0.15f))
                    .Append(rt.DOAnchorPos(to, 0.9f).SetEase(Ease.OutCubic))
                    .Join(rt.DOScale(UnityEngine.Random.Range(0.6f, 1.3f), 0.35f).SetEase(Ease.OutBack))
                    .Join(rt.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-180f, 180f)), 0.9f))
                    .Append(s.DOFade(0f, 0.4f))
                    .OnComplete(() => { if (s != null) UnityEngine.Object.Destroy(s.gameObject); });
            }
        }
    }
}
