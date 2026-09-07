using UnityEngine;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// System share sheet for a plain string. Unity ships no such API, so each platform
    /// gets its own path; everywhere else the text goes to the clipboard instead, which
    /// is the same outcome one step later and never leaves the user with a dead button.
    /// </summary>
    public static class NativeShare
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _ftShare(string text);
#endif

        /// <summary>
        /// Opens the share sheet. Returns false when the text had to be copied instead,
        /// so the caller can say "copied" rather than claim a sheet appeared.
        /// </summary>
        public static bool Share(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction",
                    intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra",
                    intentClass.GetStatic<string>("EXTRA_TEXT"), text);

                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");

                // The chooser has to be created and shown on the UI thread — starting it
                // from the scripting thread throws on some OEM builds.
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using var chooser = intentClass.CallStatic<AndroidJavaObject>(
                        "createChooser", intent, "");
                    activity.Call("startActivity", chooser);
                }));
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NativeShare] Android share failed: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                _ftShare(text);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NativeShare] iOS share failed: {e.Message}");
            }
#endif

            GUIUtility.systemCopyBuffer = text;
            return false;
        }
    }
}
