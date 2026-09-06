namespace FairyTales
{
    /// <summary>
    /// Everything that differs between the twin apps built from this codebase
    /// (Bala Stories / Islam Stories). This file is the ONLY one that is allowed to
    /// differ — every other script under Assets/Scripts/FairyTales must stay
    /// byte-identical across the repos so a fix can be ported by copying the file.
    ///
    /// Adding a brand-specific literal anywhere else re-forks the codebase; put it
    /// here instead.
    /// </summary>
    public static class AppConfig
    {
        // ── Backend ─────────────────────────────────────────
        public const string BaseUrl = "https://islam-stories.apiapp.kz:3003";

        // ── Store products ──────────────────────────────────
        // The unified ids the app uses internally; Google Play uses them as-is.
        public const string ProductMonthly = "fairytales_monthly";
        public const string ProductYearly = "fairytales_yearly";

        // App Store Connect ids. ASC ids cannot be renamed after creation, so here
        // they do NOT match the unified ids above — "fairytales_*" is only the
        // reference name in ASC.
        public const string AppleProductMonthly = "com.mozz.islamStories";
        public const string AppleProductYearly = "com.mozz.islamStoriesYearly";

        // ── Legal / support ─────────────────────────────────
        public const string TermsUrl =
            "https://docs.google.com/document/d/1I3Z1ti75XDantrKWCORtkolTmVfrgxTw3Tw45lg2VTY/edit?usp=sharing";
        public const string PrivacyUrl =
            "https://docs.google.com/document/d/1SfqqaObOdnbG0W0ArddX9XUsWhWZt7LjFybnL7t_11o/edit?usp=sharing";
        public const string SupportEmail = "support@fairytales.app";
    }
}
