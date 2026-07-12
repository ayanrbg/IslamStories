#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace FairyTales.Editor
{
    /// <summary>
    /// Post-processes the generated Xcode project for iOS builds.
    ///
    /// Adds <c>ITSAppUsesNonExemptEncryption = NO</c> to Info.plist so App Store
    /// Connect stops asking about export compliance on every upload. The app only
    /// uses standard HTTPS/TLS, which is exempt encryption.
    /// </summary>
    public static class IOSBuildPostProcessor
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = pathToBuiltProject + "/Info.plist";
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            plist.WriteToFile(plistPath);
            Debug.Log("[IOSBuildPostProcessor] Set ITSAppUsesNonExemptEncryption=NO in Info.plist");
        }
    }
}
#endif
