#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace FairyTales.Lessons.EditorTools
{
    /// <summary>
    /// Nooria-only Xcode tweaks for the lessons (IOSBuildPostProcessor is shared with Bala):
    /// <list type="bullet">
    /// <item>links PhotosUI for the avatar photo picker (Plugins/iOS/NooriaPhotoPicker.mm).
    /// PHPicker needs no NSPhotoLibraryUsageDescription — the app never reads the library itself;</item>
    /// <item>camera and microphone prompts in every app language: &lt;lang&gt;.lproj/InfoPlist.strings
    /// plus CFBundleLocalizations. The English text is in Player Settings (Info.plist).</item>
    /// </list>
    /// Bengali is left out: the app does not offer it on iOS (TextMeshPro can't shape it there).
    /// </summary>
    public static class LessonsIOSPostProcess
    {
        // lproj code → (camera, microphone)
        private static readonly Dictionary<string, (string camera, string mic)> Prompts = new()
        {
            ["en"] = (
                "Islam Stories uses the camera in lessons: your child sees themselves next to a friend with fun masks and can make an avatar from a photo. Video is not recorded, photos stay on this device.",
                "Islam Stories uses the microphone so your child can repeat words in lessons and hear themselves, and so a parent can record their voice for tales."),
            ["ru"] = (
                "Камера нужна в уроках: ребёнок видит себя рядом с другом в забавной маске и может сделать аватар по фото. Видео не записывается, фото остаётся на устройстве.",
                "Микрофон нужен, чтобы ребёнок повторял слова в уроках и слышал себя, а родитель мог записать свой голос для сказок."),
            ["kk"] = (
                "Камера сабақтарда керек: бала өзін досының қасында қызық маскамен көреді және фотосуреттен аватар жасай алады. Бейне жазылмайды, фото құрылғыда қалады.",
                "Микрофон бала сабақта сөздерді қайталап, өз дауысын естуі үшін және ата-ана ертегіге өз дауысын жазуы үшін керек."),
            ["ar"] = (
                "يستخدم التطبيق الكاميرا في الدروس: يرى طفلك نفسه بجانب صديقه بأقنعة مرحة، ويمكنه صنع صورة رمزية من صورة. لا يُسجَّل الفيديو، وتبقى الصور على هذا الجهاز.",
                "يستخدم التطبيق الميكروفون ليكرر طفلك الكلمات في الدروس ويستمع إلى صوته، وليسجّل أحد الوالدين صوته لقراءة القصص."),
            ["tr"] = (
                "Uygulama derslerde kamerayı kullanır: çocuğunuz kendini eğlenceli maskelerle arkadaşının yanında görür ve bir fotoğraftan avatar oluşturabilir. Video kaydedilmez, fotoğraflar bu cihazda kalır.",
                "Uygulama mikrofonu, çocuğunuz derslerde kelimeleri tekrar edip kendi sesini duyabilsin ve ebeveyn masallar için sesini kaydedebilsin diye kullanır."),
            ["ms"] = (
                "Aplikasi menggunakan kamera dalam pelajaran: anak anda melihat dirinya di sebelah rakan dengan topeng yang menyeronokkan dan boleh membuat avatar daripada foto. Video tidak dirakam, foto kekal dalam peranti ini.",
                "Aplikasi menggunakan mikrofon supaya anak anda boleh mengulang perkataan dalam pelajaran dan mendengar suaranya sendiri, dan supaya ibu bapa boleh merakam suara mereka untuk cerita."),
            ["id"] = (
                "Aplikasi memakai kamera dalam pelajaran: anak Anda melihat dirinya di samping teman dengan topeng lucu dan bisa membuat avatar dari foto. Video tidak direkam, foto tetap di perangkat ini.",
                "Aplikasi memakai mikrofon agar anak Anda bisa mengulang kata dalam pelajaran dan mendengar suaranya sendiri, dan agar orang tua bisa merekam suaranya untuk dongeng."),
            ["fr"] = (
                "L'application utilise la caméra pendant les leçons : votre enfant se voit à côté d'un ami avec des masques amusants et peut créer un avatar à partir d'une photo. Aucune vidéo n'est enregistrée, les photos restent sur cet appareil.",
                "L'application utilise le micro pour que votre enfant répète des mots pendant les leçons et s'écoute, et pour qu'un parent enregistre sa voix pour les contes."),
            ["ur"] = (
                "ایپ اسباق میں کیمرہ استعمال کرتی ہے: آپ کا بچہ مزے دار ماسک کے ساتھ خود کو دوست کے ساتھ دیکھتا ہے اور تصویر سے اوتار بنا سکتا ہے۔ ویڈیو ریکارڈ نہیں ہوتی، تصاویر اسی ڈیوائس پر رہتی ہیں۔",
                "ایپ مائیکروفون استعمال کرتی ہے تاکہ آپ کا بچہ اسباق میں الفاظ دہرائے اور اپنی آواز سنے، اور والدین کہانیوں کے لیے اپنی آواز ریکارڈ کر سکیں۔"),
            ["uz"] = (
                "Ilova darslarda kameradan foydalanadi: farzandingiz o‘zini qiziqarli niqoblar bilan do‘sti yonida ko‘radi va suratdan avatar yasay oladi. Video yozilmaydi, suratlar shu qurilmada qoladi.",
                "Ilova mikrofondan farzandingiz darslarda so‘zlarni takrorlab, o‘z ovozini eshitishi va ota-ona ertaklar uchun ovozini yozib olishi uchun foydalanadi."),
        };

        [PostProcessBuild(101)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "PhotosUI.framework", false);

            var main = proj.GetUnityMainTargetGuid();
            foreach (var (lang, (camera, mic)) in Prompts)
            {
                var rel = $"{lang}.lproj/InfoPlist.strings";
                Directory.CreateDirectory(Path.Combine(path, $"{lang}.lproj"));
                File.WriteAllText(Path.Combine(path, rel),
                    $"\"NSCameraUsageDescription\" = \"{Escape(camera)}\";\n" +
                    $"\"NSMicrophoneUsageDescription\" = \"{Escape(mic)}\";\n");
                if (!proj.ContainsFileByProjectPath(rel))
                    proj.AddFileToBuild(main, proj.AddFile(rel, rel, PBXSourceTree.Source));
            }
            proj.WriteToFile(projPath);

            // Without CFBundleLocalizations iOS shows only the development region's prompt.
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var langs = plist.root.CreateArray("CFBundleLocalizations");
            foreach (var lang in Prompts.Keys) langs.AddString(lang);
            plist.WriteToFile(plistPath);
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
