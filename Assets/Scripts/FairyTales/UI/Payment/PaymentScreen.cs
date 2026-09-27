using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FairyTales.Api;
using FairyTales.Diagnostics;
using FairyTales.IAP;
using FairyTales.UI.Core;

namespace FairyTales.UI.Payment
{
    public class PaymentScreen : BaseScreen
    {
        [Tooltip("The veil drawn over the shared background while the paywall is up.")]
        [SerializeField] private Image background;
        [Tooltip("How dark that veil gets once the screen is in. 0 = the background shows " +
                 "through untouched, 1 = the veil's own colour at full opacity. Editing this " +
                 "also updates the image in the scene, so the editor shows what ships.")]
        [Range(0f, 1f)]
        [SerializeField] private float dimAlpha = 0.25f;
        [SerializeField] private float bgFadeDuration = 0.5f;

        [Header("Background stars")]
        [Tooltip("The shared twinkling stars behind every screen. Found automatically when " +
                 "left empty.")]
        [SerializeField] private StarBackground stars;
        [Tooltip("What the stars dim to while the paywall is open — 0 switches them off. " +
                 "They are restored when this screen closes, so every other screen keeps " +
                 "them exactly as before.")]
        [Range(0f, 1f)]
        [SerializeField] private float starsAlphaWhileOpen;
        [SerializeField] private float starsFadeDuration = 0.4f;
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnMonthly;
        [SerializeField] private Button btnYearly;
        [SerializeField] private Button btnTrial;
        [SerializeField] private Button btnTerms;
        [SerializeField] private Button btnRestore;
        [SerializeField] private Button btnPrivacy;
        [Tooltip("Tick drawn over the radio on the monthly card. Shown only while that plan is selected.")]
        [SerializeField] private GameObject monthlySelect;
        [Tooltip("Same tick on the yearly card.")]
        [SerializeField] private GameObject yearlySelect;
        [SerializeField] private TextMeshProUGUI txtMonthlyPrice;
        [SerializeField] private TextMeshProUGUI txtYearlyPrice;
        [SerializeField] private TextMeshProUGUI txtTrialLabel;

        [Header("Plan card selected / unselected look")]
        [Tooltip("Glow behind the selected card. Enabled for the selected plan only.")]
        [SerializeField] private GameObject monthlyGlow;
        [SerializeField] private GameObject yearlyGlow;
        [Tooltip("Card background sprite while the plan IS selected.")]
        [SerializeField] private Sprite cardSpriteSelected;
        [Tooltip("Card background sprite while the plan is NOT selected.")]
        [SerializeField] private Sprite cardSpriteUnselected;
        [Tooltip("Image the two sprites above are written to. Left empty it resolves to the card button's own Image.")]
        [SerializeField] private Image monthlyCardImage;
        [SerializeField] private Image yearlyCardImage;

        [Header("Yearly saving (derived from live store prices, never hardcoded)")]
        [Tooltip("Struck-through '12 months at the monthly rate' figure on the yearly card.")]
        [SerializeField] private TextMeshProUGUI txtYearlyOldPrice;
        [Tooltip("Colour of the rule drawn through that figure. The figure itself keeps the " +
                 "colour set on the label. Default is sampled from the discount badge so the " +
                 "two reds are the same one.")]
        [SerializeField] private Color oldPriceStrikeColor = new Color32(0xF5, 0x46, 0x33, 0xFF);
        [Tooltip("Discount badge label, e.g. \"-45%\".")]
        [SerializeField] private TextMeshProUGUI txtDiscountBadge;
        [Tooltip("Badge background — hidden together with the label when there is no real saving.")]
        [SerializeField] private GameObject discountBadge;

        [Header("Free trial")]
        [Tooltip("Both plans ship with a 3-day introductory offer, so the trial wording — CTA " +
                 "and the line on the monthly card — is shown without asking the store. Note " +
                 "that a store grants that offer once per subscription group per account: a " +
                 "user who already spent it is charged straight away and still sees the " +
                 "promise. Untick to go back to showing it only when the store confirms the " +
                 "offer is still available for THIS account.")]
        [SerializeField] private bool alwaysOfferTrial = true;

        [Header("Subscription disclosure (Guideline 3.1.2(c))")]
        [Tooltip("Subordinate line under the CTA. Must be smaller and less prominent than the billed amount.")]
        [SerializeField] private TextMeshProUGUI txtTrialNote;
        [Tooltip("Auto-renewal disclaimer shown next to the Terms / Privacy links.")]
        [SerializeField] private TextMeshProUGUI txtAutoRenew;

