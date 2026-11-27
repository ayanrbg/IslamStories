using System;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

namespace FairyTales.Lessons
{
    /// <summary>
    /// The «AR» mode of the repeat step (plan section 6): an AR session with the front camera,
    /// only while the child's turn is on screen. The camera picture goes to a texture shown in
    /// the frame of the interface (cover crop, like <see cref="LessonCamera"/>), and the step's flat
    /// mask follows the tracked head: where it is, how big and how tilted (<see cref="TryGetHead"/>).
    ///
    /// The XR loader is off at startup («Initialize XR on Startup» is unchecked) and is brought up
    /// here and torn down in <see cref="Stop"/>, so the tales never pay for AR. Nothing is recorded.
    ///
    /// The session is accepted only when it runs the user-facing camera within 3 s: ARKit on an
    /// iPhone that can't track faces would quietly pick the back camera instead. ARCore that isn't
    /// installed is never offered for installation — a child would get the Play Store; the step
    /// falls back to «Рамка».
    /// </summary>
    public sealed class LessonAR
    {
        /// <summary>The Universal renderer with the AR background feature (Assets/Settings/ARRenderer.asset).</summary>
        public const int RendererIndex = 1;

        private const float StartBudget = 3f;
        // ARFace's origin is behind the nose: a child's head is ~16 cm across (with the hair)
        // and its top ~11 cm above that point.
        private const float HeadHalfWidth = 0.08f;
        private const float HeadTopAbove = 0.11f;
        private const float LostAfter = 0.4f;

        /// <summary>Why AR can't work on this device, once found out — no point in trying every lesson.</summary>
        private static string s_cantWork;
        /// <summary>A phone too slow to start AR in 3 s twice goes straight to «Рамка» after that.</summary>
        private static int s_timeouts;

        private readonly RawImage _view;
        private readonly RectTransform _frame;
        private GameObject _root;
        private ARSession _session;
        private ARCameraManager _cameraManager;
        private ARFaceManager _faces;
        private Camera _camera;
        private RenderTexture _texture;
        private bool _gotFrame;
        private bool _loaderOurs;

        private bool _hasHead;
        private float _lostFor;
        private Vector2 _top, _size;
        private float _roll;

        /// <summary>The outcome of the last <see cref="Start"/>, for analytics: ok, editor, no_loader, unsupported, not_installed, no_faces, back_camera, timeout.</summary>
        public string Result { get; private set; } = "";
        public float StartSeconds { get; private set; }
        public bool Running => _root != null;

        public LessonAR(RawImage view, RectTransform frame)
        {
            _view = view;
            _frame = frame;
        }

        /// <summary>Brings the session up; <paramref name="done"/> gets false (and everything is torn down) if it isn't running the front camera within 3 s.</summary>
        public IEnumerator Start(Action<bool> done)
        {
            Stop();
            float started = Time.realtimeSinceStartup;
            float Elapsed() => Time.realtimeSinceStartup - started;
            bool ok = false;
            try
            {
                if (s_cantWork != null) { Result = s_cantWork; yield break; }
                if (Application.isEditor) { Result = s_cantWork = "editor"; yield break; }

                var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
                if (manager == null) { Result = s_cantWork = "no_loader"; yield break; }
                if (manager.activeLoader == null)
                {
                    _loaderOurs = true; // set first: the step may end while the loader comes up
                    yield return manager.InitializeLoader();
                }
                var loader = manager.activeLoader;
                if (loader == null) { Result = s_cantWork = "no_loader"; yield break; }

                var sessionSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
                if (sessionSubsystem == null) { Result = s_cantWork = "unsupported"; yield break; }
                if (loader.GetLoadedSubsystem<XRFaceSubsystem>() == null) { Result = s_cantWork = "no_faces"; yield break; }

                // The only check that never offers to install ARCore (TryStartAsync with
                // InstallSoftwareIfNeeded, which ARSession uses, would).
#pragma warning disable CS0618
                var availability = sessionSubsystem.GetAvailabilityAsync();
#pragma warning restore CS0618
                while (availability.keepWaiting && Elapsed() < StartBudget) yield return null;
                if (availability.keepWaiting) { TimedOut(); yield break; }
                if (!availability.result.IsSupported()) { Result = s_cantWork = "unsupported"; yield break; }
                if (!availability.result.IsInstalled()) { Result = s_cantWork = "not_installed"; yield break; }

                Build();
                while (!_gotFrame && Elapsed() < StartBudget) yield return null;
                if (!_gotFrame) { TimedOut(); yield break; }
                if (_cameraManager.currentFacingDirection != CameraFacingDirection.User)
                {
                    Result = s_cantWork = "back_camera";
                    yield break;
                }

                _view.texture = _texture;
                _view.uvRect = new Rect(0, 0, 1, 1);
                _view.enabled = true;
                Fit();
                Result = "ok";
                ok = true;
            }
            finally
            {
                StartSeconds = Elapsed();
                if (!ok) Stop();
                done(ok);
            }
        }

        private void TimedOut()
        {
            Result = "timeout";
            if (++s_timeouts >= 2) s_cantWork = "timeout";
        }

