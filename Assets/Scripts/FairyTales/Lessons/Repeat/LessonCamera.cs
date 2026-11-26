using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// The front camera in the «Рамка» mode of the repeat step: the child's face, upright and
    /// mirrored like a mirror, filling a frame of the interface (cover crop). Frames are only
    /// drawn — nothing is recorded or kept.
    /// </summary>
    public sealed class LessonCamera
    {
        private readonly RawImage _view;
        private readonly RectTransform _frame;
        private WebCamTexture _cam;
        private bool _mirror;
        private int _shownAngle = -1;
        private Vector2Int _shownSize;

        public bool Running => _cam != null && _cam.isPlaying && _cam.width > 16;

        public LessonCamera(RawImage view, RectTransform frame)
        {
            _view = view;
            _frame = frame;
        }

        /// <summary>Starts the camera; <paramref name="done"/> gets false if it gave no picture within 3 s.</summary>
        public IEnumerator Start(System.Action<bool> done)
        {
            Stop();
            var devices = WebCamTexture.devices;
            if (devices.Length == 0) { done(false); yield break; }
            var device = devices.FirstOrDefault(d => d.isFrontFacing);
            if (string.IsNullOrEmpty(device.name)) device = devices[0];
            _mirror = device.isFrontFacing;
            // 640×480 is plenty for a 600 px frame and light on cheap phones.
            _cam = new WebCamTexture(device.name, 640, 480, 30);
            _cam.Play();
            float t = 0;
            while (_cam.width <= 16 && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }
            if (_cam.width <= 16) { Stop(); done(false); yield break; }
            _view.texture = _cam;
            _view.enabled = true;
            Fit();
            done(true);
        }

        /// <summary>Call every frame: phones report the rotation late and change it on turning.</summary>
        public void Update()
        {
            if (!Running) return;
            if (_cam.videoRotationAngle != _shownAngle || _cam.width != _shownSize.x || _cam.height != _shownSize.y) Fit();
        }

        public void Pause(bool pause)
        {
            if (_cam == null) return;
            if (pause && _cam.isPlaying) _cam.Pause();
            else if (!pause && !_cam.isPlaying) _cam.Play();
        }

        public void Stop()
        {
            if (_cam != null)
            {
                _cam.Stop();
                Object.Destroy(_cam);
                _cam = null;
            }
            if (_view != null) { _view.texture = null; _view.enabled = false; }
            _shownAngle = -1;
        }

        private void Fit()
        {
            int angle = _cam.videoRotationAngle;
            _shownAngle = angle;
            _shownSize = new Vector2Int(_cam.width, _cam.height);
            var (flipU, flipV) = PhotoCropper.Flips(angle, _mirror, _cam.videoVerticallyMirrored);
            _view.uvRect = new Rect(flipU ? 1 : 0, flipV ? 1 : 0, flipU ? -1 : 1, flipV ? -1 : 1);
            var rt = _view.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localEulerAngles = new Vector3(0, 0, -angle);

            bool sideways = angle % 180 != 0;
            float w = sideways ? _cam.height : _cam.width, h = sideways ? _cam.width : _cam.height;
            var box = _frame.rect.size;
            float k = Mathf.Max(box.x / w, box.y / h);          // cover
            // The RawImage's own rect is in the camera's orientation.
            rt.sizeDelta = sideways ? new Vector2(h * k, w * k) : new Vector2(w * k, h * k);
        }
    }
}
