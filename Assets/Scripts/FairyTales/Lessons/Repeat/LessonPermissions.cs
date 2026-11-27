using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace FairyTales.Lessons
{
    /// <summary>
    /// Camera and microphone in lessons (spec §4, «Разрешения»). Before the first system
    /// prompt the parent sees our own screen; their answer is remembered here:
    /// «Разрешить» asks the system for both, «Без камеры» for the microphone only.
    /// What the system then allows is read live every time — the parent may change it in
    /// the phone settings. Release 2 adds switches in «Родителям» on top of the same choice.
    /// </summary>
    public static class LessonPermissions
    {
        public enum Choice
        {
            /// <summary>The parent screen hasn't been answered yet.</summary>
            None = 0,
            Allow = 1,
            NoCamera = 2,
        }

        private const string Key = "lessons.av_choice";

        public static Choice Chosen
        {
            get => (Choice)PlayerPrefs.GetInt(Key, 0);
            set { PlayerPrefs.SetInt(Key, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>The parent screen should be shown before the step.</summary>
        public static bool NeedsAsking => Chosen == Choice.None;

        public static bool CameraGranted
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_ANDROID
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
                return Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
            }
        }

        public static bool MicGranted
        {
            get
            {
#if UNITY_EDITOR
                return true;
#elif UNITY_ANDROID
                return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
                return Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
            }
        }

        /// <summary>Camera for the step: the parent allowed it, the system too, and there is a camera.</summary>
        public static bool UseCamera => Chosen == Choice.Allow && CameraGranted && WebCamTexture.devices.Length > 0;

        /// <summary>Microphone for the step (either choice keeps it).</summary>
        public static bool UseMic => Chosen != Choice.None && MicGranted && LessonMic.HasDevice;

        /// <summary>Records the parent's answer and asks the system for what it covers.</summary>
        public static IEnumerator Request(Choice choice, Action done)
        {
            Chosen = choice;
            if (choice == Choice.Allow) yield return Ask(camera: true);
            yield return Ask(camera: false);
            // The parent's choice and what the system then granted.
            if (choice == Choice.Allow)
                Diagnostics.Analytics.Log("camera_permission", ("granted", CameraGranted),
                    ("has_camera", WebCamTexture.devices.Length > 0));
            Diagnostics.Analytics.Log("mic_permission", ("choice", choice.ToString().ToLowerInvariant()),
                ("granted", MicGranted), ("has_mic", LessonMic.HasDevice));
            done?.Invoke();
        }

        /// <summary>
        /// Asks again for what the parent allowed but the system doesn't have yet (the first
        /// prompt was dismissed). Android won't show it again after "don't ask again" — fine.
        /// </summary>
        public static IEnumerator Refresh()
        {
            if (Chosen == Choice.Allow && !CameraGranted) yield return Ask(camera: true);
            if (Chosen != Choice.None && !MicGranted) yield return Ask(camera: false);
        }

        private static IEnumerator Ask(bool camera)
        {
#if UNITY_EDITOR
            yield break;
#elif UNITY_ANDROID
            var perm = camera ? Permission.Camera : Permission.Microphone;
            if (Permission.HasUserAuthorizedPermission(perm)) yield break;
            bool done = false;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => done = true;
            cb.PermissionDenied += _ => done = true;
            cb.PermissionDeniedAndDontAskAgain += _ => done = true;
            Permission.RequestUserPermission(perm, cb);
            float t = 0;
            while (!done && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
#else
            var what = camera ? UserAuthorization.WebCam : UserAuthorization.Microphone;
            if (!Application.HasUserAuthorization(what)) yield return Application.RequestUserAuthorization(what);
#endif
        }
    }
}
