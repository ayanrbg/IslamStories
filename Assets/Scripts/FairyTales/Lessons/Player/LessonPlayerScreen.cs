using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FairyTales.Api;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>What to open in the lesson player.</summary>
    public class LessonLaunch
    {
        public string Id;
        /// <summary>contentVersion from the catalog (0 = unknown): an up-to-date copy then opens without the network.</summary>
        public int KnownVersion;
        /// <summary>Shown while the lesson downloads.</summary>
        public string Title;
        public Sprite Cover;
        /// <summary>Lesson to prefetch over Wi-Fi after the reward, or null.</summary>
        public LessonSummary Next;
        public bool Subscribed;
        /// <summary>Play the installed copy only, never ask the server (demo, tests).</summary>
        public bool InstalledOnly;
    }

    /// <summary>
    /// Full-screen lesson player (spec §3): downloads the lesson with a progress card,
    /// then shows its steps one at a time under a segmented progress bar. ✕ and the
    /// Android Back key ask before leaving; the step reached is remembered and offered
    /// next time. There is no going back a step.
    ///
    /// A screen of the app's ScreenManager like the others, so the popups that live
    /// above the screens (ChildGatePopup) cover it too. The layout is built in code by
    /// LessonPlayerBuilder (Nooria ▸ Lessons ▸ Build lesson player).
    /// </summary>
    public class LessonPlayerScreen : BaseScreen
    {
        /// <summary>Opens the Tree from the reward screen; null hides that button (the Tree is plan section 7).</summary>
        public static Action OpenTree;

        [Header("Chrome")]
        public GameObject chrome;
        public Button close;
        public RectTransform segments;
        /// <summary>The capsule round the segments; hidden with them (the parent's permission screen).</summary>
        public GameObject segmentsBar;
        public GameObject SegmentsBar => segmentsBar != null ? segmentsBar : segments.gameObject;
        public Image segmentTemplate;
        public Color segmentDone = new(0.878f, 0.341f, 0.482f);
        public Color segmentTodo = new(1f, 1f, 1f, 0.75f);

        [Header("Download")]
        public GameObject prepare;
        public Button prepareClose;
        public Image prepareCover;
        public AspectRatioFitter prepareCoverFit;
        /// <summary>The download card (title, cover, bar), hidden under the resume question.</summary>
        public GameObject prepareCard;
        public TMP_Text prepareTitle;
        public TMP_Text prepareStatus;
        public GameObject prepareBar;
        public RectTransform prepareBarFill;
        public TMP_Text preparePercent;
        public Button prepareRetry;

        [Header("Questions")]
        public GameObject exitPopup;
        public Button exitStay;
        public Button exitLeave;
        public GameObject resumePopup;

        [Header("Soft offer")]
        /// <summary>«Понравились уроки?» over the reward of the second lesson (spec §7), once.</summary>
        public GameObject softOffer;
        public RectTransform softOfferCard;
        public Button softOfferGo;
        public Button softOfferLater;
        public TMP_Text resumeNote;
        public Button resumeGo;
        public Button resumeRestart;

        [Header("Steps")]
        public StoryStepView story = new();
        public RepeatStepView repeat = new();
        public QuizStepView quiz = new();
        public DeedStepView deed = new();
        public RewardStepView reward = new();

        // ─────────────────────────── state ───────────────────────────

        public LessonPackage Package { get; private set; }
        public Lesson Lesson => Package?.Lesson;
        public LessonVoice Voice { get; private set; }
        /// <summary>The exit question is open: timers and narration wait.</summary>
        public bool Paused { get; private set; }
        /// <summary>A parent marked the deed during this run (a star and a leaf more on the reward).</summary>
        public bool DeedAwarded { get; set; }
        /// <summary>DOTween id of everything a step animates; killed when the step ends.</summary>
        public object TweenId => this;

        private readonly LessonImages _images = new();
        private readonly List<Coroutine> _stepRoutines = new();
        private readonly List<Image> _segments = new();
        private LessonLaunch _launch;
        private Type _returnTo;
        private LessonStepView _view;
        private int _index;
        private bool _bound;
        private bool _rewardShown;
        private bool _closing;
        private Coroutine _flow;

        // ─────────────────────────── opening ───────────────────────────

        /// <summary>Opens a lesson over the current screen. False when the player isn't in the scene.</summary>
        public static bool Open(LessonLaunch launch)
        {
            if (launch == null || string.IsNullOrEmpty(launch.Id)) return false;
            var sm = FindAnyObjectByType<ScreenManager>();
            var screen = sm != null ? sm.Get<LessonPlayerScreen>() : null;
            if (screen == null) return false;
            screen._launch = launch;
            screen._returnTo = sm.Current != null && sm.Current != screen ? sm.Current.GetType() : typeof(LibraryScreen);
            sm.ShowOver<LessonPlayerScreen>();
            return true;
        }

        private void BindOnce()
        {
            if (_bound) return;
            _bound = true;
            Voice = new LessonVoice(this);
            close.onClick.AddListener(AskExit);
            prepareClose.onClick.AddListener(Close);
            prepareRetry.onClick.AddListener(StartFlow);
            exitStay.onClick.AddListener(() => ShowExit(false));
            exitLeave.onClick.AddListener(Close);
            if (softOffer != null)
            {
                softOfferGo.onClick.AddListener(OnSoftOfferGo);
                softOfferLater.onClick.AddListener(HideSoftOffer);
            }
            foreach (var v in Views) v.Bind(this);
            segmentTemplate.gameObject.SetActive(false);
        }

        private IEnumerable<LessonStepView> Views => new LessonStepView[] { story, repeat, quiz, deed, reward };

        protected override void OnPrepare()
        {
            BindOnce();
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            _closing = false;
            StartFlow();
        }

        protected override void OnHidden() => Cleanup();

        private void StartFlow()
        {
            if (_flow != null) StopCoroutine(_flow);
            _flow = StartCoroutine(Flow());
        }

        private IEnumerator Flow()
        {
            Cleanup();
            ShowOnly(prepare: true);
            PrepareCard.SetActive(true);
            exitPopup.SetActive(false);
            resumePopup.SetActive(false);
            prepareTitle.text = _launch?.Title ?? "";
            SetPicture(prepareCover, prepareCoverFit, _launch?.Cover);
            prepareStatus.text = LessonStrings.T("prepare");
            SetShown(prepareRetry, false);
            prepareBar.SetActive(true);
            SetProgress(0f);
            if (_launch == null) yield break;

            LessonPackage package = null;
            var failure = LessonFailure.None;
            if (_launch.InstalledOnly)
            {
                yield return LessonCache.Open(_launch.Id, Loc.Lang, p => package = p);
                if (package == null) failure = LessonFailure.NotFound;
            }
            else
            {
                var api = FindAnyObjectByType<ApiClient>();
                LessonPrepareResult result = null;
                yield return LessonCache.Prepare(new LessonService(api), _launch.Id, _launch.KnownVersion,
                    Loc.Lang, SetProgress, r => result = r);
                package = result?.Package;
                failure = result?.Failure ?? LessonFailure.Network;
            }

            if (package?.Lesson == null || package.Lesson.steps.Count == 0)
            {
                ShowFailure(package == null ? failure : LessonFailure.Malformed);
                yield break;
            }

            Package = package;
            if (string.IsNullOrEmpty(prepareTitle.text)) prepareTitle.text = Lesson.title;
            SetProgress(1f);
            yield return new WaitForSeconds(0.25f);

            int start = 0;
            int saved = LessonProgressStore.Data.UnfinishedStep(Lesson.id, Lesson.contentVersion);
            if (saved > 0 && saved < Lesson.steps.Count)
            {
                bool? resume = null;
                resumeNote.text = LessonStrings.T("resume_note", saved + 1, Lesson.steps.Count);
                resumeGo.onClick.RemoveAllListeners();
                resumeRestart.onClick.RemoveAllListeners();
                resumeGo.onClick.AddListener(() => resume = true);
                resumeRestart.onClick.AddListener(() => resume = false);
                // The question replaces the download card; the avatars stay around it.
                PrepareCard.SetActive(false);
                resumePopup.SetActive(true);
                while (resume == null) yield return null;
                resumePopup.SetActive(false);
                if (resume == true) start = saved;
                else LessonProgressStore.ClearUnfinished(Lesson.id);
            }

            BuildSegments(Lesson.steps.Count);
            ShowOnly(prepare: false);
            _flow = null;
            ShowStep(start);
        }

        private void ShowFailure(LessonFailure failure)
        {
            prepareBar.SetActive(false);
            prepareStatus.text = LessonStrings.T(failure switch
            {
                LessonFailure.Network => "fail_network",
                LessonFailure.Locked => "fail_locked",
                _ => "fail_missing",
            });
            // Only a network failure can heal by itself; a locked lesson goes through the paywall (plan section 8).
            SetShown(prepareRetry, failure == LessonFailure.Network);
            Debug.Log($"[Lesson] {_launch?.Id} failed: {failure}");
        }

        private void SetProgress(float p)
        {
            var parent = (RectTransform)prepareBarFill.parent;
            float w = parent.rect.width, h = prepareBarFill.rect.height;
            prepareBarFill.sizeDelta = new Vector2(Mathf.Max(h, w * Mathf.Clamp01(p)), prepareBarFill.sizeDelta.y);
            preparePercent.text = Mathf.RoundToInt(Mathf.Clamp01(p) * 100) + "%";
        }

        private void ShowOnly(bool prepare)
        {
            this.prepare.SetActive(prepare);
            chrome.SetActive(false);
            foreach (var v in Views) v.root.SetActive(false);
        }

        // ─────────────────────────── steps ───────────────────────────

        private void BuildSegments(int count)
        {
            foreach (var s in _segments) if (s != null) Destroy(s.gameObject);
            _segments.Clear();
            // The template's parent is laid out by a HorizontalLayoutGroup.
            for (int i = 0; i < count; i++)
            {
                var seg = Instantiate(segmentTemplate, segments);
                seg.gameObject.SetActive(true);
                seg.name = "Segment" + (i + 1);
                _segments.Add(seg);
            }
        }

        private void PaintSegments()
        {
            for (int i = 0; i < _segments.Count; i++)
                _segments[i].color = i <= _index ? segmentDone : segmentTodo;
        }

        private void ShowStep(int index)
        {
            EndStep();
            _index = index;
            if (_index >= Lesson.steps.Count) { Finish(); return; }

            var step = Lesson.steps[_index];
            var view = ViewFor(step);
            if (view == null) { Next(); return; }

            // Remembered from the second step on: «Продолжим урок?» next time.
            if (_index > 0 && step is not RewardStep)
                LessonProgressStore.SetUnfinished(Lesson.id, _index, Lesson.contentVersion);

            _view = view;
            bool isReward = view == reward;
            chrome.SetActive(!isReward);
            PaintSegments();
            view.root.SetActive(true);
            var group = view.root.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.DOFade(1f, 0.25f).SetId(TweenId);
            }
            if (isReward) _rewardShown = true;
            view.Begin(step);
        }

        private LessonStepView ViewFor(LessonStep step) => step switch
        {
            StoryStep => story,
            RepeatStep => repeat,
            QuizStep => quiz,
            DeedStep => deed,
            RewardStep => reward,
            _ => null,
        };

        /// <summary>The current step is done.</summary>
        public void Next()
        {
            if (Lesson == null || _closing) return;
            ShowStep(_index + 1);
        }

        /// <summary>Past the last step. A lesson without a reward step still gets its reward.</summary>
        private void Finish()
        {
            if (_rewardShown) { Close(); return; }
            _index = Lesson.steps.Count;
            _view = reward;
            _rewardShown = true;
            chrome.SetActive(false);
            reward.root.SetActive(true);
            reward.Begin(new RewardStep { type = LessonStepType.Reward, index = -1 });
        }

        public void OnRewardShown()
        {
            if (_launch?.Next != null && !_launch.InstalledOnly)
                LessonCache.PrefetchInBackground(new LessonService(FindAnyObjectByType<ApiClient>()),
                    _launch.Next, Loc.Lang, _launch.Subscribed);
        }

        private void EndStep()
        {
            foreach (var c in _stepRoutines) if (c != null) StopCoroutine(c);
            _stepRoutines.Clear();
            DOTween.Kill(TweenId);
            Voice?.Stop();
            if (_view != null)
            {
                _view.End();
                _view.root.SetActive(false);
                _view = null;
            }
            // Pictures of the step just left: a lesson's worth of 2048 px textures would not fit cheap phones.
            _images.Release();
        }

        // ─────────────────────────── leaving ───────────────────────────

        private void Update()
        {
            if (_view == repeat) repeat.Tick();
            if (!IsVisible || !Input.GetKeyDown(KeyCode.Escape)) return;
            // The parental gate answers Back itself.
            var gate = FindAnyObjectByType<ChildGatePopup>();
            if (gate != null && gate.gameObject.activeInHierarchy) return;
            if (softOffer != null && softOffer.activeSelf) HideSoftOffer();
            else if (exitPopup.activeSelf) ShowExit(false);
            else if (prepare.activeSelf || resumePopup.activeSelf || _view == reward) Close();
            else AskExit();
        }

        private void OnApplicationPause(bool pause)
        {
            if (IsVisible) _view?.AppPause(pause);
        }

        private void AskExit() => ShowExit(true);

        private void ShowExit(bool show)
        {
            Paused = show;
            exitPopup.SetActive(show);
            Voice?.Pause(show);
            _view?.Pause(show);
        }

        /// <summary>Leaves the player for the screen it was opened from.</summary>
        public void Close()
        {
            if (_closing) return;
            _closing = true;
            if (_flow != null) { StopCoroutine(_flow); _flow = null; }
            EndStep();
            var sm = FindAnyObjectByType<ScreenManager>();
            if (sm != null) sm.Show(_returnTo ?? typeof(LibraryScreen));
        }

        private void Cleanup()
        {
            EndStep();
            Paused = false;
            DeedAwarded = false;
            _rewardShown = false;
            _index = 0;
            Package = null;
            Voice?.Release();
            _images.Release();
            if (exitPopup != null) exitPopup.SetActive(false);
            if (softOffer != null) softOffer.SetActive(false);
        }

        // ─────────────────────────── soft offer ───────────────────────────

        private const string SoftOfferKey = "lessons_soft_offer_shown";

        /// <summary>
        /// The soft subscription offer comes once, on the reward that completes the
        /// second lesson for the first time, and never to a subscriber.
        /// </summary>
        public static bool ShouldOfferSubscription(bool subscribed, bool firstTime, int completedCount, bool shownBefore)
            => !subscribed && firstTime && completedCount >= 2 && !shownBefore;

        /// <summary>Called by the reward: shows the offer after the star has landed, when it is due.</summary>
        public void MaybeOfferSubscription(bool firstTime)
        {
            if (softOffer == null) return;
            bool subscribed = IAP.IAPManager.Instance != null ? IAP.IAPManager.Instance.IsSubscribed : _launch != null && _launch.Subscribed;
            if (!ShouldOfferSubscription(subscribed, firstTime, LessonProgressStore.Data.CompletedCount,
                    PlayerPrefs.GetInt(SoftOfferKey, 0) == 1)) return;
            StepRoutine(SoftOfferRoutine());
        }

        private IEnumerator SoftOfferRoutine()
        {
            float t = 0f;
            while (t < 2.2f) { if (!Paused) t += Time.unscaledDeltaTime; yield return null; }
            PlayerPrefs.SetInt(SoftOfferKey, 1);
            PlayerPrefs.Save();
            Diagnostics.Analytics.Log("lesson_soft_offer_view");
            softOffer.SetActive(true);
            var group = softOffer.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.DOFade(1f, 0.25f).SetId(TweenId);
            }
            softOfferCard.localScale = Vector3.one * 0.7f;
            softOfferCard.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetId(TweenId);
        }

        private void HideSoftOffer()
        {
            Diagnostics.Analytics.Log("lesson_soft_offer_later");
            softOffer.SetActive(false);
        }

        private void OnSoftOfferGo()
        {
            Diagnostics.Analytics.Log("lesson_soft_offer_tap");
            var sm = FindAnyObjectByType<ScreenManager>();
            var back = _returnTo;
            Close();
            // The paywall gates purchase and restore behind the parental question itself.
            if (sm != null) UI.Payment.PaymentScreen.Open(sm, back ?? typeof(LibraryScreen), source: "lesson_soft_offer");
        }

        // ─────────────────────────── helpers for the steps ───────────────────────────

        /// <summary>Runs a coroutine that is stopped when the step ends.</summary>
        public Coroutine StepRoutine(IEnumerator routine)
        {
            var c = StartCoroutine(routine);
            _stepRoutines.Add(c);
            return c;
        }

        /// <summary>Lesson text for the screen: name and gender forms resolved, the name optionally coloured.</summary>
        public string Text(string raw, bool highlightName = false)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            if (highlightName && !string.IsNullOrEmpty(TaleText.ChildName))
                raw = raw.Replace("{childName}", "<color=#E0577B>{childName}</color>");
            return TaleText.Resolve(raw);
        }

        public Sprite Picture(string key) => string.IsNullOrEmpty(key) ? null : _images.Get(Package, key);

        public void SetPicture(Image image, AspectRatioFitter fit, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            if (sprite != null && fit != null) fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        /// <summary>Plays a text's narration if it has one (tap to repeat).</summary>
        public void Speak(string key) => Voice.PlayFile(Package?.NarrationPath(key));

        /// <summary>Plays a narration and waits for its end; no narration — no wait.</summary>
        public IEnumerator SpeakAndWait(string key)
        {
            var path = Package?.NarrationPath(key);
            if (path == null) yield break;
            bool done = false;
            Voice.PlayFile(path, _ => done = true);
            while (!done) yield return null;
        }

        /// <summary>
        /// Narrates a text, then shows <paramref name="button"/>: after the narration ends,
        /// or after 5 s when there is no narration (spec §8, offline or failed synthesis).
        /// </summary>
        public void NarrateThenShow(string key, Button button)
        {
            SetShown(button, false);
            StepRoutine(NarrateThenShowRoutine(key, button));
        }

        private IEnumerator NarrateThenShowRoutine(string key, Button button)
        {
            float waited = 0f;
            var path = Package?.NarrationPath(key);
            bool played = false, done = path == null;
            if (path != null) Voice.PlayFile(path, ok => { played = ok; done = true; });
            while (!done) { if (!Paused) waited += Time.unscaledDeltaTime; yield return null; }
            if (!played)
                while (waited < 5f) { if (!Paused) waited += Time.unscaledDeltaTime; yield return null; }
            PopIn(button);
        }

        public void ShowAfter(Button button, float seconds)
        {
            SetShown(button, false);
            StepRoutine(ShowAfterRoutine(button, seconds));
        }

        private IEnumerator ShowAfterRoutine(Button button, float seconds)
        {
            float t = 0f;
            while (t < seconds) { if (!Paused) t += Time.unscaledDeltaTime; yield return null; }
            PopIn(button);
        }

        public void PopIn(Button button)
        {
            var holder = Holder(button);
            if (holder.activeSelf) return;
            holder.SetActive(true);
            var t = holder.transform;
            t.localScale = Vector3.one * 0.6f;
            t.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetId(TweenId);
        }

        /// <summary>
        /// A raised button is three siblings — shadow, darker base, face — inside a holder
        /// named "…Holder" (LessonPlayerBuilder). Showing or hiding it means the holder.
        /// </summary>
        private GameObject PrepareCard => prepareCard != null ? prepareCard : prepareTitle.transform.parent.gameObject;

        public static GameObject Holder(Button button)
        {
            var parent = button.transform.parent;
            return parent != null && parent.name.EndsWith("Holder") ? parent.gameObject : button.gameObject;
        }

        public static void SetShown(Button button, bool shown) => Holder(button).SetActive(shown);
    }
}