        [Header("New paywall layout")]
        [Tooltip("Headline. The highlight colour travels inside the localized string.")]
        [SerializeField] private TextMeshProUGUI txtHeadline;
        [Tooltip("Feature titles, left to right — filled from paywall_f1_title … paywall_f4_title.")]
        [SerializeField] private TextMeshProUGUI[] featureTitles;
        [Tooltip("Feature subtitles, same order — filled from paywall_f1_sub … paywall_f4_sub.")]
        [SerializeField] private TextMeshProUGUI[] featureSubtitles;
        [Tooltip("\"Most popular\" badge caption on the monthly card.")]
        [SerializeField] private TextMeshProUGUI txtPopularBadge;
        [Tooltip("\"3 days free, then …\" line inside the monthly card. Hidden when the store offers no trial.")]
        [SerializeField] private TextMeshProUGUI txtMonthlyTrialNote;
        [SerializeField] private TextMeshProUGUI txtTermsLabel;
        [SerializeField] private TextMeshProUGUI txtRestoreLabel;
        [SerializeField] private TextMeshProUGUI txtPrivacyLabel;

        [Header("Promo")]
        [SerializeField] private Button btnPromo;
        [SerializeField] private GameObject promoPanel;
        [SerializeField] private TMP_InputField promoInput;
        [SerializeField] private Button btnPromoApply;
        [SerializeField] private Button btnPromoClose;
        [SerializeField] private TextMeshProUGUI txtPromoError;

        private ScreenManager _screens;
        private PromoService _promoService;
        /// <summary>Keeps the star rows and the link row laid out against the text that is
        /// really on them — see <see cref="LayoutGuard"/> for why one rebuild is not enough.</summary>
        private LayoutGuard _layout;
        private bool _yearlySelected = true;
        private bool _purchased;

        private bool _purchaseInFlight;
        private bool _failureReported;
        private Coroutine _purchaseWatchdog;

        /// <summary>StoreKit can leave a purchase pending (parental approval, interrupted
        /// sheet) without ever calling back. Re-arm the UI after this long so the buttons
        /// can never stay disabled.</summary>
        private const float PurchaseTimeout = 120f;

        /// <summary>
        /// Set before showing to control where Close navigates.
        /// null = LibraryScreen (default).
        /// </summary>
        public System.Type ReturnTo { get; set; }

        /// <summary>Analytics context — where the paywall was opened from (spec §2.5 source).</summary>
        public string Source { get; set; } = "unknown";

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            _layout = LayoutGuard.Attach(gameObject);

            if (!stars) stars = FindAnyObjectByType<StarBackground>(FindObjectsInactive.Include);

            var api = FindAnyObjectByType<ApiClient>();
            Debug.Log($"[Promo] Awake: ApiClient found={api != null}");
            if (api) _promoService = new PromoService(api);

            // The card's background IS the button's target graphic, so the common case
            // needs no extra wiring in the inspector.
            if (!monthlyCardImage && btnMonthly) monthlyCardImage = btnMonthly.targetGraphic as Image;
            if (!yearlyCardImage && btnYearly) yearlyCardImage = btnYearly.targetGraphic as Image;

            if (background) SetBgAlpha(0f);
            if (btnClose) btnClose.onClick.AddListener(OnClose);
            if (btnMonthly) btnMonthly.onClick.AddListener(() => SelectPlan(false));
            if (btnYearly) btnYearly.onClick.AddListener(() => SelectPlan(true));
            if (btnTrial) btnTrial.onClick.AddListener(OnPurchase);
            if (btnTerms) btnTerms.onClick.AddListener(OnTerms);
            if (btnRestore) btnRestore.onClick.AddListener(OnRestore);
            if (btnPrivacy) btnPrivacy.onClick.AddListener(OnPrivacy);
            // The promo field is deliberately identical on both stores. It was iOS-only
            // hidden for a while after a 3.1.1 rejection, but the same flow ships in
            // another of our apps and passes review, and the gift feature is unusable
            // for an invited friend who has nowhere to type the code.
            if (btnPromo) btnPromo.onClick.AddListener(() => ShowPromoPanel(true));
            if (btnPromoApply) btnPromoApply.onClick.AddListener(OnPromoApply);
            if (btnPromoClose) btnPromoClose.onClick.AddListener(() => ShowPromoPanel(false));
        }

