using System;
using System.Collections.Generic;

namespace FairyTales.Models
{
    [Serializable]
    public class TaleSummary
    {
        public string id;
        public string title;
        public string lang;
        public string file;
        public bool hasDefaultNarration;
        public bool free;
        public string coverUrl;
        public bool bundled;
        public long downloadSize;
        public bool comingSoon;
        public Dictionary<string, string> titles;

        /// <summary>
        /// Server-driven lifecycle: null/"active" = normal, "hidden" = don't show but keep
        /// local cache, "removed" = don't show AND purge local cache.
        /// </summary>
        public string status;

        public bool IsRemoved => status == "removed";
        public bool IsHidden => status == "hidden" || status == "removed";

        /// <summary>
        /// Returns the title for the given language, falling back to <see cref="title"/>.
        /// </summary>
        public string GetTitle(string langCode)
        {
            if (titles != null && titles.TryGetValue(langCode, out var t))
                return t;
            return title;
        }
    }

    [Serializable]
    public class TaleDetail
    {
        public string id;
        public string title;
        public string lang;
        public bool free;
        public int totalPages;
        public string[] pages;
        public int[] genderedPages;
        public bool bundled;
        public long downloadSize;
        public bool comingSoon;
    }

    [Serializable]
    public class NarrateAllRequest
    {
        public string name;
        public string gender;
        public string voice; // null = user's cloned voice, "narrator" = Edge TTS
    }

    /// <summary>
    /// Full payload for NarrateAll — supports sending pages for bundled tales.
    /// Serialized via Newtonsoft (NullValueHandling.Ignore).
    /// </summary>
    [Serializable]
    public class NarrateAllPayload
    {
        public string name;
        public string gender;
        public string voice;
        public string narratorGender;
        public string lang;
        public string[] pages; // client pages for bundled tales
    }

    [Serializable]
    public class PersonalizeRequest
    {
        public string name;
        public string gender;
    }

    [Serializable]
    public class PersonalizeResponse
    {
        public string[] pages;
    }

    [Serializable]
    public class DefaultNarrationInfo
    {
        public bool available;
        public string lang;
        public int[] pages;
    }
}
