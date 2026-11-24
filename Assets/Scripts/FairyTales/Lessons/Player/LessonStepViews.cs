using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FairyTales.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FairyTales.Lessons
{
    // The step screens of the lesson player (spec §3). Each is a plain serialized
    // group of references inside LessonPlayerScreen — the prefab is built by
    // LessonPlayerBuilder — and the screen drives them one at a time.

    [Serializable]
    public abstract class LessonStepView
    {
        public GameObject root;
        protected LessonPlayerScreen P;

        public virtual void Bind(LessonPlayerScreen player) => P = player;
        public abstract void Begin(LessonStep step);
        /// <summary>The step is being left (next step, exit): stop what it started.</summary>
        public virtual void End() { }
        /// <summary>The exit question is open over the step.</summary>
        public virtual void Pause(bool pause) { }
    }

    // ─────────────────────────── 1. История ───────────────────────────

    [Serializable]
    public class StoryStepView : LessonStepView
    {
        public Image picture;
        public AspectRatioFitter pictureFit;
        public TMP_Text text;
        public Button textTap;
        public Button listen;
        public Button next;
        public GameObject pagePill;
        public TMP_Text pageLabel;

        private StoryStep _step;
        private int _page;

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            next.onClick.AddListener(OnNext);
            listen.onClick.AddListener(Replay);
            textTap.onClick.AddListener(Replay);
        }

        public override void Begin(LessonStep step)
        {
            _step = (StoryStep)step;
            _page = 0;
            if (_step.pages.Count == 0) { P.Next(); return; }
            ShowPage();
        }

        private void ShowPage()
        {
            var page = _step.pages[_page] ?? new StoryPage();
            P.SetPicture(picture, pictureFit, P.Picture(page.image));
            text.text = P.Text(page.text, highlightName: true);
            pagePill.SetActive(_step.pages.Count > 1);
            pageLabel.text = $"{_page + 1} / {_step.pages.Count}";
            P.NarrateThenShow(NarrationKey.Page(_step, _page), next);
        }

        private void Replay() => P.Speak(NarrationKey.Page(_step, _page));

        private void OnNext()
        {
            if (_page + 1 < _step.pages.Count) { _page++; ShowPage(); }
            else P.Next();
        }
    }

    // ─────────────────────── 2. Повтори как друг ───────────────────────
    // Release 1 player: the friend's video and the phrase, "I said it!" moves on.
    // Microphone, camera, mask and the character mode are plan section 5.

    [Serializable]
    public class RepeatStepView : LessonStepView
    {
        public GameObject videoFrame;
        public RawImage video;
        public AspectRatioFitter videoFit;
        public GameObject friendArt;
        public TMP_Text friendName;
        public TMP_Text phraseAr;
        public TMP_Text phraseTranslit;
        public TMP_Text phraseMeaning;
        public Button replay;
        public Button said;

        private VideoPlayer _player;
        private RenderTexture _texture;
        private bool _ended;

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            replay.onClick.AddListener(PlayVideo);
            said.onClick.AddListener(() => P.Next());
        }

        public override void Begin(LessonStep step)
        {
            var s = (RepeatStep)step;
            var phrase = s.phrase ?? new LessonPhrase();
            phraseAr.text = phrase.ar ?? "";
            phraseTranslit.text = P.Text(phrase.translit);
            phraseMeaning.text = string.IsNullOrEmpty(phrase.meaning) ? "" : "«" + P.Text(phrase.meaning) + "»";
            friendName.text = P.Text(s.friendName);
            friendName.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(s.friendName));

            var path = P.Package.MediaPath(s.video, UserProfile.ChildGender);
            bool hasVideo = path != null;
            videoFrame.SetActive(hasVideo);
            friendArt.SetActive(!hasVideo);
            LessonPlayerScreen.SetShown(replay, hasVideo);
            LessonPlayerScreen.SetShown(said, false);

            if (hasVideo)
            {
                EnsurePlayer();
                _player.url = new Uri(path).AbsoluteUri;
                PlayVideo();
                P.StepRoutine(ShowSaidAfterVideo());
            }
            else
            {
                P.ShowAfter(said, 3f);
            }
        }

        private void EnsurePlayer()
        {
            if (_player != null) return;
            _player = videoFrame.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.skipOnDrop = true;
            _player.prepareCompleted += OnPrepared;
            _player.loopPointReached += _ => _ended = true;
            _player.errorReceived += (_, msg) => { Debug.LogWarning($"[Lesson] video: {msg}"); _ended = true; };
        }

        private void OnPrepared(VideoPlayer vp)
        {
            int w = (int)vp.width, h = (int)vp.height;
            if (w <= 0 || h <= 0) { w = h = 720; }
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) _texture.Release();
                _texture = new RenderTexture(w, h, 0);
            }
            vp.targetTexture = _texture;
            video.texture = _texture;
            videoFit.aspectRatio = (float)w / h;
            vp.Play();
        }

        private void PlayVideo()
        {
            if (_player == null || string.IsNullOrEmpty(_player.url)) return;
            _ended = false;
            P.Voice.Stop();
            if (_player.isPrepared) { _player.time = 0; _player.Play(); }
            else _player.Prepare();
        }

        private IEnumerator ShowSaidAfterVideo()
        {
            // Video end, or 8 s (the longest allowed video) if it never reports one.
            float t = 0;
            while (!_ended && t < 9f) { if (!P.Paused) t += Time.unscaledDeltaTime; yield return null; }
            P.PopIn(said);
        }

        public override void Pause(bool pause)
        {
            if (_player == null || !_player.isPrepared) return;
            if (pause) _player.Pause();
            else if (!_ended) _player.Play();
        }

        public override void End()
        {
            if (_player != null) { _player.Stop(); _player.url = null; }
            video.texture = null;
            if (_texture != null) { _texture.Release(); UnityEngine.Object.Destroy(_texture); _texture = null; }
        }
    }

    // ─────────────────────────── 3. Задание ───────────────────────────

    [Serializable]
    public class QuizCard
    {
        public Button button;
        public RectTransform rect;
        public CanvasGroup group;
        public Image picture;
        public AspectRatioFitter pictureFit;
        public TMP_Text caption;
        public GameObject correctGlow;
        public Image hintGlow;
        public RectTransform check;
    }

    [Serializable]
    public class QuizStepView : LessonStepView
    {
        public TMP_Text question;
        public Button listen;
        public QuizCard[] cards = Array.Empty<QuizCard>();
        public RectTransform toast;
        public Image toastBg;
        public TMP_Text toastText;
        public Color toastCorrect = new(0.247f, 0.714f, 0.451f);
        public Color toastTry = new(0.482f, 0.357f, 0.839f);

        private QuizStep _step;
        // Card slot → option index in the server's order (narration keys follow it).
        private readonly List<int> _order = new();
        private int _errors;
        private bool _answered;
        private Coroutine _reading;
        private Vector2[] _cardPos = Array.Empty<Vector2>();

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            listen.onClick.AddListener(ReadAloud);
            _cardPos = new Vector2[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                int slot = i;
                cards[i].button.onClick.AddListener(() => OnCard(slot));
                _cardPos[i] = cards[i].rect.anchoredPosition;
            }
        }

        public override void Begin(LessonStep step)
        {
            _step = (QuizStep)step;
            _errors = 0;
            _answered = false;
            question.text = P.Text(_step.question, highlightName: true);
            toast.gameObject.SetActive(false);

            _order.Clear();
            for (int i = 0; i < _step.options.Count && i < cards.Length; i++)
                if (_step.options[i] != null) _order.Add(i);
            // Fresh order on every run: the child should look, not remember a position.
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }

            for (int slot = 0; slot < cards.Length; slot++)
            {
                var c = cards[slot];
                bool used = slot < _order.Count;
                c.rect.gameObject.SetActive(used);
                if (!used) continue;
                var option = _step.options[_order[slot]];
                P.SetPicture(c.picture, c.pictureFit, P.Picture(option.image));
                c.caption.text = P.Text(option.text);
                c.group.alpha = 1f;
                c.button.interactable = true;
                c.rect.localScale = Vector3.one;
                c.rect.anchoredPosition = _cardPos[slot];
                c.correctGlow.SetActive(false);
                c.hintGlow.gameObject.SetActive(false);
                c.check.gameObject.SetActive(false);
            }
            if (_order.Count == 0) { P.Next(); return; }
            ReadAloud();
        }

        /// <summary>The question, then each caption while its card pulses.</summary>
        private void ReadAloud()
        {
            if (_reading != null) P.StopCoroutine(_reading);
            _reading = P.StepRoutine(Read());
        }

        private IEnumerator Read()
        {
            yield return P.SpeakAndWait(NarrationKey.Question(_step));
            for (int slot = 0; slot < _order.Count && !_answered; slot++)
            {
                var c = cards[slot];
                if (!c.button.interactable) continue;
                c.rect.DOPunchScale(Vector3.one * 0.05f, 0.5f, 4).SetId(P.TweenId);
                yield return P.SpeakAndWait(NarrationKey.Option(_step, _order[slot]));
                yield return new WaitForSeconds(0.25f);
            }
            _reading = null;
        }

        private void OnCard(int slot)
        {
            if (_answered || slot >= _order.Count) return;
            if (_reading != null) { P.StopCoroutine(_reading); _reading = null; }
            var c = cards[slot];

            if (_step.options[_order[slot]].correct)
            {
                _answered = true;
                foreach (var other in cards) other.hintGlow.gameObject.SetActive(false);
                c.correctGlow.SetActive(true);
                c.check.gameObject.SetActive(true);
                c.check.localScale = Vector3.zero;
                c.check.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetId(P.TweenId);
                c.rect.DOPunchScale(Vector3.one * 0.06f, 0.4f, 5).SetId(P.TweenId);
                ShowToast("quiz_correct", toastCorrect);
                P.Voice.PlayBundled(LessonVoice.Correct);
                P.StepRoutine(NextAfter(1.5f));
                return;
            }

            // No red, no penalty: the card wobbles and fades, a soft "try again".
            _errors++;
            c.button.interactable = false;
            c.rect.DOShakeAnchorPos(0.45f, new Vector2(18, 0), 14, 0).SetId(P.TweenId);
            c.group.DOFade(0.45f, 0.3f).SetId(P.TweenId);
            ShowToast("quiz_try", toastTry);
            P.Voice.PlayBundled(LessonVoice.TryAgain);
            if (_errors >= 2) Hint();
        }

        private void Hint()
        {
            for (int slot = 0; slot < _order.Count; slot++)
            {
                if (!_step.options[_order[slot]].correct) continue;
                var glow = cards[slot].hintGlow;
                glow.gameObject.SetActive(true);
                var col = glow.color;
                col.a = 1f;
                glow.color = col;
                glow.DOFade(0.4f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(P.TweenId);
            }
        }

        private void ShowToast(string key, Color color)
        {
            toastText.text = LessonStrings.T(key);
            toastBg.color = color;
            toast.gameObject.SetActive(true);
            DOTween.Kill(toast);
            toast.localScale = Vector3.one * 0.6f;
            toast.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetId(P.TweenId).SetTarget(toast);
        }

        private IEnumerator NextAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            P.Next();
        }

        public override void End()
        {
            _reading = null;
        }
    }

    // ─────────────────────────── 4. Сделай в жизни ───────────────────────────

    [Serializable]
    public class DeedStepView : LessonStepView
    {
        public GameObject childBoy;
        public GameObject childGirl;
        public TMP_Text title;
        public GameObject pictureFrame;
        public Image picture;
        public AspectRatioFitter pictureFit;
        public Button listen;
        public Button checkRow;
        public Image checkRowBg;
        public GameObject checkMark;
        public TMP_Text checkLabel;
        public TMP_Text whoLabel;
        public Button done;
        public Color rowIdle = new(0.953f, 0.933f, 1f);
        public Color rowMarked = new(0.867f, 0.953f, 0.902f);
        /// <summary>How far the title moves up when the deed has no picture.</summary>
        public float titleLiftWithoutPicture = 110f;

        private DeedStep _step;
        private Vector2 _titlePos;
        private bool _marked;

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            _titlePos = title.rectTransform.anchoredPosition;
            listen.onClick.AddListener(() => P.Speak(NarrationKey.Deed(_step)));
            checkRow.onClick.AddListener(OnCheck);
            done.onClick.AddListener(OnDone);
        }

        public override void Begin(LessonStep step)
        {
            _step = (DeedStep)step;
            bool girl = UserProfile.ChildGender == "female";
            childGirl.SetActive(girl);
            childBoy.SetActive(!girl);
            title.text = P.Text(_step.title);

            var sprite = P.Picture(_step.image);
            pictureFrame.SetActive(sprite != null);
            P.SetPicture(picture, pictureFit, sprite);
            title.rectTransform.anchoredPosition = sprite != null
                ? _titlePos
                : _titlePos + new Vector2(0, titleLiftWithoutPicture);

            // A deed already marked today stays ticked: the mark counts once a day.
            SetMarked(!LessonProgressStore.Data.CanMarkDeedToday(P.Lesson.id, DateTime.Now));
            P.Speak(NarrationKey.Deed(_step));
        }

        private void OnCheck()
        {
            if (_marked) return;
            P.Voice.Pause(true);
            // The tick is the parent's: behind the parental gate so the child can't set it.
            ChildGatePopup.Show(() =>
            {
                P.Voice.Pause(false);
                P.DeedAwarded |= LessonProgressStore.MarkDeed(P.Lesson.id, _step.leafLabel);
                SetMarked(true);
                checkMark.transform.localScale = Vector3.zero;
                checkMark.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetId(P.TweenId);
            }, () => P.Voice.Pause(false));
        }

        private void SetMarked(bool marked)
        {
            _marked = marked;
            checkMark.SetActive(marked);
            checkRowBg.color = marked ? rowMarked : rowIdle;
            checkLabel.text = LessonStrings.T(marked ? "deed_marked" : "deed_check");
            whoLabel.gameObject.SetActive(!marked);
        }

        private void OnDone()
        {
            // "Done" without the mark: the deed waits in «Ждут отметки» for the parent.
            if (!_marked) LessonProgressStore.AddPendingDeed(P.Lesson.id, _step.title, _step.leafLabel);
            P.Next();
        }
    }

    // ─────────────────────────── 5. Награда ───────────────────────────

    [Serializable]
    public class RewardStepView : LessonStepView
    {
        public RectTransform star;
        public RectTransform rays;
        public RectTransform confetti;
        public TMP_Text title;
        public GameObject chipStar;
        public TMP_Text chipStarText;
        public GameObject chipLeaf;
        public TMP_Text chipLeafText;
        public TMP_Text again;
        public Button tree;
        public Button home;

        private (Vector2 pos, Quaternion rot)[] _bits = Array.Empty<(Vector2, Quaternion)>();

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            _bits = new (Vector2, Quaternion)[confetti.childCount];
            for (int i = 0; i < _bits.Length; i++)
            {
                var bit = (RectTransform)confetti.GetChild(i);
                _bits[i] = (bit.anchoredPosition, bit.localRotation);
            }
            home.onClick.AddListener(() => P.Close());
            tree.onClick.AddListener(() => { var open = LessonPlayerScreen.OpenTree; P.Close(); open?.Invoke(); });
        }

        public override void Begin(LessonStep step)
        {
            // Reaching this screen is what completes the lesson (spec §3, step 5).
            var reward = LessonProgressStore.CompleteLesson(P.Lesson.id, P.Lesson.title);
            int deed = P.DeedAwarded ? 1 : 0;
            int stars = (reward.Star ? 1 : 0) + deed;
            int leaves = (reward.Leaf ? 1 : 0) + deed;

            title.text = LessonStrings.T("reward_title");
            chipStar.SetActive(stars > 0);
            chipStarText.text = LessonStrings.Stars(stars);
            chipLeaf.SetActive(leaves > 0);
            chipLeafText.text = LessonStrings.Leaves(leaves);
            again.gameObject.SetActive(stars == 0);
            again.text = LessonStrings.T("reward_again");
            LessonPlayerScreen.SetShown(tree, LessonPlayerScreen.OpenTree != null);

            star.localScale = Vector3.zero;
            star.localEulerAngles = new Vector3(0, 0, -8);
            star.DOScale(1f, 0.6f).SetEase(Ease.OutBack).SetDelay(0.15f).SetId(P.TweenId);
            star.DORotate(new Vector3(0, 0, 8), 1.6f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(P.TweenId);
            rays.localEulerAngles = Vector3.zero;
            rays.DORotate(new Vector3(0, 0, -360), 24f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear).SetId(P.TweenId);
            for (int i = 0; i < confetti.childCount && i < _bits.Length; i++)
            {
                var bit = (RectTransform)confetti.GetChild(i);
                // Back to the layout spot: a killed loop leaves a piece wherever it was.
                bit.anchoredPosition = _bits[i].pos;
                bit.localRotation = _bits[i].rot;
                float fall = UnityEngine.Random.Range(5f, 9f);
                var from = bit.anchoredPosition;
                bit.DOAnchorPosY(from.y - 140f, fall).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                    .SetDelay(UnityEngine.Random.Range(0f, 1.5f)).SetId(P.TweenId);
                bit.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-180f, 180f)), fall, RotateMode.LocalAxisAdd)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(P.TweenId);
            }
            P.Voice.PlayBundled("reward");
            P.OnRewardShown();
        }
    }
}
