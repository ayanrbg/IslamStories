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
            if (btnPromo) btnPromo.onClick.AddListener(() => ShowPromoPanel(true));
            if (btnPromoApply) btnPromoApply.onClick.AddListener(OnPromoApply);
            if (btnPromoClose) btnPromoClose.onClick.AddListener(() => ShowPromoPanel(false));
        }

        protected override void OnPrepare()
        {
            _purchased = false;
            SelectPlan(true);
            UpdatePrices();
            if (promoPanel) promoPanel.SetActive(false);
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

        private void UpdatePrices()
        {
            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized) return;

            var monthlyPrice = iap.GetLocalizedPrice(IAPManager.ProductMonthly);
            var yearlyPrice = iap.GetLocalizedPrice(IAPManager.ProductYearly);

            if (txtMonthlyPrice && monthlyPrice != null)
                txtMonthlyPrice.text = $"{monthlyPrice}/{Loc.Get("per_month")}";
            if (txtYearlyPrice && yearlyPrice != null)
                txtYearlyPrice.text = $"{yearlyPrice}/{Loc.Get("per_year")}";
        }

        private void UpdateTrialButton()
        {
            // Trial eligibility is decided by the store, not shown in the UI.
            // The CTA is always "Попробовать бесплатно" on every language.
            if (txtTrialLabel) txtTrialLabel.text = Loc.Get("start_trial");
        }

        private void OnPurchase()
        {
            var iap = IAPManager.Instance;
            if (iap == null || !iap.IsInitialized)
            {
                Toast.Show(Loc.Get("iap_not_ready"));
                return;
            }

            var productId = _yearlySelected
                ? IAPManager.ProductYearly
                : IAPManager.ProductMonthly;

            Analytics.Log("purchase_start", ("product_id", productId), ("source", Source));
            SetButtonsInteractable(false);

            iap.Purchase(productId, success =>
            {
                SetButtonsInteractable(true);

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
                Toast.Show(Loc.Get("iap_not_ready"));
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
                Application.OpenURL("https://docs.google.com/document/d/15SLWdfFw3A9e4D-8BBJDY-STWj34_6bFRZznK8HVqQA/edit?usp=sharing"));
        }

        private void OnPrivacy()
        {
            ChildGatePopup.Show(() =>
                Application.OpenURL("https://docs.google.com/document/d/1oH94jmvSXyxxazAu9Dz-vAZQz3jYLLSMIPyXFZEOX0o/edit?usp=sharing"));
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

                    if (response.type == "premium")
                    {
                        PlayerPrefs.SetString("ft_premium_source", "promo");
                        PlayerPrefs.Save();
                        IAPManager.Instance.IsSubscribed = true;
                        Analytics.Log("promo_redeem", ("success", true));
                        _purchased = true;
                        Toast.Show(response.message ?? Loc.Get("purchase_success"));
                        ShowPromoPanel(false);
                        OnClose();
                    }
                    else if (response.type == "blogger")
                    {
                        SetPromoError(response.message ?? response.bloggerName);
                    }
                },
                error =>
                {
                    if (btnPromoApply) btnPromoApply.interactable = true;
                    Analytics.Log("promo_redeem", ("success", false));
                    Debug.LogWarning($"[Promo] CheckPromo error: {error}");

                    var err = error?.ToLowerInvariant() ?? "";
                    if (err.Contains("already"))
                        SetPromoError(Loc.Get("promo_already_used"));
                    else if (err.Contains("not found") || err.Contains("invalid"))
                        SetPromoError(Loc.Get("promo_not_found"));
                    else
                        SetPromoError(Loc.Get("promo_no_connection"));
                }
            ));
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (btnTrial) btnTrial.interactable = interactable;
            if (btnRestore) btnRestore.interactable = interactable;
            if (btnMonthly) btnMonthly.interactable = interactable;
            if (btnYearly) btnYearly.interactable = interactable;
        }
    }
}
