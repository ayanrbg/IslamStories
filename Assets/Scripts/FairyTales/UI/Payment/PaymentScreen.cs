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
        [SerializeField] private Image background;
        [SerializeField] private float bgFadeDuration = 0.5f;
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnMonthly;
        [SerializeField] private Button btnYearly;
        [SerializeField] private Button btnTrial;
        [SerializeField] private Button btnTerms;
        [SerializeField] private Button btnRestore;
        [SerializeField] private Button btnPrivacy;
        [SerializeField] private GameObject monthlySelect;
        [SerializeField] private GameObject yearlySelect;
        [SerializeField] private TextMeshProUGUI txtMonthlyPrice;
        [SerializeField] private TextMeshProUGUI txtYearlyPrice;
        [SerializeField] private TextMeshProUGUI txtTrialLabel;

        [Header("Yearly saving (derived from live store prices, never hardcoded)")]
        [Tooltip("Struck-through '12 months at the monthly rate' figure on the yearly card.")]
        [SerializeField] private TextMeshProUGUI txtYearlyOldPrice;
        [Tooltip("Discount badge label, e.g. \"-45%\".")]
        [SerializeField] private TextMeshProUGUI txtDiscountBadge;
        [Tooltip("Badge background — hidden together with the label when there is no real saving.")]
        [SerializeField] private GameObject discountBadge;

        [Header("Subscription disclosure (Guideline 3.1.2(c))")]
        [Tooltip("Subordinate line under the CTA. Must be smaller and less prominent than the billed amount.")]
        [SerializeField] private TextMeshProUGUI txtTrialNote;
        [Tooltip("Auto-renewal disclaimer shown next to the Terms / Privacy links.")]
        [SerializeField] private TextMeshProUGUI txtAutoRenew;

        [Header("Store unavailable state")]
        [SerializeField] private GameObject storeErrorPanel;
        [SerializeField] private TextMeshProUGUI txtStoreError;
        [SerializeField] private Button btnRetryStore;

        [Header("Promo")]
        [SerializeField] private Button btnPromo;
        [SerializeField] private GameObject promoPanel;
        [SerializeField] private TMP_InputField promoInput;
        [SerializeField] private Button btnPromoApply;
        [SerializeField] private Button btnPromoClose;
        [SerializeField] private TextMeshProUGUI txtPromoError;

        private ScreenManager _screens;
        private PromoService _promoService;
        private bool _yearlySelected = true;
        private bool _purchased;

        private bool _purchaseInFlight;
        private bool _failureReported;
        private Coroutine _purchaseWatchdog;
        private bool _storeErrorUiChecked;

        /// <summary>StoreKit can leave a purchase pending (parental approval, interrupted
        /// sheet) without ever calling back. Re-arm the UI after this long so the buttons
        /// can never stay disabled.</summary>
        private const float PurchaseTimeout = 120f;

        /// <summary>Height of the runtime-built store-status strip, in the CTA's units.</summary>
        private const float StoreErrorHeight = 100f;

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

            var api = FindAnyObjectByType<ApiClient>();
            Debug.Log($"[Promo] Awake: ApiClient found={api != null}");
            if (api) _promoService = new PromoService(api);

            if (background) SetBgAlpha(0f);
            if (btnClose) btnClose.onClick.AddListener(OnClose);
            if (btnMonthly) btnMonthly.onClick.AddListener(() => SelectPlan(false));
            if (btnYearly) btnYearly.onClick.AddListener(() => SelectPlan(true));
            if (btnTrial) btnTrial.onClick.AddListener(OnPurchase);
            if (btnTerms) btnTerms.onClick.AddListener(OnTerms);
            if (btnRestore) btnRestore.onClick.AddListener(OnRestore);
            if (btnPrivacy) btnPrivacy.onClick.AddListener(OnPrivacy);
            if (btnRetryStore) btnRetryStore.onClick.AddListener(OnRetryStore);
#if UNITY_IOS
            // Guideline 3.1.1: premium may only be unlocked through In-App Purchase
            // on iOS — the promo-code entry point must not exist there at all.
            if (btnPromo) btnPromo.gameObject.SetActive(false);
            if (promoPanel) promoPanel.SetActive(false);
#else
            if (btnPromo) btnPromo.onClick.AddListener(() => ShowPromoPanel(true));
            if (btnPromoApply) btnPromoApply.onClick.AddListener(OnPromoApply);
            if (btnPromoClose) btnPromoClose.onClick.AddListener(() => ShowPromoPanel(false));
