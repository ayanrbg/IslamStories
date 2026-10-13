using System;
using System.Globalization;
using UnityEngine;

namespace FairyTales.UI.Core
{
    public static partial class Loc
    {
        /// <summary>
        /// Every language this build can show — the codes GET /api/languages uses. The screen
        /// lists the intersection with what the server actually serves (<see cref="LanguageCatalog"/>).
        /// A language is here only once the app has its UI strings and can draw its script;
        /// Bengali also needs the native HarfBuzz shaper, which iOS doesn't ship yet, so
        /// there <see cref="IsSupported"/> leaves it out.
        /// </summary>
        public static readonly string[] AllLangs =
            { "ru", "kz", "en-US", "en-GB", "uz", "id", "tr", "ms", "fr", "ar", "ur", "bn" };

        public static bool IsSupported(string lang) =>
            Array.IndexOf(AllLangs, lang) >= 0 && (lang != "bn" || BengaliShaper.Available);

        /// <summary>Written right to left (GET /api/languages flags the same two as "rtl").</summary>
        public static bool IsRtl(string lang) => lang is "ar" or "ur";

        /// <summary>
        /// The key of a language in the UI string tables. US and UK English differ in the
        /// tales (spelling, Mom/Mum, voices), not in the menus, so they share "en".
        /// </summary>
        public static string StringsLang(string lang) => lang is "en-US" or "en-GB" ? "en" : lang;

        /// <summary>
        /// Puts a stored preference onto a code this build still offers. Runs before any scene
        /// object reads it, because half the callers read PlayerPrefs["ft_lang"] directly
        /// rather than going through <see cref="Lang"/>.
        ///   "en" — builds before the US/UK split. The server already moved these profiles
        ///          to en-US, so the device follows instead of guessing a region.
        ///   anything else unsupported — onto English, or the picker could never show it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void MigrateStoredLang()
        {
            if (!PlayerPrefs.HasKey("ft_lang")) return;
            var stored = PlayerPrefs.GetString("ft_lang");
            if (IsSupported(stored)) return;
            PlayerPrefs.SetString("ft_lang", stored == "en" ? "en-US" : DefaultEnglish());
            PlayerPrefs.Save();
        }

        /// <summary>First launch: the device language, when the app has it.</summary>
        public static string DetectSystemLang()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Russian => "ru",
                SystemLanguage.English => DefaultEnglish(),
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.French => "fr",
                SystemLanguage.Arabic => "ar",
                _ => "ru"
            };
        }

        /// <summary>British English for a UK device, American for everyone else.</summary>
        private static string DefaultEnglish()
        {
            var region = DeviceRegion();
            return region is "GB" or "UK" ? "en-GB" : "en-US";
        }

        /// <summary>
        /// Region of the device locale ("GB" of en_GB). Unity only knows the language.
        /// Android: java.util.Locale. Elsewhere the .NET culture, which IL2CPP fills from the OS.
        /// </summary>
        private static string DeviceRegion()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var locale = new AndroidJavaClass("java.util.Locale");
                using var current = locale.CallStatic<AndroidJavaObject>("getDefault");
                return current.Call<string>("getCountry")?.ToUpperInvariant() ?? "";
#else
                var name = CultureInfo.CurrentCulture.Name;
                int dash = name.IndexOf('-');
                return dash > 0 ? name.Substring(dash + 1).ToUpperInvariant() : "";
#endif
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
