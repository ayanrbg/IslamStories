#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>
    /// Nooria-only Xcode tweaks for the lessons (IOSBuildPostProcessor is shared with Bala):
    /// links PhotosUI for the avatar photo picker (Plugins/iOS/NooriaPhotoPicker.mm).
    /// PHPicker needs no NSPhotoLibraryUsageDescription — the app never reads the library itself.
    /// </summary>
    public static class LessonsIOSPostProcess
    {
        [PostProcessBuild(101)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "PhotosUI.framework", false);
            proj.WriteToFile(projPath);
        }
    }
}
#endif
