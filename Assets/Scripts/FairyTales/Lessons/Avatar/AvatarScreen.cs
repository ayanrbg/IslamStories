using System;
using System.Collections;
using System.Linq;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using TMPro;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace FairyTales.Lessons
{
    /// <summary>
    /// Making the child's and mum's avatars, Speech Blubs style: the real head, hair and all,
    /// cut out of a photo (<see cref="HeadCutter"/>) and stood on a drawn body — or one of
    /// the ready drawn heads. Photos are taken or picked behind the parental gate and stay
    /// on the phone (<see cref="AvatarStore"/>). Built by AvatarScreenBuilder.
    /// </summary>
    public class AvatarScreen : BaseScreen
    {
        /// <summary>Picks a photo from the gallery; set by default to the system picker.</summary>
        public static Action<Action<Texture2D>> PickFromGallery = NativePhotoPicker.Supported ? NativePhotoPicker.Pick : null;

        [Header("Model")]
        public ModelAsset headModel;

        [Header("Main")]
        public Button back;
        public Button tabChild;
        public Button tabMom;
        public Image tabChildBg;
        public Image tabMomBg;
        public TMP_Text tabChildLabel;
        public TMP_Text tabMomLabel;
        public AvatarView preview;
        public AvatarFitter fitter;
        public GameObject fitBlock;
        public Button sizeMinus;
        public Button sizePlus;
        public GameObject bodyBlock;
        public Button bodyPlain;
        public Button bodyHijab;
        public Image bodyPlainBg;
        public Image bodyHijabBg;
        public TMP_Text title;
        public Button takePhoto;
        public Button gallery;
        public Button removePhoto;
        public TMP_Text headsLabel;
        public Button[] heads = Array.Empty<Button>();
        public Image[] headImages = Array.Empty<Image>();
        public GameObject[] headSelected = Array.Empty<GameObject>();
        public Button done;
        public Color tabOn = new(0.878f, 0.341f, 0.482f);
        public Color tabOffText = new(0.424f, 0.388f, 0.569f);
        public Color chipOff = new(0.953f, 0.933f, 1f);

        [Header("Photo")]
        public GameObject photoPanel;
        public PhotoCropper cropper;
        public TMP_Text photoHint;
        public Button shoot;
        public Button retake;
        public Button photoClose;

        private AvatarPerson _person;
        private Type _returnTo;
        private bool _bound;
        private WebCamTexture _cam;
        private Coroutine _camRoutine;
        private bool _mirror;
        private HeadCutter _cutter;
        private bool _busy;

        // ─────────────────────────── opening ───────────────────────────

        public static bool Open(AvatarPerson person = AvatarPerson.Child)
        {
            var sm = FindAnyObjectByType<ScreenManager>();
            var screen = sm != null ? sm.Get<AvatarScreen>() : null;
            if (screen == null) return false;
            screen._person = person;
            screen._returnTo = sm.Current != null && sm.Current != screen ? sm.Current.GetType() : typeof(LibraryScreen);
            sm.ShowOver<AvatarScreen>();
            return true;
        }

        private void BindOnce()
        {
            if (_bound) return;
            _bound = true;
            back.onClick.AddListener(Close);
            done.onClick.AddListener(Close);
            tabChild.onClick.AddListener(() => SetPerson(AvatarPerson.Child));
            tabMom.onClick.AddListener(() => SetPerson(AvatarPerson.Mom));
            takePhoto.onClick.AddListener(() => ChildGatePopup.Show(OpenCamera));
            gallery.onClick.AddListener(() => ChildGatePopup.Show(OpenGallery));
            removePhoto.onClick.AddListener(() => { AvatarStore.Reset(_person); Paint(); });
            sizeMinus.onClick.AddListener(() => { fitter.Resize(1f / 1.08f); fitter.Commit(); });
            sizePlus.onClick.AddListener(() => { fitter.Resize(1.08f); fitter.Commit(); });
            bodyPlain.onClick.AddListener(() => { AvatarStore.SetBody(_person, "girl"); Paint(); });
            bodyHijab.onClick.AddListener(() => { AvatarStore.SetBody(_person, "girl_hijab"); Paint(); });
            for (int i = 0; i < heads.Length; i++)
            {
                int index = i;
                heads[i].onClick.AddListener(() =>
                {
                    AvatarStore.SetFace(_person, index);
                    Diagnostics.Analytics.Log("avatar_created", ("person", _person.ToString().ToLowerInvariant()), ("kind", "face"));
                    Paint();
                });
            }
            shoot.onClick.AddListener(Shoot);
            retake.onClick.AddListener(OpenCamera);
            photoClose.onClick.AddListener(ClosePhoto);
        }

        protected override void OnPrepare()
        {
            BindOnce();
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            photoPanel.SetActive(false);
            SetPerson(_person);
        }

        protected override void OnHidden()
        {
            StopCamera();
            _cutter?.Dispose();
            _cutter = null;
        }

        private void Update()
        {
            if (!IsVisible || !Input.GetKeyDown(KeyCode.Escape)) return;
            var gate = FindAnyObjectByType<ChildGatePopup>();
            if (gate != null && gate.gameObject.activeInHierarchy) return;
            if (photoPanel.activeSelf) ClosePhoto();
            else Close();
        }

        private void Close()
        {
            if (_busy) return;
            StopCamera();
            var sm = FindAnyObjectByType<ScreenManager>();
            if (sm != null) sm.Show(_returnTo ?? typeof(LibraryScreen));
        }

        // ─────────────────────────── main ───────────────────────────

        private void SetPerson(AvatarPerson person)
        {
            _person = person;
            preview.person = person;
            fitter.view = preview;
            tabChildLabel.text = string.IsNullOrWhiteSpace(TaleText.ChildName) ? LessonStrings.T("avatar_child_tab") : TaleText.ChildName;
            Paint();
        }

        private void Paint()
        {
            var art = AvatarArt.Instance;
            bool child = _person == AvatarPerson.Child;
            var record = AvatarStore.Get(_person);
            bool photo = record.kind == AvatarStore.KindPhoto;

            tabChildBg.color = child ? tabOn : Color.clear;
            tabMomBg.color = child ? Color.clear : tabOn;
            tabChildLabel.color = child ? Color.white : tabOffText;
            tabMomLabel.color = child ? tabOffText : Color.white;
            title.text = child ? LessonStrings.AvatarTitleChild() : LessonStrings.T("avatar_title_mom");
            headsLabel.text = LessonStrings.T(child ? "avatar_or_face" : "avatar_or_face_mom");

            var list = art != null ? art.HeadsFor(_person) : Array.Empty<AvatarArt.Head>();
            int chosen = record.kind == AvatarStore.KindFace ? record.face : record.kind == AvatarStore.KindPhoto ? -1 : art?.DefaultHead(_person) ?? 0;
            for (int i = 0; i < heads.Length; i++)
            {
                bool used = i < list.Length;
                heads[i].gameObject.SetActive(used);
                headSelected[i].SetActive(used && i == chosen);
                if (used) headImages[i].sprite = list[i].sprite;
            }

            fitBlock.SetActive(photo);
            removePhoto.gameObject.SetActive(photo);
            bool girl = child && UserProfile.ChildGender == "female";
            bodyBlock.SetActive(girl);
            if (girl && art != null)
            {
                bool hijab = art.BodyFor(_person, record) == art.girlHijab;
                bodyPlainBg.color = hijab ? chipOff : tabOn;
                bodyHijabBg.color = hijab ? tabOn : chipOff;
                bodyPlain.GetComponentInChildren<TMP_Text>().color = hijab ? tabOffText : Color.white;
                bodyHijab.GetComponentInChildren<TMP_Text>().color = hijab ? Color.white : tabOffText;
            }
            LessonPlayerScreen.SetShown(gallery, PickFromGallery != null);
            preview.Refresh();
        }

        // ─────────────────────────── photo ───────────────────────────

        private void OpenCamera()
        {
            photoPanel.SetActive(true);
            photoHint.text = LessonStrings.T("avatar_fit");
            LessonPlayerScreen.SetShown(shoot, false);
            LessonPlayerScreen.SetShown(retake, false);
            if (_camRoutine != null) StopCoroutine(_camRoutine);
            _camRoutine = StartCoroutine(StartCamera());
        }

        private IEnumerator StartCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                bool? answer = null;
                var cb = new PermissionCallbacks();
                cb.PermissionGranted += _ => answer = true;
                cb.PermissionDenied += _ => answer = false;
                cb.PermissionDeniedAndDontAskAgain += _ => answer = false;
                Permission.RequestUserPermission(Permission.Camera, cb);
                while (answer == null) yield return null;
            }
            bool allowed = Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            bool allowed = Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
            var devices = WebCamTexture.devices;
            if (!allowed || devices.Length == 0) { Fail("avatar_no_camera"); yield break; }
            var device = devices.FirstOrDefault(d => d.isFrontFacing);
            if (string.IsNullOrEmpty(device.name)) device = devices[0];

            _cam = new WebCamTexture(device.name, 1280, 720, 30);
            _cam.Play();
            float t = 0;
            while (_cam.width <= 16 && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            if (_cam.width <= 16) { StopCamera(); Fail("avatar_no_camera"); yield break; }
            _mirror = device.isFrontFacing;
            cropper.ShowCamera(_cam, _cam.videoRotationAngle, _mirror, _cam.videoVerticallyMirrored);
            LessonPlayerScreen.SetShown(shoot, true);
            _camRoutine = null;
        }

        private void Shoot()
        {
            if (_cam == null || !_cam.isPlaying || _busy) return;
            var shot = Upright(_cam, _cam.videoRotationAngle, _mirror, _cam.videoVerticallyMirrored);
            StopCamera();
            StartCoroutine(CutAndSave(shot));
        }

        private void OpenGallery()
        {
            PickFromGallery?.Invoke(tex =>
            {
                if (tex == null) return;
                photoPanel.SetActive(true);
                LessonPlayerScreen.SetShown(shoot, false);
                LessonPlayerScreen.SetShown(retake, false);
                cropper.SetPhoto(tex);
                StartCoroutine(CutAndSave(tex));
            });
        }

        /// <summary>Cuts the head out and puts it on the avatar. The photo itself is dropped right after.</summary>
        private IEnumerator CutAndSave(Texture2D photo)
        {
            _busy = true;
            cropper.SetPhoto(photo);
            photoHint.text = LessonStrings.T("avatar_cutting");
            LessonPlayerScreen.SetShown(shoot, false);
            LessonPlayerScreen.SetShown(retake, false);
            yield return null; // let the hint draw before the model runs
            yield return null;

            HeadCut cut = null;
            try
            {
                _cutter ??= new HeadCutter(headModel);
                cut = _cutter.Cut(photo);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Avatar] head cut failed: {e.Message}");
            }
            cropper.photo.texture = null;
            Destroy(photo);
            _busy = false;

            if (cut == null) { Fail("avatar_no_face"); yield break; }
            AvatarStore.SetPhoto(_person, cut);
            Diagnostics.Analytics.Log("avatar_created", ("person", _person.ToString().ToLowerInvariant()), ("kind", "photo"));
            Destroy(cut.Texture);
            photoPanel.SetActive(false);
            Paint();
        }

        private void Fail(string key)
        {
            photoHint.text = LessonStrings.T(key);
            LessonPlayerScreen.SetShown(shoot, false);
            LessonPlayerScreen.SetShown(retake, true);
        }

        private void ClosePhoto()
        {
            if (_busy) return;
            StopCamera();
            photoPanel.SetActive(false);
        }

        private void StopCamera()
        {
            if (_camRoutine != null) { StopCoroutine(_camRoutine); _camRoutine = null; }
            if (_cam != null)
            {
                _cam.Stop();
                Destroy(_cam);
                _cam = null;
            }
            if (cropper != null && cropper.photo != null && !_busy) cropper.photo.texture = null;
        }

        /// <summary>A still of the camera turned upright and mirrored like the preview.</summary>
        private static Texture2D Upright(WebCamTexture cam, int angle, bool mirror, bool flipY)
        {
            int w = cam.width, h = cam.height;
            var src = cam.GetPixels32();
            angle = ((angle % 360) + 360) % 360;
            bool sideways = angle == 90 || angle == 270;
            int ow = sideways ? h : w, oh = sideways ? w : h;
            var dst = new Color32[ow * oh];
            var (flipU, flipV) = PhotoCropper.Flips(angle, mirror, flipY);
            for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                // Output (x, y) is the source turned clockwise by angle (y up).
                int sx, sy;
                switch (angle)
                {
                    case 90: sx = w - 1 - y; sy = x; break;
                    case 180: sx = w - 1 - x; sy = h - 1 - y; break;
                    case 270: sx = y; sy = h - 1 - x; break;
                    default: sx = x; sy = y; break;
                }
                if (flipU) sx = w - 1 - sx;
                if (flipV) sy = h - 1 - sy;
                dst[y * ow + x] = src[sy * w + sx];
            }
            var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(dst);
            tex.Apply();
            return tex;
        }
    }
}
