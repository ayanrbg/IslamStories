using UnityEngine;

namespace FairyTales
{
    /// <summary>
    /// The three values every narration request needs: who listens (child name and
    /// gender) and who speaks (narrator gender). They used to be read as raw
    /// PlayerPrefs strings in a dozen places and the narrator gender was not
    /// persisted at all — it lived only inside one generation run, so every request
    /// that did not come from the voice panel reached the server without it.
    /// </summary>
    public static class UserProfile
    {
        public static string ChildName => PlayerPrefs.GetString("ft_childName", "");
        public static string ChildGender => PlayerPrefs.GetString("ft_gender", "male");

        /// <summary>
        /// "male" | "female" — the narrator voice. The user's own pick always wins;
        /// until they make one we use the server default, so the voice everyone hears
        /// out of the box can be changed without shipping a build.
        /// </summary>
        public static string NarratorGender
        {
            get => PlayerPrefs.GetString("ft_narratorGender",
                PlayerPrefs.GetString("ft_narratorGenderDefault", "male"));
            set
            {
                if (string.IsNullOrEmpty(value)) return; // parent voice — not a narrator
                PlayerPrefs.SetString("ft_narratorGender", value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Server-provided default — never overrides an explicit choice.</summary>
        public static void SetNarratorDefault(string gender)
        {
            if (gender != "male" && gender != "female") return;
            // The config arrives with every log batch (~4s). Writing an unchanged
            // value would hit the disk for the whole session.
            if (PlayerPrefs.GetString("ft_narratorGenderDefault", "") == gender) return;
            PlayerPrefs.SetString("ft_narratorGenderDefault", gender);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Everything the audio depends on, as one path-safe token. Cached audio
        /// keyed by it is dropped automatically when the name, the child's gender or
        /// the narrator changes — the old take said the old name in the old voice.
        /// FNV-1a, not GetHashCode: the key must survive a restart.
        /// </summary>
        public static string CacheTag => Hash($"{ChildName}|{ChildGender}|{NarratorGender}");

        private static string Hash(string s)
        {
            uint h = 2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return h.ToString("x8");
        }
    }
}
