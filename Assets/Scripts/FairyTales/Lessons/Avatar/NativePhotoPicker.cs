using System;
using System.IO;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace FairyTales.Lessons
{
    /// <summary>
    /// One photo from the phone's gallery, with no storage or Photos permission: the system
    /// pickers hand over only the picture the parent chose (Android: Photo Picker / open
    /// document — Plugins/Android/NooriaPhotoPicker.java; iOS: PHPicker —
    /// Plugins/iOS/NooriaPhotoPicker.mm). The native side writes an upright copy, at most
    /// 1600 px, to the cache and reports its path here. In the editor: a file dialog.
    /// </summary>
    public class NativePhotoPicker : MonoBehaviour
    {
        private const string ObjectName = "NooriaPhotoPicker";

        private static NativePhotoPicker _instance;
        private Action<Texture2D> _done;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void NooriaPickPhoto(string gameObject, string method);
#endif

        public static bool Supported =>
            Application.isEditor || Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>Picks a photo; <paramref name="done"/> gets a readable texture, or null when cancelled.</summary>
        public static void Pick(Action<Texture2D> done)
        {
            if (_instance == null)
            {
                var go = new GameObject(ObjectName);
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<NativePhotoPicker>();
            }
            _instance._done = done;
#if UNITY_EDITOR
            _instance.OnPicked(UnityEditor.EditorUtility.OpenFilePanel("Photo", "", "jpg,jpeg,png"));
#elif UNITY_ANDROID
            using var picker = new AndroidJavaClass("com.nooria.picker.NooriaPhotoPicker");
            picker.CallStatic("pick", ObjectName, nameof(OnPicked));
#elif UNITY_IOS
            NooriaPickPhoto(ObjectName, nameof(OnPicked));
#else
            _instance.OnPicked("");
#endif
        }

        /// <summary>From the native side (UnitySendMessage).</summary>
        public void OnPicked(string path)
        {
            var done = _done;
            _done = null;
            Texture2D tex = null;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(File.ReadAllBytes(path))) { Destroy(tex); tex = null; }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PhotoPicker] {e.Message}");
                    tex = null;
                }
                // The copy in the cache is not kept: the photo lives only until the head is cut out.
                if (!Application.isEditor) { try { File.Delete(path); } catch (Exception) { } }
            }
            done?.Invoke(tex);
        }
    }
}