        protected override void OnPrepare()
        {
            _purchased = false;
            // Opening the paywall always re-arms it: a purchase left in flight by an
            // earlier visit must never come back as a permanently greyed-out CTA.
            _purchaseInFlight = false;
            _failureReported = false;
            _purchaseWatchdog = null;
            SetButtonsInteractable(true);

            SelectPlan(true);
            ApplyLocalizedText();
            if (promoPanel) promoPanel.SetActive(false);

            // The store may still be initialising (or retrying) while the paywall is
            // open — refresh prices as soon as the catalog lands instead of leaving
            // placeholders on screen.
            var iap = IAPManager.Instance;
            if (iap != null)
            {
                iap.OnStoreReady -= OnStoreReady;
                iap.OnStoreReady += OnStoreReady;
                iap.PurchaseFailed -= OnPurchaseError;
                iap.PurchaseFailed += OnPurchaseError;
                if (iap.StoreUnavailable) iap.RetryInitialize();
            }

            // Last thing before the screen slides in: every label above has just been
            // rewritten (prices, plan, promo caption), so the guard re-reads the subtree
            // and lays it out.
            if (_layout) _layout.Rescan();
        }

        /// <summary>
        /// The screen survives between visits — <see cref="BaseScreen.WarmUp"/> activates the
        /// object once and hiding only switches the canvas off — so nothing rewrites its
        /// labels when the language changes behind its back. Hence the subscription: a
        /// paywall that is merely hidden still has to end up in the new language.
        /// </summary>
        private void OnEnable() => Loc.OnLanguageChanged += ApplyLocalizedText;

        private void OnDisable() => Loc.OnLanguageChanged -= ApplyLocalizedText;

        /// <summary>
        /// Every string the screen takes from <see cref="Loc"/>, in one pass. The prices
        /// belong here as much as the captions do — they carry the localized period word
        /// ("/ месяц"), so a language change has to re-run them too.
        /// </summary>
        private void ApplyLocalizedText()
        {
            ApplyStaticLabels();

            if (txtAutoRenew)
            {
#if UNITY_IOS
                txtAutoRenew.text = Loc.Get("auto_renew_ios");
#else
                txtAutoRenew.text = Loc.Get("auto_renew_android");
#endif
            }

            UpdatePromoButton();
            // Rewrites both card prices, the struck-through figure, the trial line and
            // the CTA label.
            UpdatePrices();

            // The feature columns take their height from their own text and sit in groups
            // that do not control child sizes, so a new translation needs the guard's
            // multi-pass rebuild rather than Unity's single automatic one.
            if (_layout) _layout.Refresh();
        }

        /// <summary>
        /// Every label the new layout carries that isn't a price.
        /// Each reference is optional — the old layout simply has none of them.
        /// </summary>
        private void ApplyStaticLabels()
        {
            if (txtHeadline) txtHeadline.text = Loc.Get("paywall_headline");
            if (txtPopularBadge) txtPopularBadge.text = Loc.Get("paywall_popular");
            if (txtTermsLabel) txtTermsLabel.text = Loc.Get("terms_of_use");
            if (txtRestoreLabel) txtRestoreLabel.text = Loc.Get("restore_purchases");
            if (txtPrivacyLabel) txtPrivacyLabel.text = Loc.Get("privacy_policy");

            ApplyFeatureLabels(featureTitles, "title");
            ApplyFeatureLabels(featureSubtitles, "sub");
        }

        /// <summary>Fills a feature row from paywall_f{n}_{suffix}, in array order.</summary>
        private static void ApplyFeatureLabels(TextMeshProUGUI[] labels, string suffix)
        {
            if (labels == null) return;
            for (var i = 0; i < labels.Length; i++)
                if (labels[i])
                    labels[i].text = Loc.Get($"paywall_f{i + 1}_{suffix}");
        }

        private void OnDestroy()
        {
            RestoreStars();

            var iap = IAPManager.Instance;
            if (iap == null) return;
            iap.OnStoreReady -= OnStoreReady;
            iap.PurchaseFailed -= OnPurchaseError;
        }

        /// <summary>
        /// Every purchase failure must say something on screen — a silent failure looks
        /// exactly like a broken button. A user-initiated cancel is the one case that
        /// needs no message, since the user already knows what happened.
        /// </summary>
        private void OnPurchaseError(string reason)
        {
            _failureReported = true;
            var cancelled = !string.IsNullOrEmpty(reason) &&
                            reason.IndexOf("UserCancelled", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!cancelled) Toast.Show(Loc.Get("purchase_failed"));
            FinishPurchaseAttempt();
        }

        private void OnStoreReady() => UpdatePrices();

