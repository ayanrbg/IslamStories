using System.Text.RegularExpressions;
using FairyTales.Models;
using UnityEngine;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// Single place that turns a raw tale template into readable text.
    /// Templates contain {childName} and {m:...|f:...}; the name can sit INSIDE
    /// a gender block, so the name is always substituted first.
    /// </summary>
    public static class TaleText
    {
        private static readonly Regex GenderTag =
            new(@"\{m:([^|]*)\|f:([^}]*)\}", RegexOptions.Compiled);

        public static string ChildName => UserProfile.ChildName;
        public static string ChildGender => UserProfile.ChildGender;

        public static string ResolveName(string text)
            => string.IsNullOrEmpty(text) ? text : text.Replace("{childName}", ChildName);

        public static string ResolveGender(string text, string gender)
        {
            if (string.IsNullOrEmpty(text)) return text;
            bool isMale = gender != "female";
            return GenderTag.Replace(text, m => isMale ? m.Groups[1].Value : m.Groups[2].Value);
        }

        /// <summary>Fully resolved text — what the child sees and what TTS must read.</summary>
        public static string Resolve(string text)
            => ResolveGender(ResolveName(text), ChildGender);

        /// <summary>
        /// The language every narration path must use: the language the pages on
        /// screen are REALLY in, not the UI language. Text can legitimately fall
        /// back to another translation (no tale_en.json → Russian pages), and
        /// narrating that with the UI language would speak Russian text with an
        /// English voice and cache it under the English key — so a later switch
        /// to Russian would regenerate what we already have.
        /// </summary>
        public static string SpokenLang(TaleDetail tale)
            => string.IsNullOrEmpty(tale?.lang) ? Loc.Lang : tale.lang;
    }
}