        private void Build()
        {
            int h = 720;
            int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)Screen.width / Mathf.Max(1, Screen.height)));
            // The projection ARFoundation sets is made for the screen's shape, so the texture keeps it.
            _texture = new RenderTexture(w, h, 24) { name = "LessonAR" };

            // Inactive while the components are set up: XROrigin would grab Camera.main in Awake.
            _root = new GameObject("LessonAR");
            _root.SetActive(false);

            var sessionGo = new GameObject("Session");
            sessionGo.transform.SetParent(_root.transform, false);
            _session = sessionGo.AddComponent<ARSession>();
            _session.attemptUpdate = false;
            _session.matchFrameRateRequested = false; // keep the app's own frame rate
            _session.requestedTrackingMode = TrackingMode.DontCare;

            var originGo = new GameObject("Origin");
            originGo.transform.SetParent(_root.transform, false);
            var offset = new GameObject("Offset");
            offset.transform.SetParent(originGo.transform, false);
            var cameraGo = new GameObject("ARCamera");
            cameraGo.transform.SetParent(offset.transform, false);

            _camera = cameraGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.cullingMask = 0; // only the camera picture; the mask is drawn by the interface
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 10f;
            _camera.depth = -100;
            _camera.targetTexture = _texture;
            var data = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(RendererIndex);
            data.renderPostProcessing = false;
            data.renderShadows = false;

            _cameraManager = cameraGo.AddComponent<ARCameraManager>();
            _cameraManager.requestedFacingDirection = CameraFacingDirection.User;
            _cameraManager.frameReceived += OnFrame;
            cameraGo.AddComponent<ARCameraBackground>();
#if ENABLE_INPUT_SYSTEM
            var pose = cameraGo.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(new InputAction("ARPosition", binding: "<HandheldARInputDevice>/devicePosition"));
            pose.rotationInput = new InputActionProperty(new InputAction("ARRotation", binding: "<HandheldARInputDevice>/deviceRotation"));
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            pose.ignoreTrackingState = true;
#endif

            var origin = originGo.AddComponent<XROrigin>();
            origin.Camera = _camera;
            origin.CameraFloorOffsetObject = offset;
            _faces = originGo.AddComponent<ARFaceManager>();
            _faces.requestedMaximumFaceCount = 1;

            _root.SetActive(true);
        }

        private void OnFrame(ARCameraFrameEventArgs _) => _gotFrame = true;

        /// <summary>Call every frame: follows the head (smoothed), forgets it after 0.4 s out of sight.</summary>
        public void Update()
        {
            if (_root == null) return;
            if (!Measure(out var top, out var size, out var roll))
            {
                _lostFor += Time.unscaledDeltaTime;
                if (_lostFor > LostAfter) _hasHead = false;
                return;
            }
            _lostFor = 0;
            if (!_hasHead)
            {
                _top = top; _size = size; _roll = roll;
                _hasHead = true;
                return;
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f);
            _top = Vector2.Lerp(_top, top, k);
            _size = Vector2.Lerp(_size, size, k);
            _roll = Mathf.LerpAngle(_roll, roll, k);
        }

        /// <summary>
        /// The head in the frame's space (from its centre, like an anchored position under centre
        /// anchors): the top centre, the box size and the tilt in degrees (counter-clockwise).
        /// </summary>
        public bool TryGetHead(out Vector2 top, out Vector2 size, out float roll)
        {
            top = _top; size = _size; roll = _roll;
            return _root != null && _hasHead;
        }

        private bool Measure(out Vector2 top, out Vector2 size, out float roll)
        {
            top = size = default; roll = 0;
            if (_faces == null || _camera == null) return false;
            ARFace face = null;
            foreach (var f in _faces.trackables)
                if (f.trackingState == TrackingState.Tracking) { face = f; break; }
            if (face == null) return false;

            var t = face.transform;
            var c = t.position;
            if (!ToFrame(c, out var centre) ||
                !ToFrame(c + t.up * HeadTopAbove, out top) ||
                !ToFrame(c - t.right * HeadHalfWidth, out var left) ||
                !ToFrame(c + t.right * HeadHalfWidth, out var right)) return false;

            float width = (right - left).magnitude;
            if (width < 1f) return false;
            // Same proportions as the head box of «Рамка» and the avatar (see RepeatStepView.PlaceSticker).
            size = new Vector2(width, width * 1.25f);
            var up = top - centre;
            roll = Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>A world point → the camera picture → the frame's space.</summary>
        private bool ToFrame(Vector3 world, out Vector2 p)
        {
            var v = _camera.WorldToViewportPoint(world);
            var rt = _view.rectTransform;
            var s = rt.rect.size;
            p = rt.anchoredPosition + new Vector2((v.x - 0.5f) * s.x, (v.y - 0.5f) * s.y);
            return v.z > 0;
        }

        private void Fit()
        {
            var rt = _view.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localEulerAngles = Vector3.zero;
            var box = _frame.rect.size;
            float w = _texture.width, h = _texture.height;
            float k = Mathf.Max(box.x / w, box.y / h); // cover
            rt.sizeDelta = new Vector2(w * k, h * k);
        }

        /// <summary>The exit question or the app going to the background: the camera is let go.</summary>
        public void Pause(bool pause)
        {
            if (_session != null) _session.enabled = !pause;
            if (pause) _hasHead = false;
        }

        public void Stop()
        {
            if (_cameraManager != null) _cameraManager.frameReceived -= OnFrame;
            if (_root != null)
            {
                // Disabled right now (Destroy waits for the frame's end): the managers must stop
                // their subsystems before the loader below destroys them.
                _root.SetActive(false);
                UnityEngine.Object.Destroy(_root);
            }
            _root = null;
            _session = null;
            _cameraManager = null;
            _faces = null;
            _camera = null;
            _gotFrame = false;
            _hasHead = false;
            _lostFor = 0;

            if (_loaderOurs)
            {
                _loaderOurs = false;
                var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
                if (manager != null && manager.activeLoader != null) manager.DeinitializeLoader();
            }
            if (_texture != null)
            {
                if (_view != null && _view.texture == _texture) { _view.texture = null; _view.enabled = false; }
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
        }
    }
}
