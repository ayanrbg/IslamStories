namespace FairyTales.Lessons
{
    /// <summary>
    /// Which app languages have lessons. The plan: ru, kz and en (en-US and en-GB read
    /// one English text). In the other nine languages the lessons UI is hidden and
    /// the app works as before. Mirrors contentLang() in the server's lessonFormat.js.
    /// </summary>
    public static class LessonLang
    {
        /// <summary>Language of the lesson text for an app language, or null — no lessons there.</summary>
        public static string ContentLang(string appLang) => appLang switch
        {
            "ru" => "ru",
            "kz" => "kz",
            "en" or "en-US" or "en-GB" => "en",
            _ => null,
        };

        /// <summary>True when lessons should be shown at all in this app language.</summary>
        public static bool IsAvailable(string appLang)
            => AppConfig.LessonsEnabled && ContentLang(appLang) != null;
    }
}
