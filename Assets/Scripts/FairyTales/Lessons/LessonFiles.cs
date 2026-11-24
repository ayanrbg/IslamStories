using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FairyTales.Lessons
{
    /// <summary>File helpers shared by the lesson cache and the progress store.</summary>
    internal static class LessonFiles
    {
        private static readonly Regex SafeName = new(@"^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

        /// <summary>
        /// Writes through a temp file and a rename, so a crash or a full disk leaves
        /// either the old file or the new one, never half of it. The previous version
        /// is kept as <c>.bak</c> when <paramref name="keepBackup"/> is set.
        /// </summary>
        public static void WriteAtomic(string path, byte[] data, bool keepBackup = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }

            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }

            var bak = path + ".bak";
            try
            {
                // One rename on the same volume. Mono implements it on Android and iOS too.
                File.Replace(tmp, path, keepBackup ? bak : null);
            }
            catch (Exception)
            {
                // A file system without replace: keep the old file aside until the new
                // one is in place, so a crash in between still leaves a readable copy.
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
                File.Move(tmp, path);
                if (!keepBackup) File.Delete(bak);
            }
        }

        public static void WriteAtomic(string path, string text, bool keepBackup = false)
            => WriteAtomic(path, Encoding.UTF8.GetBytes(text), keepBackup);

        /// <summary>Deletes a directory tree; false when it could not (file in use).</summary>
        public static bool TryDeleteDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Last segment of a server path ("/api/lessons/x/media/story_1_boy.webp" →
        /// "story_1_boy.webp"), or null when it is not a plain file name — a path from
        /// the network never reaches the file system unchecked.
        /// </summary>
        public static string FileName(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var q = url.IndexOf('?');
            if (q >= 0) url = url.Substring(0, q);
            var name = url.Substring(url.LastIndexOf('/') + 1);
            return IsSafeName(name) ? name : null;
        }

        public static bool IsSafeName(string name)
            => !string.IsNullOrEmpty(name) && name != "." && name != ".." && SafeName.IsMatch(name);
    }
}