#endif
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

            EnsureStoreErrorUi();
            SelectPlan(true);
            UpdatePrices();
            if (promoPanel) promoPanel.SetActive(false);
            UpdatePromoButton();

            if (txtAutoRenew)
            {
#if UNITY_IOS
                txtAutoRenew.text = Loc.Get("auto_renew_ios");
#else
                txtAutoRenew.text = Loc.Get("auto_renew_android");
#endif
            }

            // The store may still be initialising (or retrying) while the paywall is
            // open — refresh prices as soon as the catalog lands instead of leaving
            // placeholders on screen.
            var iap = IAPManager.Instance;
            if (iap != null)
            {
                iap.OnStoreReady -= OnStoreReady;
                iap.OnStoreReady += OnStoreReady;
                iap.OnStoreFailed -= OnStoreFailed;
                iap.OnStoreFailed += OnStoreFailed;
                iap.PurchaseFailed -= OnPurchaseError;
                iap.PurchaseFailed += OnPurchaseError;
                if (iap.StoreUnavailable) iap.RetryInitialize();
            }
        }

        private void OnDestroy()
        {
            var iap = IAPManager.Instance;
            if (iap == null) return;
            iap.OnStoreReady -= OnStoreReady;
            iap.OnStoreFailed -= OnStoreFailed;
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

        private void OnStoreFailed() => UpdateStoreErrorState();

        private void OnRetryStore()
        {
            var iap = IAPManager.Instance;
            if (iap == null) return;
            if (txtStoreError) txtStoreError.text = Loc.Get("iap_not_ready");
            if (btnRetryStore) btnRetryStore.interactable = false;
            iap.RetryInitialize();
        }

        protected override void OnShown()
        {
            if (background) background.DOFade(1f, bgFadeDuration).SetEase(Ease.OutQuad);
            Analytics.Log("paywall_view", ("source", Source));
        }

        /// <summary>
        /// Kids category (Guideline 1.3): the parental gate must pass BEFORE the
        /// paywall opens. Single entry point for every navigation to this screen —
        /// shows the gate first, opens the screen only on success.
        /// </summary>
        public static void Open(ScreenManager screens, System.Type returnTo = null,
            System.Action onCancel = null, string source = "unknown")
        {
            if (screens == null) return;
            ChildGatePopup.Show(() =>
            {
                var payment = screens.Get<PaymentScreen>();
                if (payment != null)
                {
                    payment.ReturnTo = returnTo;
                    payment.Source = source;
                }
                screens.Show<PaymentScreen>();
            }, onCancel);
        }

        protected override void OnHidden()
        {
            if (background) SetBgAlpha(0f);
        }

        private void SetBgAlpha(float a)
        {
            var c = background.color;
            c.a = a;
            background.color = c;
        }

        private void SelectPlan(bool yearly)
        {
            _yearlySelected = yearly;
            if (monthlySelect) monthlySelect.SetActive(!yearly);
            if (yearlySelect) yearlySelect.SetActive(yearly);
            UpdateTrialButton();
        }

        private string SelectedProductId => _yearlySelected
            ? IAPManager.ProductYearly
            : IAPManager.ProductMonthly;

        /// <summary>Billed amount for the selected plan, e.g. "₽19 990/год". Null while
        /// the store has no catalog — callers must not invent a price in that case.</summary>
        private string SelectedBilledAmount()
        {
            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized) return null;
            var price = iap.GetLocalizedPrice(SelectedProductId);
            if (string.IsNullOrEmpty(price)) return null;
            return $"{price}/{Loc.Get(_yearlySelected ? "per_year" : "per_month")}";
        }

        private void UpdatePrices()
        {
            UpdateStoreErrorState();

            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized)
            {
                // No catalog: leave the plan cards exactly as authored in the scene. The
                // screen must stay complete for the reviewer even when the store is down.
                UpdateTrialButton();
                return;
            }

            var monthlyPrice = iap.GetLocalizedPrice(IAPManager.ProductMonthly);
            var yearlyPrice = iap.GetLocalizedPrice(IAPManager.ProductYearly);

            if (txtMonthlyPrice && monthlyPrice != null)
                txtMonthlyPrice.text = $"{monthlyPrice}/{Loc.Get("per_month")}";
            if (txtYearlyPrice && yearlyPrice != null)
                txtYearlyPrice.text = $"{yearlyPrice}/{Loc.Get("per_year")}";

            UpdateYearlySaving(iap, yearlyPrice);
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
                    ? $"<s>{oldPrice}/{Loc.Get("per_year")}</s>"
                    : "";
                txtYearlyOldPrice.gameObject.SetActive(hasSaving);
            }
            if (txtDiscountBadge) txtDiscountBadge.text = hasSaving ? $"-{percentOff}%" : "";
            if (discountBadge) discountBadge.SetActive(hasSaving);
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
        /// Guideline 3.1.2(c): the billed amount must be the clearest pricing element in
        /// the flow. The CTA therefore carries no free-trial promise at all — the price on
        /// the selected plan card stays the loudest pricing element, and any trial wording
        /// lives in the subordinate note below it.
        /// The label is deliberately a short static string: it must not resize the button.
        /// </summary>
        private void UpdateTrialButton()
        {
            if (txtTrialLabel) txtTrialLabel.text = Loc.Get("subscribe");

            if (!txtTrialNote) return;

            var billed = SelectedBilledAmount();

            var iap = IAPManager.Instance;
            var hasTrial = billed != null && iap != null && iap.HasTrialAvailable(SelectedProductId);
            txtTrialNote.text = hasTrial ? Loc.Format("trial_note", billed) : "";
            txtTrialNote.gameObject.SetActive(hasTrial);
        }

        /// <summary>
        /// The store-status line is the only thing that explains a paywall the store
        /// cannot serve, so it must exist even when the scene reference was never wired —
        /// otherwise the CTA looks simply broken (Guideline 2.1(b)). Builds a minimal
        /// panel just above the CTA, never over it.
        /// </summary>
        private void EnsureStoreErrorUi()
        {
            if (_storeErrorUiChecked) return;
            _storeErrorUiChecked = true;
            if (storeErrorPanel != null || btnTrial == null) return;

            var cta = btnTrial.GetComponent<RectTransform>();
            if (cta == null) return;

            // Runtime text needs a font that actually has Cyrillic glyphs — take the one
            // the CTA already uses instead of TMP's default ASCII asset.
            var fontSource = txtTrialLabel ? txtTrialLabel : txtAutoRenew;

            var panel = new GameObject("StoreErrorRuntime", typeof(RectTransform), typeof(Image));
            var rt = panel.GetComponent<RectTransform>();
            rt.SetParent(cta.parent, false);
            rt.anchorMin = cta.anchorMin;
            rt.anchorMax = cta.anchorMax;
            rt.pivot = cta.pivot;
            rt.sizeDelta = new Vector2(cta.sizeDelta.x, StoreErrorHeight);
            // Sits directly above the CTA and never over it — the purchase button has to
            // stay tappable no matter what the store is doing.
            rt.anchoredPosition = cta.anchoredPosition +
                new Vector2(0f, (cta.rect.height + StoreErrorHeight) * 0.5f + 12f);
            panel.GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.22f, 0.92f);

            var label = CreateRuntimeLabel(rt, "Text", fontSource,
                new Vector2(0.03f, 0f), new Vector2(0.7f, 1f));

            var retryGo = new GameObject("BtnRetry", typeof(RectTransform), typeof(Image), typeof(Button));
            var retryRt = retryGo.GetComponent<RectTransform>();
            retryRt.SetParent(rt, false);
            retryRt.anchorMin = new Vector2(0.72f, 0.15f);
            retryRt.anchorMax = new Vector2(0.97f, 0.85f);
            retryRt.offsetMin = Vector2.zero;
            retryRt.offsetMax = Vector2.zero;
            retryGo.GetComponent<Image>().color = new Color(0.55f, 0.36f, 0.85f);
            var retryLabel = CreateRuntimeLabel(retryRt, "Text", fontSource,
                Vector2.zero, Vector2.one);
            retryLabel.text = Loc.Get("retry");

            storeErrorPanel = panel;
            txtStoreError = label;
            btnRetryStore = retryGo.GetComponent<Button>();
            btnRetryStore.onClick.AddListener(OnRetryStore);
            panel.SetActive(false);
        }

        private static TextMeshProUGUI CreateRuntimeLabel(RectTransform parent, string name,
            TextMeshProUGUI fontSource, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (fontSource)
            {
                tmp.font = fontSource.font;
                tmp.fontSharedMaterial = fontSource.fontSharedMaterial;
            }
            // Autosized: the strip is built against whatever canvas units the scene uses,
            // so a fixed point size could come out unreadable or overflow.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10f;
            tmp.fontSizeMax = StoreErrorHeight * 0.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        /// <summary>
        /// Reflects store availability WITHOUT disabling the CTA. A greyed-out purchase
        /// button reads as "tapped, nothing happened" — the exact 2.1(b) complaint — so the
        /// button stays live and <see cref="OnPurchase"/> explains the problem and retries.
        /// </summary>
        private void UpdateStoreErrorState()
        {
            var iap = IAPManager.Instance;
            var ready = iap != null && iap.IsInitialized;
            var failed = iap != null && iap.StoreUnavailable;

            if (storeErrorPanel) storeErrorPanel.SetActive(!ready);
            if (txtStoreError)
                txtStoreError.text = Loc.Get(failed ? "iap_unavailable" : "iap_not_ready");
            if (btnRetryStore)
            {
                btnRetryStore.gameObject.SetActive(!ready);
                btnRetryStore.interactable = failed;
            }
        }

        private void OnPurchase()
        {
            if (_purchaseInFlight) return;

            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized)
            {
                // Never leave a dead button: say what's wrong and kick off a retry.
                EnsureStoreErrorUi();
                Toast.Show(Loc.Get(iap != null && iap.StoreUnavailable
                    ? "iap_unavailable"
                    : "iap_not_ready"));
                iap?.RetryInitialize();
                UpdateStoreErrorState();
                return;
            }

            var productId = SelectedProductId;

            Analytics.Log("purchase_start", ("product_id", productId), ("source", Source));
            _purchaseInFlight = true;
            _failureReported = false;
            SetButtonsInteractable(false);
            _purchaseWatchdog = StartCoroutine(PurchaseWatchdog());

            iap.Purchase(productId, success =>
            {
                FinishPurchaseAttempt();

                if (!success && !_failureReported)
                    Toast.Show(Loc.Get("purchase_failed"));

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
        /// A promo code is redeemable once per account, so a second attempt can only end
        /// in a 409 — hide the entry point instead of letting the user hit that wall.
        /// Never hidden for subscribers: paying does not disqualify anyone from a code.
        /// On iOS it stays hidden either way (Guideline 3.1.1, see Awake).
        /// </summary>
        private void UpdatePromoButton()
        {
            if (!btnPromo) return;
#if UNITY_IOS
            btnPromo.gameObject.SetActive(false);
#else
            btnPromo.gameObject.SetActive(!PromoService.WasUsed);
#endif
        }

        private void ShowPromoPanel(bool show)
        {
            if (!promoPanel) return;
            promoPanel.SetActive(show);
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
                        PromoService.MarkUsed(code);
                        Analytics.Log("promo_redeem", ("success", true));
                        LogPromoEntered("premium", 0, response.bloggerName);
                        _purchased = true;
                        Toast.Show(response.message ?? Loc.Get("purchase_success"));
                        ShowPromoPanel(false);
                        OnClose();
                    }
                    else if (response.type == "tale")
                    {
                        OnTalesUnlocked(code, response);
                    }
                    else
                    {
                        // "blogger" and anything a future server adds: show what it said
                        // rather than swallowing the answer.
                        PromoService.MarkUsed(code);
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

                    var key = PromoService.ErrorKey(error);
                    // 409 means this account already spent its one code — stop offering
                    // the field, the server will refuse every further attempt too. Redraw
                    // right away: the screen is already open, so waiting for the next
                    // OnPrepare would let the user walk into the same 409 again.
                    if (key == "promo_already_used")
                    {
                        PromoService.MarkUsed(code);
                        UpdatePromoButton();
                    }
                    SetPromoError(Loc.Get(key));
                }
            ));
        }

        /// <summary>
        /// A blogger code opened a set of tales. The lock is drawn by the app from the
        /// catalog's `unlocked` flag, so the library must re-read the catalog — otherwise
        /// the tales stay locked on screen until the next cold start.
        /// </summary>
        private void OnTalesUnlocked(string code, PromoService.PromoCheckResponse response)
        {
            var count = response.taleIds?.Length ?? 0;
            PromoService.MarkUsed(code);
            LogPromoEntered("tale", count, response.bloggerName);

            // Our own translation wins: the server's `message` is Russian whatever the
            // app language is. It is only used when the count is missing.
            Toast.Show(count > 0
                ? Loc.Format("promo_tales_unlocked", count)
                : response.message ?? Loc.Get("promo_code"));

            ShowPromoPanel(false);
            UpdatePromoButton();

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