        protected override void OnShown()
        {
            // A localized string can land during the slide-in, after OnPrepare laid
            // everything out.
            if (_layout) _layout.Refresh();
            if (background) background.DOFade(dimAlpha, bgFadeDuration).SetEase(Ease.OutQuad);
            // The stars twinkle right through the copy on this screen, which is the one
            // screen that is mostly text over the background.
            if (stars) stars.FadeTo(starsAlphaWhileOpen, starsFadeDuration);
            Analytics.Log("paywall_view", ("source", Source));
        }

        /// <summary>
        /// Single entry point for every navigation to this screen. The child may look at
        /// the paywall freely; the parental gate (Kids category, Guideline 1.3) sits on
        /// the buttons that reach the store — purchase and restore — not on the door.
        /// </summary>
        public static void Open(ScreenManager screens, System.Type returnTo = null,
            string source = "unknown")
        {
            if (screens == null) return;

            var payment = screens.Get<PaymentScreen>();
            if (payment != null)
            {
                payment.ReturnTo = returnTo;
                payment.Source = source;
            }
            screens.Show<PaymentScreen>();
        }

        protected override void OnHidden()
        {
            if (background) SetBgAlpha(0f);
            RestoreStars();
        }

        /// <summary>
        /// Hands the star field back to whatever screen comes next. Every exit runs through
        /// OnHidden, and OnDestroy repeats it, so the one screen that dims them can never
        /// leave them dimmed for the rest of the app.
        /// </summary>
        private void RestoreStars()
        {
            if (stars) stars.FadeTo(1f, starsFadeDuration);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Mirrors the slider onto the veil in the scene. Without it the editor would show
        /// whatever alpha the image was last left with — which is 0 after a play session —
        /// and the dim could only be judged by running the game.
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying || background == null) return;
            var c = background.color;
            if (Mathf.Approximately(c.a, dimAlpha)) return;
            c.a = dimAlpha;
            background.color = c;
            UnityEditor.EditorUtility.SetDirty(background);
        }
