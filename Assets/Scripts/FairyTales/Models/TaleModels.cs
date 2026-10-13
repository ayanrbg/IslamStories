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
        /// <summary>
        /// Unlocked for THIS user (blogger promo code or an admin grant). Personal —
        /// never cache it across userIds. Unrelated to <see cref="free"/>, which is a
        /// catalog property shared by everyone.
        /// </summary>
        public bool unlocked;
        /// <summary>
        /// Server-marked "invite a friend and this one is yours" tale. Purely a badge +
        /// a different tap target — the tale is still locked by <see cref="IsLocked"/>
        /// until the friend redeems the code and the server flips <see cref="unlocked"/>.
        /// </summary>
        public bool gift;
        public string coverUrl;
        public bool bundled;
        public long downloadSize;
        public bool comingSoon;
        public Dictionary<string, string> titles;
        /// <summary>Raised by the server whenever the tale's text changes (AssetCache.ReconcileTextVersion).</summary>
        public int contentVersion;

        /// <summary>
        /// Server-driven lifecycle: null/"active" = normal, "hidden" = don't show but keep
        /// local cache, "removed" = don't show AND purge local cache.
        /// </summary>
        public string status;

        public bool IsRemoved => status == "removed";
        public bool IsHidden => status == "hidden" || status == "removed";

        /// <summary>The single lock formula: paid tale, no subscription, not personally unlocked.</summary>
        public bool IsLocked(bool subscribed) => !free && !subscribed && !unlocked;

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

    /// <summary>
    /// Body of POST /tales/{id}/narrate. A named class ON PURPOSE: the same payload
    /// built from an anonymous type serialized to "{}" on device (build 2.6, iOS).
    /// Newtonsoft reflects over the type's properties, and IL2CPP managed stripping
    /// drops the metadata of an anonymous type nothing else references — so every
    /// field vanished, the server saw no narratorGender and spoke male whatever the
    /// user picked. In the Editor (Mono, no stripping) the identical code was fine,
    /// which is why it survived testing. [Preserve] keeps this one safe even if the
    /// stripping level is raised later.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.Preserve]
    public class NarratePayload
    {
        public string text;
        public string narratorGender;
        public string name;
        public string gender;
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
