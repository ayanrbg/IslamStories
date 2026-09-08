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
    ///
    /// SANCTIONED EXCEPTIONS (owner's decision, 2026-09-08). Four files are allowed to
    /// stay different and must NOT be "aligned" by copying either way:
    ///
    ///   IAP/IAPManager.cs, UI/Payment/PaymentScreen.cs, Editor/PaymentSetup.cs,
    ///   and the paywall keys in UI/Core/Loc.cs
    ///     (trial_note, auto_renew_ios, auto_renew_android, iap_unavailable, retry).
    ///
    /// Islam Stories went through App Store rejections that Bala never did, so its
    /// versions carry things Bala has no equivalent for: the Guideline 3.1.2(c) trial
    /// and auto-renew disclosures, the store-unavailable strip with a retry, and the
    /// stalled-purchase watchdog. Above all it maps store product ids through
    /// StoreSpecificIds — without that, this app asks App Store Connect for
    /// "fairytales_monthly", which does not exist under com.mozz.islamStories, and iOS
    /// purchases stop working entirely.
    ///
    /// The parental gate is NOT one of the differences: both apps have it, Bala on the
    /// purchase and restore buttons, Islam Stories on the door to the paywall.
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

        // ── Push ────────────────────────────────────────────
        // Android notification channel. Must match BOTH the server's channel_id and
        // Assets/Plugins/Android/AndroidManifest.xml — a push aimed at a channel the app
        // never created shows no banner at all on Android 8+.
        public const string PushChannelId = "islamstories_default";
    }
}
