using System;
using System.Collections;
using DG.Tweening;
using FairyTales.UI.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Step 2, «Повтори как друг» (spec §4), without AR (plan section 5):
    ///
    ///  0. the parent's camera-and-microphone screen, the first time only;
    ///  1. a short scene — the child's avatar and the friend (a drawn body with the real
    ///     child's head from the video) stand side by side, the friend waves, the phrase
    ///     pops up in a bubble; «▶ Смотреть», or by itself after 4 s;
    ///  2. the friend's video plays once (tap to repeat);
    ///  3. the child's turn: their face from the front camera with a flat mask («Рамка»),
    ///     or the avatar in the mask whose mouth moves with the voice («Персонаж»); the
    ///     microphone opens by itself and listens up to 5 s; a voice is played straight
    ///     back («Послушай себя!»), then praise and «Дальше». Silence: «Скажи вместе с
    ///     другом!» and the video again; after two quiet tries «Дальше» comes anyway.
    ///     No microphone: «Я сказал(а)!».
    ///
    /// The step never blocks the lesson. Camera frames are only drawn; the voice lives in
    /// memory until the step ends.
    /// </summary>
    [Serializable]
    public class RepeatStepView : LessonStepView
    {
        [Header("Parent screen")]
        public GameObject permissions;
        public Button permAllow;
        public Button permNoCamera;

        [Header("Scene with the friend")]
        public GameObject intro;
        public AvatarView introFriend;
        public AvatarView introChild;
        public RectTransform introBubble;
        public TMP_Text introFriendName;
        public TMP_Text introChildName;
        public TMP_Text introAr;
        public TMP_Text introTranslit;
        public TMP_Text introMeaning;
        public Button watch;

        [Header("Friend")]
        public GameObject turn;
        public GameObject videoFrame;
        public RawImage video;
        public AspectRatioFitter videoFit;
        public Button videoTap;
        public GameObject friendArt;
        public AvatarView friendFigure;
        public TMP_Text friendName;
        public RectTransform friendBubble;
        public TMP_Text friendBubbleText;
        public Button replay;

        [Header("Me")]
        public RectTransform meFrame;
        public RawImage cameraView;
        public GameObject characterBg;
        public AvatarView meAvatar;
        public AvatarJaw meJaw;
        public TMP_Text meLabel;
        public Image meDot;
        public TMP_Text meHint;
        public Image sticker;
        public Button[] maskButtons = Array.Empty<Button>();
        public GameObject[] maskRings = Array.Empty<GameObject>();
        /// <summary>Flat masks: crown with a crescent, fanous lantern, stars.</summary>
        public Sprite[] masks = Array.Empty<Sprite>();
        public Color dotLive = new(0.878f, 0.341f, 0.482f);
        public Color dotCharacter = new(0.482f, 0.357f, 0.839f);

        [Header("Phrase and microphone")]
        public TMP_Text phraseTranslit;
        public TMP_Text phraseAr;
        public Button mic;
        public RectTransform micPulse;
        public TMP_Text hint;
        public Button said;

        [Header("Listen to yourself")]
        public GameObject listenBar;
        public Button listenPlay;
        public RectTransform[] waveBars = Array.Empty<RectTransform>();
        public Button again;
        public TMP_Text praise;
        public Button next;

        private enum Phase { Idle, Video, Listening, Playback, Done }

        private RepeatStep _step;
        private VideoPlayer _player;
        private RenderTexture _texture;
        private bool _videoEnded;
        private bool _hasVideo;
        private LessonMic _mic;
        private LessonCamera _camera;
        private bool _useMic;
        private int _quietTries;
        private int _mask;
        private Phase _phase;
        private Coroutine _flow;
        private Coroutine _attempt;
        private bool _watchPressed;
        private bool _appPaused;
        private bool _happy;
        private bool _succeeded;
        private Coroutine _follow;

        // Where each mask sits on the head's box: a point in head sizes from the box's top centre
        // (x right, y up), the mask's width in head widths, and which point of the mask goes there.
        private static readonly (Vector2 at, float width, Vector2 pivot)[] MaskPlace =
        {
            (new Vector2(0f, -0.1f), 0.85f, new Vector2(0.5f, 0.08f)),   // crown sitting on the head
            (new Vector2(0.62f, -0.55f), 0.42f, new Vector2(0.5f, 0.5f)), // lantern beside the cheek
            (new Vector2(0f, -0.02f), 1.12f, new Vector2(0.5f, 0.73f)),   // star headband: its arch on top
        };

        public override void Bind(LessonPlayerScreen player)
        {
            base.Bind(player);
            _mic = new LessonMic();
            _camera = new LessonCamera(cameraView, meFrame);
            permAllow.onClick.AddListener(() => P.StartCoroutine(Answer(LessonPermissions.Choice.Allow)));
            permNoCamera.onClick.AddListener(() => P.StartCoroutine(Answer(LessonPermissions.Choice.NoCamera)));
            watch.onClick.AddListener(() => _watchPressed = true);
            replay.onClick.AddListener(ReplayVideo);
            videoTap.onClick.AddListener(ReplayVideo);
            mic.onClick.AddListener(NewAttempt);
            again.onClick.AddListener(NewAttempt);
            listenPlay.onClick.AddListener(PlayBack);
            said.onClick.AddListener(() => P.Next());
            next.onClick.AddListener(() => P.Next());
            for (int i = 0; i < maskButtons.Length; i++)
            {
                int index = i;
                maskButtons[i].onClick.AddListener(() => SetMask(index, pop: true));
            }
            // The avatar's jaw and the sticker follow the head after every layout.
            meAvatar.LaidOut += PlaceSticker;
        }

        public override void Begin(LessonStep step)
        {
            _step = (RepeatStep)step;
            _quietTries = 0;
            _phase = Phase.Idle;
            _watchPressed = false;
            _happy = false;
            _succeeded = false;
            var phrase = _step.phrase ?? new LessonPhrase();

            introAr.text = phraseAr.text = phrase.ar ?? "";
            introTranslit.text = phraseTranslit.text = P.Text(phrase.translit);
            introMeaning.text = string.IsNullOrEmpty(phrase.meaning) ? "" : "«" + P.Text(phrase.meaning) + "»";
            friendBubbleText.text = P.Text(phrase.translit) + "!";
            var name = P.Text(_step.friendName);
            introFriendName.text = friendName.text = name;
            friendName.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(name));
            introFriendName.gameObject.SetActive(!string.IsNullOrEmpty(name));
            introChildName.text = TaleText.ChildName ?? "";

            ShowFriend(introFriend);
            ShowFriend(friendFigure);

            var path = P.Package.MediaPath(_step.video, UserProfile.ChildGender);
            _hasVideo = path != null;
            if (_hasVideo)
            {
                EnsurePlayer();
                _player.url = new Uri(path).AbsoluteUri;
                _player.Prepare(); // ready by the time the scene ends
            }

            permissions.SetActive(false);
            intro.SetActive(false);
            turn.SetActive(false);
            _flow = P.StepRoutine(Flow());
        }

        // ─────────────────────────── flow ───────────────────────────

        private IEnumerator Flow()
        {
            if (LessonPermissions.NeedsAsking)
            {
                permissions.SetActive(true);
                P.SegmentsBar.SetActive(false); // the parent screen isn't a part of the lesson
                while (LessonPermissions.NeedsAsking) yield return null;
                permissions.SetActive(false);
                P.SegmentsBar.SetActive(true);
            }
            else yield return LessonPermissions.Refresh();

            yield return Intro();
            yield return SetUpTurn();
            yield return PlayVideoAndWait();
            StartListening();
        }

        private IEnumerator Answer(LessonPermissions.Choice choice)
        {
            permAllow.interactable = permNoCamera.interactable = false;
            yield return LessonPermissions.Request(choice, null);
            permAllow.interactable = permNoCamera.interactable = true;
        }

        private IEnumerator Intro()
        {
            intro.SetActive(true);
            LessonPlayerScreen.SetShown(watch, false);
            introBubble.localScale = Vector3.zero;

            // The friend waves: a little hop and a sway, then the phrase pops up.
            var f = (RectTransform)introFriend.transform;
            f.localEulerAngles = Vector3.zero;
            yield return Wait(0.4f);
            f.DOPunchAnchorPos(new Vector2(0, 40), 0.6f, 3, 0.5f).SetId(P.TweenId);
            f.DOPunchRotation(new Vector3(0, 0, 6), 0.9f, 4, 0.6f).SetId(P.TweenId);
            yield return Wait(0.5f);
            introBubble.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetId(P.TweenId);
            yield return Wait(0.6f);
            P.PopIn(watch);
            var face = Holder(watch).transform;
            face.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetDelay(0.4f).SetId(P.TweenId);

            float t = 0;
            while (!_watchPressed && t < 4f) { if (!P.Paused) t += Time.unscaledDeltaTime; yield return null; }
            DOTween.Kill(face);
            intro.SetActive(false);
        }

        private IEnumerator SetUpTurn()
        {
            turn.SetActive(true);
            videoFrame.SetActive(_hasVideo);
            friendArt.SetActive(!_hasVideo);
            LessonPlayerScreen.SetShown(replay, _hasVideo);
            friendBubble.gameObject.SetActive(false);
            listenBar.SetActive(false);
            praise.gameObject.SetActive(false);
            LessonPlayerScreen.SetShown(next, false);
            LessonPlayerScreen.SetShown(said, false);
            hint.text = LessonStrings.T("repeat_hint");
            hint.gameObject.SetActive(true);

            _useMic = LessonPermissions.UseMic;
            Holder(mic).SetActive(_useMic);
            mic.interactable = false;
            micPulse.gameObject.SetActive(false);

            // «Рамка» when the camera works, otherwise «Персонаж» (AR comes in plan section 6).
            bool live = false;
            if (LessonPermissions.UseCamera) yield return _camera.Start(ok => live = ok);
            cameraView.gameObject.SetActive(live);
            characterBg.SetActive(!live);
            meAvatar.gameObject.SetActive(!live);
            meJaw.enabled = !live && _useMic;
            meJaw.Open = 0;
            meDot.color = live ? dotLive : dotCharacter;
            meLabel.text = live ? LessonStrings.T("repeat_me", TaleText.ChildName ?? "") : LessonStrings.T("repeat_no_camera");
            if (live && string.IsNullOrEmpty(TaleText.ChildName)) meLabel.text = LessonStrings.T("repeat_me_noname");
            meHint.gameObject.SetActive(!live && _useMic);
            meHint.text = LessonStrings.T("repeat_mouth");
            SetMask(_mask, pop: false);
        }

        private IEnumerator PlayVideoAndWait()
        {
            if (!_hasVideo)
            {
                // No video: the drawn friend «says» it in the bubble.
                yield return Bubble();
                yield return Wait(1.2f);
                yield break;
            }
            PlayVideo();
            P.StepRoutine(Bubble());
            // The video's end, or 9 s (the longest allowed video is 8 s) if it never reports one.
            float t = 0;
            while (!_videoEnded && t < 9f) { if (!P.Paused && !_appPaused) t += Time.unscaledDeltaTime; yield return null; }
            _phase = Phase.Idle;
        }

        private IEnumerator Bubble()
        {
            friendBubble.gameObject.SetActive(true);
            friendBubble.localScale = Vector3.zero;
            yield return Wait(_hasVideo ? 0.8f : 0.2f);
            friendBubble.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetId(P.TweenId);
        }

        // ─────────────────────────── the child's turn ───────────────────────────

        private void StartListening()
        {
            if (_phase == Phase.Done) return;
            if (!_useMic)
            {
                P.PopIn(said);
                return;
            }
            mic.interactable = true;
            NewAttempt();
        }

        private void NewAttempt()
        {
            if (!_useMic || P.Paused) return;
            StopAttempt();
            StopVideo();
            listenBar.SetActive(false);
            praise.gameObject.SetActive(false);
            Holder(mic).SetActive(true);
            if (!_succeeded && _quietTries < 2) LessonPlayerScreen.SetShown(next, false);
            hint.gameObject.SetActive(true);
            hint.text = LessonStrings.T("repeat_hint");
            _attempt = P.StepRoutine(Attempt());
        }

        private IEnumerator Attempt()
        {
            _phase = Phase.Listening;
            Pulse(true);
            MicAttempt result = MicAttempt.Failed;
            bool finished = false;
            _follow = P.StepRoutine(FollowLevel());
            yield return _mic.Listen(() => P.Paused || _appPaused, r => { result = r; finished = true; });
            Pulse(false);
            if (!finished) yield break;
            Diagnostics.Analytics.Log("repeat_attempt", ("lesson", P.Lesson.id),
                ("result", result.ToString().ToLowerInvariant()), ("quiet_tries", _quietTries),
                ("camera", LessonPermissions.UseCamera));

            switch (result)
            {
                case MicAttempt.Voice:
                    _quietTries = 0;
                    yield return Playback();
                    break;
                case MicAttempt.Silent:
                    _quietTries++;
                    if (_quietTries >= 2)
                    {
                        // Never stuck: praise the try and let them go on.
                        hint.text = LessonStrings.T("repeat_next_time");
                        P.PopIn(next);
                        _phase = Phase.Idle;
                        yield break;
                    }
                    hint.text = LessonStrings.T("repeat_say_with");
                    yield return SpeakBundled(LessonVoice.SayWithFriend);
                    yield return PlayVideoAndWait();
                    _attempt = null;
                    NewAttempt();
                    break;
                default:
                    // The microphone failed: fall back to the button.
                    _useMic = false;
                    Holder(mic).SetActive(false);
                    meJaw.enabled = false;
                    meHint.gameObject.SetActive(false);
                    P.PopIn(said);
                    _phase = Phase.Idle;
                    break;
            }
        }

        private IEnumerator Playback()
        {
            _phase = Phase.Playback;
            hint.gameObject.SetActive(false);
            Holder(mic).SetActive(false);
            listenBar.SetActive(true);
            listenBar.transform.localScale = Vector3.one * 0.8f;
            listenBar.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetId(P.TweenId);
            PaintWave(-1);
            _happy = true;
            yield return SpeakBundled(LessonVoice.ListenYourself);
            yield return PlayRecording();
            _happy = false;

            praise.text = LessonStrings.T("repeat_praise");
            praise.gameObject.SetActive(true);
            praise.transform.localScale = Vector3.zero;
            praise.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetId(P.TweenId);
            P.Voice.PlayBundled(LessonVoice.Correct);
            P.PopIn(next);
            _succeeded = true;
            _phase = Phase.Done;
        }

        private void PlayBack()
        {
            if (_phase == Phase.Listening || _mic.Recording == null) return;
            P.StepRoutine(PlayRecording());
        }

        private IEnumerator PlayRecording()
        {
            var clip = _mic.Recording;
            if (clip == null) yield break;
            bool done = false;
            P.Voice.PlayClip(clip, _ => done = true);
            float t = 0;
            while (!done)
            {
                if (!P.Paused) t += Time.unscaledDeltaTime;
                PaintWave(Mathf.Clamp01(t / clip.length));
                if (meJaw.enabled) meJaw.Open = _mic.Envelope.Length > 0
                    ? _mic.Envelope[Mathf.Min(_mic.Envelope.Length - 1, (int)(t / clip.length * _mic.Envelope.Length))]
                    : 0;
                yield return null;
            }
            meJaw.Open = 0;
            PaintWave(1f);
        }

        private IEnumerator SpeakBundled(string key)
        {
            bool done = false;
            P.Voice.PlayBundled(key, _ => done = true);
            while (!done) yield return null;
        }

        /// <summary>The mask and the mouth follow the voice; the mask "rejoices" during playback.</summary>
        private IEnumerator FollowLevel()
        {
            var baseScale = sticker.rectTransform.localScale;
            while (_phase == Phase.Listening || _phase == Phase.Playback)
            {
                float level = _phase == Phase.Listening ? _mic.Level : 0f;
                if (_phase == Phase.Listening && meJaw.enabled) meJaw.Open = Mathf.Lerp(meJaw.Open, level, 0.5f);
                float bounce = _happy ? 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 9f)) : level * 0.15f;
                sticker.rectTransform.localScale = Vector3.one * (1f + bounce);
                sticker.rectTransform.localEulerAngles = new Vector3(0, 0, _happy ? 6f * Mathf.Sin(Time.time * 7f) : 0f);
                micPulse.localScale = Vector3.one * (1f + level * 0.35f);
                yield return null;
            }
            sticker.rectTransform.localScale = Vector3.one;
            sticker.rectTransform.localEulerAngles = Vector3.zero;
            if (meJaw.enabled) meJaw.Open = 0;
        }

        private void Pulse(bool on)
        {
            micPulse.gameObject.SetActive(on);
            DOTween.Kill(micPulse);
            var img = micPulse.GetComponent<Image>();
            if (on && img != null)
            {
                var c = img.color; c.a = 0.55f; img.color = c;
                img.DOFade(0.2f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetId(P.TweenId).SetTarget(micPulse);
            }
        }

        private void PaintWave(float progress)
        {
            var env = _mic.Envelope;
            for (int i = 0; i < waveBars.Length; i++)
            {
                float v = env.Length == 0 ? 0.2f : env[i * env.Length / waveBars.Length];
                var bar = waveBars[i];
                bar.sizeDelta = new Vector2(bar.sizeDelta.x, Mathf.Lerp(18f, 92f, v));
                var img = bar.GetComponent<Image>();
                if (img == null) continue;
                bool played = progress >= 0 && (float)i / waveBars.Length <= progress;
                var c = img.color; c.a = progress < 0 || played ? 1f : 0.35f; img.color = c;
            }
        }

        private void StopAttempt()
        {
            if (_attempt != null) { P.StopCoroutine(_attempt); _attempt = null; }
            if (_follow != null) { P.StopCoroutine(_follow); _follow = null; }
            if (sticker != null) { sticker.rectTransform.localScale = Vector3.one; sticker.rectTransform.localEulerAngles = Vector3.zero; }
            if (meJaw != null) meJaw.Open = 0;
            _mic?.Stop();
            Pulse(false);
            if (_phase == Phase.Listening) _phase = Phase.Idle;
        }

        // ─────────────────────────── masks ───────────────────────────

        private void SetMask(int index, bool pop)
        {
            if (masks.Length == 0) { sticker.enabled = false; return; }
            _mask = Mathf.Clamp(index, 0, masks.Length - 1);
            sticker.sprite = masks[_mask];
            sticker.enabled = sticker.sprite != null;
            for (int i = 0; i < maskRings.Length; i++) maskRings[i].SetActive(i == _mask);
            PlaceSticker();
            if (pop)
            {
                sticker.rectTransform.DOKill();
                sticker.rectTransform.localScale = Vector3.one * 0.6f;
                sticker.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetId(P.TweenId);
            }
        }

        /// <summary>The sticker on the face: the avatar's in «Персонаж», a fixed spot in «Рамка».</summary>
        private void PlaceSticker()
        {
            if (sticker == null || sticker.sprite == null || meFrame == null) return;
            var place = MaskPlace[Mathf.Clamp(_mask, 0, MaskPlace.Length - 1)];
            Vector2 top;      // top centre of the head, frame space
            Vector2 head;     // head box size
            var frame = meFrame.rect.size;
            if (meAvatar.gameObject.activeSelf && meAvatar.HeadRect.width > 1f)
            {
                // Avatar space → frame space.
                var r = meAvatar.HeadRect;
                top = meFrame.InverseTransformPoint(meAvatar.transform.TransformPoint(new Vector2(r.center.x, r.yMax)));
                float scale = meAvatar.transform.lossyScale.x / meFrame.lossyScale.x;
                head = r.size * scale;
            }
            else
            {
                // The camera: a child at arm's length — the face a little below the middle, ~30% wide.
                float faceW = frame.x * 0.3f;
                head = new Vector2(faceW * 1.4f, faceW * 1.75f);
                top = new Vector2(0, -frame.y * 0.04f + faceW * 1.05f);
            }
            var rt = sticker.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = place.pivot;
            float w = place.width * head.x;
            float aspect = sticker.sprite.rect.height / sticker.sprite.rect.width;
            rt.sizeDelta = new Vector2(w, w * aspect);
            rt.anchoredPosition = top + new Vector2(place.at.x * head.x, place.at.y * head.y);
        }

        // ─────────────────────────── friend ───────────────────────────

        /// <summary>The friend: a drawn body with the real child's head, or a drawn head while there is none.</summary>
        private void ShowFriend(AvatarView view)
        {
            var art = AvatarArt.Instance;
            if (art == null || view == null) return;
            var body = _step.FriendIsGirl ? art.friendGirl : art.friendBoy;
            var head = P.Picture(_step.friendHead);
            if (head != null)
            {
                view.ShowFigure(body, head, RepeatStep.FriendHeadChin, RepeatStep.FriendHeadFace);
                return;
            }
            // Stand-in: a ready drawn head that matches the body — the last one, so it differs
            // from the child's default head (the first).
            AvatarArt.Head drawn = null;
            for (int i = 0; i < art.kidHeads.Length; i++)
            {
                bool boyHead = i >= art.kidHeads.Length / 2;
                if (_step.FriendIsGirl ? art.kidHeads[i].hijab : boyHead) drawn = art.kidHeads[i];
            }
            view.ShowFigure(body, drawn?.sprite, drawn?.chin ?? new Vector2(0.5f, 1f), 0f);
        }

        // ─────────────────────────── video ───────────────────────────

        private void EnsurePlayer()
        {
            if (_player != null) return;
            // On the step's root, not on videoFrame: the frame is hidden during the scene,
            // and a VideoPlayer on an inactive object refuses to Prepare ahead of time.
            _player = root.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;
            _player.skipOnDrop = true;
            _player.prepareCompleted += OnPrepared;
            _player.loopPointReached += _ => _videoEnded = true;
            _player.errorReceived += (_, msg) => { Debug.LogWarning($"[Lesson] video: {msg}"); _videoEnded = true; };
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
            if (_phase == Phase.Video) vp.Play();
        }

        private void PlayVideo()
        {
            if (_player == null || string.IsNullOrEmpty(_player.url)) { _videoEnded = true; return; }
            _videoEnded = false;
            _phase = Phase.Video;
            P.Voice.Stop();
            if (_player.isPrepared) { _player.time = 0; _player.Play(); }
            else _player.Prepare();
        }

        private void ReplayVideo()
        {
            if (!_hasVideo || _phase == Phase.Playback || P.Paused) return;
            StopAttempt();
            PlayVideo();
            // Until the child has spoken, a replay is followed by a fresh try (the mic waits for the video).
            if (_useMic && !_succeeded) P.StepRoutine(ListenAfterVideo());
        }

        private IEnumerator ListenAfterVideo()
        {
            float t = 0;
            while (!_videoEnded && t < 9f) { if (!P.Paused && !_appPaused) t += Time.unscaledDeltaTime; yield return null; }
            _phase = Phase.Idle;
            if (mic.interactable) NewAttempt();
        }

        private void StopVideo()
        {
            if (_player != null && _player.isPlaying) _player.Pause();
            if (_phase == Phase.Video) _phase = Phase.Idle;
        }

        // ─────────────────────────── pause, end ───────────────────────────

        public override void Pause(bool pause)
        {
            if (_player != null && _player.isPrepared)
            {
                if (pause) _player.Pause();
                else if (_phase == Phase.Video && !_videoEnded) _player.Play();
            }
            _camera?.Pause(pause);
            // The exit question: an attempt in progress starts over afterwards.
            if (pause && _phase == Phase.Listening) { StopAttempt(); _resumeListening = true; }
            else if (!pause && _resumeListening) { _resumeListening = false; NewAttempt(); }
        }

        private bool _resumeListening;

        public override void AppPause(bool pause)
        {
            _appPaused = pause;
            if (!P.Paused) Pause(pause);
        }

        public override void End()
        {
            StopAttempt();
            _mic?.Dispose();
            _camera?.Stop();
            DOTween.Kill(micPulse);
            if (_player != null) { _player.Stop(); _player.url = null; }
            video.texture = null;
            if (_texture != null) { _texture.Release(); UnityEngine.Object.Destroy(_texture); _texture = null; }
            _phase = Phase.Idle;
            _resumeListening = false;
            permissions.SetActive(false);
            if (P.segments != null) P.SegmentsBar.SetActive(true);
        }

        /// <summary>Called by the player every frame while the step is shown.</summary>
        public void Tick() => _camera?.Update();

        private IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds) { if (!P.Paused) t += Time.unscaledDeltaTime; yield return null; }
        }

        private static GameObject Holder(Button b) => LessonPlayerScreen.Holder(b);
    }
}
