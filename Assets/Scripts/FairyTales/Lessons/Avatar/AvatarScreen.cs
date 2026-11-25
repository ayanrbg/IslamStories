using System;
using System.Collections;
using System.Linq;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace FairyTales.Lessons
{
    /// <summary>
    /// Making the child's and mum's avatars (spec «Профиль, аватар и семья», design 13):
    /// a photo from the front camera fitted into the oval, or one of 12 ready faces for
    /// the child. Photos are taken behind the parental gate and stay on the phone
    /// (<see cref="AvatarStore"/>). Built by AvatarScreenBuilder.
    /// </summary>
    public class AvatarScreen : BaseScreen
    {
        /// <summary>
        /// Picks a photo from the gallery and hands back a readable texture (or null), or is
        /// unset — then the gallery button is hidden. No gallery plugin ships yet.
        /// </summary>
        public static Action<Action<Texture2D>> PickFromGallery;

        [Header("Main")]
        public Button back;
        public Button tabChild;
        public Button tabMom;
        public Image tabChildBg;
        public Image tabMomBg;
        public TMP_Text tabChildLabel;
        public TMP_Text tabMomLabel;
        public AvatarView preview;
        public TMP_Text title;
        public Button takePhoto;
        public Button gallery;
        public Button removePhoto;
        public GameObject facesBlock;
        public Button[] faces = Array.Empty<Button>();
        public GameObject[] faceSelected = Array.Empty<GameObject>();
        public GameObject momHint;
        public Button done;
        public Color tabOn = new(0.878f, 0.341f, 0.482f);
        public Color tabOffText = new(0.424f, 0.388f, 0.569f);

        [Header("Photo")]
        public GameObject photoPanel;
        public PhotoCropper cropper;
        public TMP_Text photoHint;
        public Button shoot;
        public Button retake;
        public Button savePhoto;
        public Button photoClose;

        private AvatarPerson _person;
        private Type _returnTo;
        private bool _bound;
        private WebCamTexture _cam;
        private Texture2D _shot;
        private Coroutine _camRoutine;
        private bool _mirror;

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
            for (int i = 0; i < faces.Length; i++)
            {
                int index = i;
                faces[i].onClick.AddListener(() => { AvatarStore.SetFace(AvatarPerson.Child, index); Paint(); });
            }
            shoot.onClick.AddListener(Shoot);
            retake.onClick.AddListener(OpenCamera);
            savePhoto.onClick.AddListener(SavePhoto);
            photoClose.onClick.AddListener(ClosePhoto);
        }

        protected override void OnPrepare()
        {
            BindOnce();
            foreach (var label in GetComponentsInChildren<LessonLabel>(true)) label.Apply();
            photoPanel.SetActive(false);
            SetPerson(_person);
        }

        protected override void OnHidden() => StopCamera(true);

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
            StopCamera(true);
            var sm = FindAnyObjectByType<ScreenManager>();
            if (sm != null) sm.Show(_returnTo ?? typeof(LibraryScreen));
        }

        // ─────────────────────────── main ───────────────────────────

        private void SetPerson(AvatarPerson person)
        {
            _person = person;
            preview.person = person;
            tabChildLabel.text = string.IsNullOrWhiteSpace(TaleText.ChildName) ? LessonStrings.T("avatar_child_tab") : TaleText.ChildName;
            Paint();
        }

        private void Paint()
        {
            bool child = _person == AvatarPerson.Child;
            tabChildBg.color = child ? tabOn : Color.clear;
            tabMomBg.color = child ? Color.clear : tabOn;
            tabChildLabel.color = child ? Color.white : tabOffText;
            tabMomLabel.color = child ? tabOffText : Color.white;
            title.text = child ? LessonStrings.AvatarTitleChild() : LessonStrings.T("avatar_title_mom");

            facesBlock.SetActive(child);
            momHint.SetActive(!child);
            int chosen = child ? AvatarStore.FaceIndex(AvatarPerson.Child) : -1;
            for (int i = 0; i < faceSelected.Length; i++) faceSelected[i].SetActive(i == chosen);
            LessonPlayerScreen.SetShown(gallery, PickFromGallery != null);
            removePhoto.gameObject.SetActive(AvatarStore.Get(_person).kind == AvatarStore.KindPhoto);
            preview.Refresh();
        }

        // ─────────────────────────── photo ───────────────────────────

        private void OpenCamera()
        {
            DropShot();
            photoPanel.SetActive(true);
            photoHint.text = LessonStrings.T("avatar_fit");
            LessonPlayerScreen.SetShown(shoot, false);
            LessonPlayerScreen.SetShown(retake, false);
            LessonPlayerScreen.SetShown(savePhoto, false);
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
            if (!allowed || devices.Length == 0)
            {
                photoHint.text = LessonStrings.T("avatar_no_camera");
                yield break;
            }
            var device = devices.FirstOrDefault(d => d.isFrontFacing);
            if (string.IsNullOrEmpty(device.name)) device = devices[0];

            _cam = new WebCamTexture(device.name, 1280, 720, 30);
            _cam.Play();
            // The size is known once the first frame arrives (16×16 before that).
            float t = 0;
            while (_cam.width <= 16 && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            if (_cam.width <= 16)
            {
                photoHint.text = LessonStrings.T("avatar_no_camera");
                StopCamera(false);
                yield break;
            }
            _mirror = device.isFrontFacing;
            cropper.ShowCamera(_cam, _cam.videoRotationAngle, _mirror, _cam.videoVerticallyMirrored);
            LessonPlayerScreen.SetShown(shoot, true);
            _camRoutine = null;
        }

        private void Shoot()
        {
            if (_cam == null || !_cam.isPlaying) return;
            _shot = Upright(_cam, _cam.videoRotationAngle, _mirror, _cam.videoVerticallyMirrored);
            StopCamera(false);
            ShowShot();
        }

        private void OpenGallery()
        {
            PickFromGallery?.Invoke(tex =>
            {
                if (tex == null) return;
                DropShot();
                _shot = tex;
                photoPanel.SetActive(true);
                photoHint.text = LessonStrings.T("avatar_fit");
                ShowShot();
            });
        }

        private void ShowShot()
        {
            cropper.SetPhoto(_shot);
            LessonPlayerScreen.SetShown(shoot, false);
            LessonPlayerScreen.SetShown(retake, true);
            LessonPlayerScreen.SetShown(savePhoto, true);
        }

        private void SavePhoto()
        {
            var face = cropper.Crop();
            if (face == null) return;
            AvatarStore.SetPhoto(_person, face);
            Destroy(face);
            ClosePhoto();
            Paint();
        }

        private void ClosePhoto()
        {
            StopCamera(true);
            photoPanel.SetActive(false);
        }

        private void StopCamera(bool dropShot)
        {
            if (_camRoutine != null) { StopCoroutine(_camRoutine); _camRoutine = null; }
            if (_cam != null)
            {
                _cam.Stop();
                Destroy(_cam);
                _cam = null;
            }
            if (cropper != null && cropper.photo != null && _shot == null) cropper.photo.texture = null;
            if (dropShot) DropShot();
        }

        /// <summary>The camera frames are never kept: the shot lives only until saved or dropped.</summary>
        private void DropShot()
        {
            if (_shot == null) return;
            if (cropper != null && cropper.photo != null) cropper.photo.texture = null;
            Destroy(_shot);
            _shot = null;
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
                // The same flips the preview applies to the texture (PhotoCropper.Flips).
                var (flipU, flipV) = PhotoCropper.Flips(angle, mirror, flipY);
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