#endif

        private void SetBgAlpha(float a)
        {
            var c = background.color;
            c.a = a;
            background.color = c;
        }

        /// <summary>
        /// A plan card carries its state in three places at once — the tick on its radio,
        /// the glow behind it and its own background sprite. All three are driven from
        /// here so a card can never end up half-selected, which is what happens when the
        /// scene's authored state is left to speak for one of them.
        /// </summary>
        private void SelectPlan(bool yearly)
        {
            _yearlySelected = yearly;
            ApplyCardState(monthlyCardImage, monthlySelect, monthlyGlow, !yearly);
            ApplyCardState(yearlyCardImage, yearlySelect, yearlyGlow, yearly);
            UpdateTrialButton();
        }

        private void ApplyCardState(Image card, GameObject tick, GameObject glow, bool selected)
        {
            if (tick) tick.SetActive(selected);
            if (glow) glow.SetActive(selected);

            // Swapping the sprite is safe because the card buttons use a ColorTint
            // transition — a SpriteSwap transition would overwrite this on every hover.
            var sprite = selected ? cardSpriteSelected : cardSpriteUnselected;
            if (card && sprite) card.sprite = sprite;
        }

        private string SelectedProductId => _yearlySelected
            ? IAPManager.ProductYearly
            : IAPManager.ProductMonthly;

        /// <summary>
        /// "990 ₸ / месяц" — one place for the separator, because the same price string is
        /// built for the two cards, the struck-through figure and the trial note, and the
        /// design spaces the slash out on all of them.
        /// </summary>
        private static string PerPeriod(string price, bool yearly)
            => $"{price} / {Loc.Get(yearly ? "per_year" : "per_month")}";

        /// <summary>Billed amount for the selected plan, e.g. "₽19 990 / год". Null while
        /// the store has no catalog — callers must not invent a price in that case.</summary>
        private string SelectedBilledAmount()
        {
            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized) return null;
            var price = iap.GetLocalizedPrice(SelectedProductId);
            if (string.IsNullOrEmpty(price)) return null;
            return PerPeriod(price, _yearlySelected);
        }

        private void UpdatePrices()
        {
            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized)
            {
                // No catalog: leave the plan cards exactly as authored in the scene. The
                // screen must stay complete for the reviewer even when the store is down.
                UpdateMonthlyTrialNote(null);
                UpdateTrialButton();
                return;
            }

            var monthlyPrice = iap.GetLocalizedPrice(IAPManager.ProductMonthly);
            var yearlyPrice = iap.GetLocalizedPrice(IAPManager.ProductYearly);

            if (txtMonthlyPrice && monthlyPrice != null)
                txtMonthlyPrice.text = PerPeriod(monthlyPrice, false);
            if (txtYearlyPrice && yearlyPrice != null)
                txtYearlyPrice.text = PerPeriod(yearlyPrice, true);

            UpdateYearlySaving(iap, yearlyPrice);
            UpdateMonthlyTrialNote(monthlyPrice);
            UpdateTrialButton();
        }

        /// <summary>
        /// The "old price" and the discount badge are comparison pricing, so they must be
        /// true for the store the user is actually buying from — a hardcoded figure would
        /// show the wrong currency and a wrong percentage abroad. Both are derived from the
        /// live prices (12 months at the monthly rate vs the yearly price) and hidden only
        /// when the store itself reports that the yearly plan is not actually cheaper —
        /// showing a discount the prices don't back up is a false pricing claim.
        /// Called only with a live catalog; without one the scene's own figures stay.
        /// </summary>
        private void UpdateYearlySaving(IAPManager iap, string yearlyPriceSample)
        {
            string oldPrice = null;
            var percentOff = 0;

            var monthly = iap.GetDecimalPrice(IAPManager.ProductMonthly);
            var yearly = iap.GetDecimalPrice(IAPManager.ProductYearly);
            if (monthly > 0m && yearly > 0m && !string.IsNullOrEmpty(yearlyPriceSample))
            {
                var yearAtMonthlyRate = monthly * 12m;
                if (yearAtMonthlyRate > yearly)
                {
                    oldPrice = FormatPriceLike(yearlyPriceSample, yearAtMonthlyRate);
                    percentOff = (int)Math.Round((1m - yearly / yearAtMonthlyRate) * 100m);
                }
            }

            var hasSaving = oldPrice != null && percentOff > 0;

            if (txtYearlyOldPrice)
            {
                txtYearlyOldPrice.text = hasSaving
                    ? StruckThrough(PerPeriod(oldPrice, true))
                    : "";
                txtYearlyOldPrice.gameObject.SetActive(hasSaving);
            }
            if (txtDiscountBadge) txtDiscountBadge.text = hasSaving ? $"-{percentOff}%" : "";
            if (discountBadge) discountBadge.SetActive(hasSaving);
        }

        /// <summary>
        /// Strikes <paramref name="text"/> through with a rule in its own colour while the
        /// figure keeps the colour the label carries.
        ///
        /// TMP fixes the rule's colour when it parses the opening tag, not per character, so
        /// the colour has to be set BEFORE &lt;s&gt; and put back inside it. A single
        /// &lt;color&gt; around the whole thing would take the digits red along with the rule.
        /// </summary>
        private string StruckThrough(string text)
        {
            var rule = ColorUtility.ToHtmlStringRGB(oldPriceStrikeColor);
            var figure = txtYearlyOldPrice
                ? ColorUtility.ToHtmlStringRGB(txtYearlyOldPrice.color)
                : "FFFFFF";
            return $"<color=#{rule}><s><color=#{figure}>{text}</color></s></color>";
        }

        /// <summary>
        /// Render <paramref name="value"/> with the same currency layout the store used for
        /// <paramref name="sample"/> — symbol side, separators and decimals all copied, so
        /// "$0.01" yields "$0.12" and "₸2 990" yields "₸35 880". Returns null when the
        /// sample has no parseable number; callers must hide the element in that case.
        /// </summary>
        private static string FormatPriceLike(string sample, decimal value)
        {
            var match = Regex.Match(sample, @"[0-9][0-9\s .,]*");
            if (!match.Success) return null;

            // The match may swallow the space before a trailing currency symbol
            // ("2 490 ₽") — trim it back so the suffix keeps its separator.
            var number = match.Value.TrimEnd();
            var prefix = sample.Substring(0, match.Index);
            var suffix = sample.Substring(match.Index + number.Length);

            // A trailing ".XX" / ",XX" is the decimal part; anything else is grouping.
            var decimals = 0;
            var decSep = ".";
            var tail = Regex.Match(number, @"[.,](\d{1,2})$");
            if (tail.Success)
            {
                decimals = tail.Groups[1].Value.Length;
                decSep = tail.Value.Substring(0, 1);
            }

            // First branch tests a non-breaking space (U+00A0), second a plain one —
            // stores use either for grouping depending on locale.

            var groupSep = number.Contains(" ") ? " "
                : number.Contains(" ") ? " "
                : decSep == "." ? ","
                : ".";

            var nfi = new NumberFormatInfo
            {
                NumberDecimalSeparator = decSep,
                NumberGroupSeparator = groupSep,
                NumberDecimalDigits = decimals
            };
            return prefix + value.ToString("N", nfi) + suffix;
        }

        /// <summary>
        /// Whether the screen may advertise the free trial for <paramref name="productId"/>.
        /// With <see cref="alwaysOfferTrial"/> on this is simply true and the store is never
        /// consulted — see the field's own note for what that costs.
        /// </summary>
        private bool ShowTrialWording(string productId)
        {
            if (alwaysOfferTrial) return true;
            var iap = IAPManager.Instance;
            return iap != null && iap.HasTrialAvailable(productId);
        }

        /// <summary>
        /// The CTA names the free trial, as the design asks. Guideline 3.1.2(c) wants the
        /// billed amount to read at least as clearly as that promise — that job belongs to
        /// the price on the plan card and to the "N days free, then {price}" line under it,
        /// so neither may be hidden while this wording is up.
        /// </summary>
        private void UpdateTrialButton()
        {
            var hasTrial = ShowTrialWording(SelectedProductId);

            if (txtTrialLabel)
                txtTrialLabel.text = Loc.Get(hasTrial ? "cta_start_trial" : "subscribe");

            if (!txtTrialNote) return;

            // This note quotes the billed amount, so unlike the button it still has to wait
            // for a catalog — "free, then " with nothing after it is worse than no line.
            var billed = SelectedBilledAmount();
            var show = hasTrial && billed != null;
            txtTrialNote.text = show ? Loc.Format("trial_note", billed) : "";
            txtTrialNote.gameObject.SetActive(show);
        }

        /// <summary>
        /// The "3 days free, then 990 ₸ / month" line inside the monthly card, so it follows
        /// the monthly product rather than the selected one. The line itself stays up
        /// whenever the trial is advertised; only the price inside it waits for a catalog,
        /// and until then the card keeps the figure the scene was authored with.
        /// </summary>
        private void UpdateMonthlyTrialNote(string monthlyPrice)
        {
            if (!txtMonthlyTrialNote) return;

            var hasTrial = ShowTrialWording(IAPManager.ProductMonthly);
            txtMonthlyTrialNote.gameObject.SetActive(hasTrial);

            // Hiding the line is enough — blanking it would throw away the figure the scene
            // was authored with, which is the only thing left to show if the note comes back
            // up before the catalog has landed.
            if (hasTrial && !string.IsNullOrEmpty(monthlyPrice))
                txtMonthlyTrialNote.text = Loc.Format("trial_days_note", PerPeriod(monthlyPrice, false));
        }

        private void OnPurchase()
        {
            if (_purchaseInFlight) return;

            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized)
            {
                // Never leave a dead button. The CTA stays live and answers here: it says
                // what is wrong and kicks off a retry, so tapping again after the catalog
                // lands goes straight through. A greyed-out button would read as "tapped,
                // nothing happened" — the exact Guideline 2.1(b) complaint.
                Toast.Show(Loc.Get(iap != null && iap.StoreUnavailable
                    ? "iap_unavailable"
                    : "iap_not_ready"));
                iap?.RetryInitialize();
                return;
            }

            // The gate comes after the store check, so a parent is never asked to solve it
            // only to be told the store is not ready. The plan is read once the gate passes.
            ChildGatePopup.Show(() => StartPurchase(iap));
        }

        private void StartPurchase(IAPManager iap)
        {
            if (_purchaseInFlight) return;

            var productId = SelectedProductId;

            Analytics.Log("purchase_start", ("product_id", productId), ("source", Source));
            _purchaseInFlight = true;
            _failureReported = false;
            SetButtonsInteractable(false);
            _purchaseWatchdog = StartCoroutine(PurchaseWatchdog());

            iap.Purchase(productId, success =>
            {
                FinishPurchaseAttempt();

                // The store never complained, yet the purchase did not go through: the
                // store took the payment and the server would not confirm the receipt.
                // "Try again" is the wrong advice — a second purchase is refused as a
                // duplicate, restoring is what recovers it.
                if (!success && !_failureReported)
                {
                    RemoteLog.Event("paywall_purchase_failed",
                        $"productId={productId}; source={Source}; reason=not_validated");
                    Toast.Show(Loc.Get("purchase_not_activated"));
                }

                if (success)
                {
                    // Fires exactly once per user-initiated purchase (restore uses purchase_restore).
                    Analytics.Log("purchase_success", ("product_id", productId),
                        ("price", (double)iap.GetDecimalPrice(productId)),
                        ("currency", iap.GetIsoCurrencyCode(productId) ?? ""),
                        ("is_trial", iap.HasTrialAvailable(productId)));
                    // GA4 standard revenue event (spec §2.5 note) for out-of-the-box money reports.
                    Analytics.Log("purchase", ("value", (double)iap.GetDecimalPrice(productId)),
                        ("currency", iap.GetIsoCurrencyCode(productId) ?? ""));
                    _purchased = true;
                    Toast.Show(Loc.Get("purchase_success"));
                    OnClose();
                }
            });
        }

        /// <summary>Re-arms the paywall once a purchase attempt is over, whichever way it
        /// ended. Idempotent — failure event and purchase callback both land here.</summary>
        private void FinishPurchaseAttempt()
        {
            _purchaseInFlight = false;
            if (_purchaseWatchdog != null)
            {
                StopCoroutine(_purchaseWatchdog);
                _purchaseWatchdog = null;
            }
            SetButtonsInteractable(true);
        }

        private IEnumerator PurchaseWatchdog()
        {
            yield return new WaitForSecondsRealtime(PurchaseTimeout);
            if (!_purchaseInFlight) yield break;

            Debug.LogWarning("[IAP-DBG] Purchase produced no callback within the timeout — re-enabling UI");
            RemoteLog.Event("purchase_timeout", $"productId={SelectedProductId}");
            _purchaseWatchdog = null;
            FinishPurchaseAttempt();
            if (!_failureReported) Toast.Show(Loc.Get("purchase_failed"));
        }

        private void OnClose()
        {
            if (!_purchased) Analytics.Log("paywall_dismiss", ("source", Source));

            var target = ReturnTo ?? typeof(Library.LibraryScreen);
            ReturnTo = null;

            if (background)
            {
                background.DOFade(0f, bgFadeDuration).SetEase(Ease.InQuad)
                    .OnComplete(() => _screens.Show(target));
            }
            else
            {
                _screens.Show(target);
            }
        }

        private void OnRestore()
        {
            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized)
            {
                Toast.Show(Loc.Get(iap != null && iap.StoreUnavailable
                    ? "iap_unavailable"
                    : "iap_not_ready"));
                iap?.RetryInitialize();
                return;
            }

            ChildGatePopup.Show(() => StartRestore(iap));
        }

        private void StartRestore(IAPManager iap)
        {
            SetButtonsInteractable(false);

            iap.RestorePurchases(success =>
            {
                SetButtonsInteractable(true);

                bool restored = success && iap.IsSubscribed;
                Analytics.Log("purchase_restore", ("restored", restored));

                if (restored)
                {
                    _purchased = true;
                    Toast.Show(Loc.Get("restore_success"));
                    OnClose();
                }
                else
                {
                    Toast.Show(Loc.Get("restore_none"));
                }
            });
        }

        private void OnTerms()
        {
            ChildGatePopup.Show(() =>
                Application.OpenURL(AppConfig.TermsUrl));
        }

        private void OnPrivacy()
        {
            ChildGatePopup.Show(() =>
                Application.OpenURL(AppConfig.PrivacyUrl));
        }

        /// <summary>
        /// The entry point is always on screen — on every platform, for subscribers, and
        /// for someone who already redeemed a code. A code is not only premium: it opens
        /// individual tales, so a user who spent one still has a reason to come back, and
        /// a missing button reads as a missing feature. A code the server refuses answers
        /// with a message in the field instead.
        /// </summary>
        private void UpdatePromoButton()
        {
            if (!btnPromo) return;
            btnPromo.gameObject.SetActive(true);

            // The icon carries a caption now: the gift popup tells the invited friend to
            // tap a button by this exact name, so it has to follow the app language.
            var label = btnPromo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label) label.text = Loc.Get("promo_code");
        }

        private void ShowPromoPanel(bool show)
        {
            if (!promoPanel) return;
            promoPanel.SetActive(show);
            if (_layout) _layout.Refresh();
            if (!show)
            {
                if (promoInput) promoInput.text = "";
                SetPromoError("");
            }
        }

        private void SetPromoError(string msg)
        {
            if (!txtPromoError) return;
            txtPromoError.text = msg;
            txtPromoError.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        }

        private void OnPromoApply()
        {
            Debug.Log($"[Promo] OnPromoApply: _promoService={_promoService != null}");
            if (_promoService == null)
            {
                SetPromoError(Loc.Get("promo_no_connection"));
                return;
            }

            var code = promoInput ? promoInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code)) return;

            SetPromoError("");
            if (btnPromoApply) btnPromoApply.interactable = false;

            StartCoroutine(_promoService.CheckPromo(code,
                response =>
                {
                    if (btnPromoApply) btnPromoApply.interactable = true;

                    // A 200 with a body we can't read would otherwise fall through every
                    // branch below and leave the user staring at a button that did nothing.
                    if (response == null || string.IsNullOrEmpty(response.type))
                    {
                        SetPromoError(Loc.Get("promo_not_found"));
                        return;
                    }

                    if (response.type == "premium")
                    {
                        PlayerPrefs.SetString("ft_premium_source", "promo");
                        PlayerPrefs.Save();
                        IAPManager.Instance.IsSubscribed = true;
                        Analytics.Log("promo_redeem", ("success", true));
                        LogPromoEntered("premium", 0, response.bloggerName);
                        _purchased = true;
                        Toast.Show(response.message ?? Loc.Get("purchase_success"));
                        ShowPromoPanel(false);
                        OnClose();
                    }
                    else if (response.type == "tale")
                    {
                        OnTalesUnlocked(response);
                    }
                    else if (response.type == "referral")
                    {
                        // The invited friend's side of "book as a gift": nothing unlocks
                        // here, the tale opens on the INVITER's account. Say thank you and
                        // close the field — otherwise they wait for a book that was never
                        // theirs. The paywall stays open: nothing was purchased.
                        Analytics.Log("promo_redeem", ("success", true));
                        LogPromoEntered("referral", 0, response.bloggerName);
                        Toast.Show(Loc.Get("promo_referral_ok"));
                        ShowPromoPanel(false);
                    }
                    else
                    {
                        // "blogger" and anything a future server adds: show what it said
                        // rather than swallowing the answer.
                        Analytics.Log("promo_redeem", ("success", true));
                        LogPromoEntered(response.type, 0, response.bloggerName);
                        var text = response.message ?? response.bloggerName;
                        SetPromoError(string.IsNullOrEmpty(text) ? Loc.Get("promo_code") : text);
                    }
                },
                error =>
                {
                    if (btnPromoApply) btnPromoApply.interactable = true;
                    Analytics.Log("promo_redeem", ("success", false));
                    Debug.LogWarning($"[Promo] CheckPromo error: {error}");

                    // 409 means this account already spent its one code. The field stays
                    // where it is and says so — hiding it would take the whole feature
                    // off the screen for the sake of one refused attempt.
                    var key = PromoService.ErrorKey(error);
                    SetPromoError(Loc.Get(key));
                }
            ));
        }

        /// <summary>
        /// A blogger code opened a set of tales. The lock is drawn by the app from the
        /// catalog's `unlocked` flag, so the library must re-read the catalog — otherwise
        /// the tales stay locked on screen until the next cold start.
        /// </summary>
        private void OnTalesUnlocked(PromoService.PromoCheckResponse response)
        {
            var count = response.taleIds?.Length ?? 0;
            LogPromoEntered("tale", count, response.bloggerName);

            // Our own translation wins: the server's `message` is Russian whatever the
            // app language is. It is only used when the count is missing.
            Toast.Show(count > 0
                ? Loc.Format("promo_tales_unlocked", count)
                : response.message ?? Loc.Get("promo_code"));

            ShowPromoPanel(false);

            var library = _screens != null ? _screens.Get<Library.LibraryScreen>() : null;
            if (library != null) library.ApplyPromoUnlocks(response.taleIds);

            // The paywall ended in a success, not a dismissal — and the user is sent to
            // the library rather than back to where they came from, because the whole
            // point of the code is the tales that just became available.
            _purchased = true;
            ReturnTo = null;
            OnClose();
        }

        private static void LogPromoEntered(string codeType, int talesUnlocked, string blogger)
            => Analytics.Log("promo_entered", ("code_type", codeType),
                ("tales_unlocked", talesUnlocked), ("blogger", blogger ?? ""));

        private void SetButtonsInteractable(bool interactable)
        {
            // Only ever disabled while a purchase is in flight, never because of store state.
            if (btnTrial) btnTrial.interactable = interactable;
            if (btnRestore) btnRestore.interactable = interactable;
            if (btnMonthly) btnMonthly.interactable = interactable;
            if (btnYearly) btnYearly.interactable = interactable;
        }
    }
}
