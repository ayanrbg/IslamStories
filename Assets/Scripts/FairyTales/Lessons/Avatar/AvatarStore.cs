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
    /// A person without a record shows the default drawn head on the body.
    /// </summary>
    public static class AvatarStore
    {
        public const string KindDrawn = "drawn";
        public const string KindFace = "face";
        public const string KindPhoto = "photo";

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

        /// <summary>The cut-out head (transparent around the hair), or null.</summary>
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

        /// <summary>A ready drawn head. Drops the photo: nothing left on the phone that isn't used.</summary>
        public static void SetFace(AvatarPerson person, int index)
        {
            DeletePhotoFile(person);
            var old = Get(person);
            Records[Key(person)] = new AvatarRecord { kind = KindFace, face = index, body = old.kind == KindFace ? old.body : null };
            Save();
        }

        /// <summary>Saves a head cut out of a photo (<see cref="HeadCutter"/>).</summary>
        public static bool SetPhoto(AvatarPerson person, HeadCut cut)
        {
            try
            {
                var path = PhotoPath(person);
                LessonFiles.WriteAtomic(path, cut.Texture.EncodeToPNG());
                ExcludeFromBackup(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Avatar] photo save failed: {e.Message}");
                return false;
            }
            ForgetPhoto(person);
            Records[Key(person)] = new AvatarRecord
            {
                kind = KindPhoto, covered = cut.Covered,
                chinX = cut.Chin.x, chinY = cut.Chin.y, faceWidth = cut.FaceWidth,
            };
            Save();
            return true;
        }

        /// <summary>The girl's body chosen by the parent: "girl", "girl_hijab", or null for automatic.</summary>
        public static void SetBody(AvatarPerson person, string body)
        {
            var r = Get(person);
            r.body = body;
            Records[Key(person)] = r;
            Save();
        }

        /// <summary>Hand fitting of a photo head: size multiplier and offset in head widths.</summary>
        public static void SetFit(AvatarPerson person, float scale, Vector2 offset, bool save = true)
        {
            var r = Get(person);
            r.scale = Mathf.Clamp(scale, 0.6f, 1.6f);
            r.dx = Mathf.Clamp(offset.x, -0.5f, 0.5f);
            r.dy = Mathf.Clamp(offset.y, -0.5f, 0.5f);
            Records[Key(person)] = r;
            if (save) Save();
            else Changed?.Invoke();
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
        /// <summary>"drawn" (default head), "face" (a chosen ready head) or "photo".</summary>
        public string kind = AvatarStore.KindDrawn;
        public int face = -1;
        /// <summary>A girl's body picked by hand: "girl" / "girl_hijab"; null = by the head.</summary>
        public string body;
        // Photo head: where its chin is and how wide the face is (HeadCut), plus hand fitting.
        public bool covered;
        public float chinX = 0.5f, chinY = 0.9f, faceWidth = 0.5f;
        public float scale = 1f, dx, dy;
    }
}
