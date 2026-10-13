using System.IO;
using UnityEngine;

namespace FairyTales.Cache
{
    /// <summary>
    /// Cached tale text and the server's contentVersion. The server raises a tale's version
    /// whenever its text changes (warm_bread, 05.10.2026: pages 33, 35, 36 in every
    /// language). A cached translation of another version is set aside, not deleted:
    /// online the reader fetches the new text, offline it still opens the old one.
    /// </summary>
    public static partial class AssetCache
    {
        private const string StaleSuffix = ".stale";
        private const string TextVersionPrefix = "ft_text_v_";

        public static void SaveTaleText(string taleId, string lang, byte[] json)
        {
            Save(TaleTextKey(taleId, lang), json);
            var stale = GetPath(TaleTextKey(taleId, lang)) + StaleSuffix;
            if (File.Exists(stale)) File.Delete(stale);
        }

        /// <summary>The set-aside text of an older version; the reader's last resort offline.</summary>
        public static string LoadStaleTaleText(string taleId, string lang)
        {
            var path = GetPath(TaleTextKey(taleId, lang)) + StaleSuffix;
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>
        /// Call with every tale of a FRESH server catalog. A tale seen for the first time
        /// counts as changed too: builds before this one cached text without its version.
        /// Narration made from the old text reads words no longer on the page, so it goes —
        /// but only on a real change, not on that first sighting.
        /// </summary>
        public static void ReconcileTextVersion(string taleId, int version)
        {
            if (string.IsNullOrEmpty(taleId) || version <= 0) return;
            var key = TextVersionPrefix + taleId;
            int known = PlayerPrefs.GetInt(key, 0);
            if (known == version) return;

            SetTextAside(taleId);
            if (known != 0) ClearTaleNarration(taleId);
            PlayerPrefs.SetInt(key, version);
        }

        private static void SetTextAside(string taleId)
        {
            var dir = Path.Combine(Root, "tales-text", taleId);
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var stale = file + StaleSuffix;
                if (File.Exists(stale)) File.Delete(stale);
                File.Move(file, stale);
            }
        }

        /// <summary>AI and TTS narration of one tale, every language and voice.</summary>
        private static void ClearTaleNarration(string taleId)
        {
            foreach (var sub in new[] { "ai-narration", "tts-narration", "narration" })
            {
                var dir = Path.Combine(Root, sub, taleId);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// One-off for the 05.10.2026 server update, which first sighting can't cover for
        /// narration: warm_bread's text was reloaded without a version bump, and English
        /// split into en-US / en-GB, so nothing is ever read under "en" again. Old English
        /// text becomes en-US's set-aside copy (the server moved "en" users to en-US), so a
        /// downloaded tale still opens offline.
        /// </summary>
        private static void PurgeOctober2026Changes()
        {
            const string flag = "ft_cache_2026_10";
            if (PlayerPrefs.GetInt(flag, 0) == 1) return;

            ClearTaleNarration("warm_bread");
            foreach (var sub in new[] { "tales-text", "ai-narration", "tts-narration", "narration" })
            {
                var root = Path.Combine(Root, sub);
                if (!Directory.Exists(root)) continue;
                foreach (var taleDir in Directory.GetDirectories(root))
                {
                    var en = Path.Combine(taleDir, "en");
                    if (Directory.Exists(en)) Directory.Delete(en, true);
                    var enText = Path.Combine(taleDir, "en.json");
                    if (!File.Exists(enText)) continue;
                    var aside = Path.Combine(taleDir, "en-US.json" + StaleSuffix);
                    if (File.Exists(aside)) File.Delete(aside);
                    File.Move(enText, aside);
                }
            }
            PlayerPrefs.SetInt(flag, 1);
            PlayerPrefs.Save();
        }
    }
}
