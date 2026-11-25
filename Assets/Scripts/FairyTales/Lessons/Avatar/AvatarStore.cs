using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace FairyTales.Lessons
{
    public enum AvatarPerson
    {
        Child,
        Mom,
    }

    /// <summary>
    /// The family's avatars, on this phone only (spec: photos never go to the server):
    /// <code>
    /// persistentDataPath/avatars/avatars.json   which avatar each person uses
    /// persistentDataPath/avatars/&lt;person&gt;.png   the cut-out face photo
    /// </code>
    /// A person without a record shows the drawn body as it is.
    /// </summary>
    public static class AvatarStore
    {
        public const string KindDrawn = "drawn";
        public const string KindFace = "face";
        public const string KindPhoto = "photo";

        /// <summary>Size of the saved face photo; the oval is inscribed in it.</summary>
        public const int PhotoWidth = 448, PhotoHeight = 512;
        public const float PhotoAspect = (float)PhotoWidth / PhotoHeight;

        public static event Action Changed;

        private static Dictionary<string, AvatarRecord> _records;
        private static readonly Dictionary<AvatarPerson, Sprite> Photos = new();

        private static string Dir => Path.Combine(Application.persistentDataPath, "avatars");
        private static string RecordsPath => Path.Combine(Dir, "avatars.json");
        private static string PhotoPath(AvatarPerson p) => Path.Combine(Dir, Key(p) + ".png");
        private static string Key(AvatarPerson p) => p == AvatarPerson.Mom ? "mom" : "child";

        private static Dictionary<string, AvatarRecord> Records => _records ??= Load();

        // ─────────────────────────── queries ───────────────────────────

        public static AvatarRecord Get(AvatarPerson person)
            => Records.TryGetValue(Key(person), out var r) && r != null ? r : new AvatarRecord();

        public static bool HasPhoto(AvatarPerson person) => Get(person).kind == KindPhoto && PhotoSprite(person) != null;

        /// <summary>Index of the chosen drawn face, or -1.</summary>
        public static int FaceIndex(AvatarPerson person)
        {
            var r = Get(person);
            return r.kind == KindFace ? r.face : -1;
        }

        /// <summary>The cut-out face photo (oval with soft edges), or null.</summary>
        public static Sprite PhotoSprite(AvatarPerson person)
        {
            if (Photos.TryGetValue(person, out var s) && s != null) return s;
            var path = PhotoPath(person);
            if (!File.Exists(path)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                if (!tex.LoadImage(File.ReadAllBytes(path), true)) { UnityEngine.Object.Destroy(tex); return null; }
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                Photos[person] = s;
                return s;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Avatar] photo unreadable: {e.Message}");
                return null;
            }
        }

        // ─────────────────────────── changes ───────────────────────────

        /// <summary>A ready drawn face for the child. Drops the photo: nothing left on the phone that isn't used.</summary>
        public static void SetFace(AvatarPerson person, int index)
        {
            DeletePhotoFile(person);
            Records[Key(person)] = new AvatarRecord { kind = KindFace, face = index };
            Save();
        }

        /// <summary>Saves the cut-out face (<see cref="PhotoWidth"/>×<see cref="PhotoHeight"/>, oval in alpha).</summary>
        public static bool SetPhoto(AvatarPerson person, Texture2D face)
        {
            try
            {
                var path = PhotoPath(person);
                LessonFiles.WriteAtomic(path, face.EncodeToPNG());
                ExcludeFromBackup(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Avatar] photo save failed: {e.Message}");
                return false;
            }
            ForgetPhoto(person);
            Records[Key(person)] = new AvatarRecord { kind = KindPhoto };
            Save();
            return true;
        }

        /// <summary>Back to the drawn body; the photo file is deleted.</summary>
        public static void Reset(AvatarPerson person)
        {
            DeletePhotoFile(person);
            Records.Remove(Key(person));
            Save();
        }

        // ─────────────────────────── io ───────────────────────────

        private static void DeletePhotoFile(AvatarPerson person)
        {
            ForgetPhoto(person);
            try
            {
                var path = PhotoPath(person);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) { Debug.LogWarning($"[Avatar] photo delete failed: {e.Message}"); }
        }

        private static void ForgetPhoto(AvatarPerson person)
        {
            if (!Photos.TryGetValue(person, out var s)) return;
            Photos.Remove(person);
            if (s == null) return;
            UnityEngine.Object.Destroy(s.texture);
            UnityEngine.Object.Destroy(s);
        }

        /// <summary>iOS: keep the photos out of iCloud backups too — they stay on this phone.</summary>
        private static void ExcludeFromBackup(string path)
        {
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
        }

        private static void Save()
        {
            try
            {
                LessonFiles.WriteAtomic(RecordsPath, JsonConvert.SerializeObject(Records));
                ExcludeFromBackup(RecordsPath);
            }
            catch (Exception e) { Debug.LogWarning($"[Avatar] save failed: {e.Message}"); }
            Changed?.Invoke();
        }

        private static Dictionary<string, AvatarRecord> Load()
        {
            try
            {
                if (File.Exists(RecordsPath))
                    return JsonConvert.DeserializeObject<Dictionary<string, AvatarRecord>>(File.ReadAllText(RecordsPath))
                           ?? new Dictionary<string, AvatarRecord>();
            }
            catch (Exception e) { Debug.LogWarning($"[Avatar] avatars.json unreadable: {e.Message}"); }
            return new Dictionary<string, AvatarRecord>();
        }

        /// <summary>Forget everything loaded (tests).</summary>
        internal static void ResetCache()
        {
            _records = null;
            foreach (var p in new List<AvatarPerson>(Photos.Keys)) ForgetPhoto(p);
        }
    }

    [Serializable, Preserve]
    public class AvatarRecord
    {
        /// <summary>"drawn" (the body as drawn), "face" (a ready face) or "photo".</summary>
        public string kind = AvatarStore.KindDrawn;
        public int face = -1;
    }
}
