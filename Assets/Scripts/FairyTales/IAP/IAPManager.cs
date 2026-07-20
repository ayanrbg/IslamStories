using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using FairyTales.Diagnostics;

namespace FairyTales.IAP
{
    public class IAPManager : MonoBehaviour, IDetailedStoreListener
    {
        public const string ProductMonthly = "fairytales_monthly";
        public const string ProductYearly = "fairytales_yearly";

        // App Store Connect product IDs differ from the unified ids above (ASC ids
        // cannot be renamed after creation; "fairytales_*" there is only the
        // reference name). Google Play uses the unified ids as-is.
        private const string AppleProductMonthly = "com.mozz.islamStories";
        private const string AppleProductYearly = "com.mozz.islamStoriesYearly";

        public static IAPManager Instance { get; private set; }

        public bool IsInitialized => _store != null;

        /// <summary>
        /// True when the user has an active premium subscription.
        /// Persisted to PlayerPrefs so the flag survives app restarts
        /// even before IAP finishes initialising.
        /// </summary>
        public bool IsSubscribed
        {
            get => _isSubscribed;
            set
            {
                bool changed = _isSubscribed != value;
                _isSubscribed = value;
                PlayerPrefs.SetInt("ft_premium", value ? 1 : 0);
                PlayerPrefs.Save();
                Debug.Log($"[IAP-DBG] IsSubscribed persisted: {value}");
                if (changed)
                {
                    RemoteLog.Event("premium_changed", $"value={value}");
                    Analytics.RefreshUserProps();
                    OnSubscriptionChanged?.Invoke(value);
                }
            }
        }
        private bool _isSubscribed;

        /// <summary>
        /// When the active subscription runs out (UTC). null = perpetual (promo/admin)
        /// or unknown. Persisted so the "Active until …" label survives restarts.
        /// </summary>
        public DateTime? PremiumExpiresUtc
        {
            get
            {
                var s = PlayerPrefs.GetString("ft_premium_expires", "");
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
                    return d;
                return null;
            }
            private set
            {
                PlayerPrefs.SetString("ft_premium_expires", value.HasValue ? value.Value.ToString("o") : "");
                PlayerPrefs.Save();
            }
        }

        public event Action<bool> OnSubscriptionChanged;
        public event Action<string> PurchaseFailed;

        private IStoreController _store;
        private IExtensionProvider _extensions;
        private Action<bool> _purchaseCallback;

        private void Awake()
        {
            Debug.Log("[IAP-DBG] ===== IAPManager.Awake() START =====");
            Debug.Log($"[IAP-DBG] Instance={Instance}, this={GetInstanceID()}, gameObject={gameObject.name}");

            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[IAP-DBG] Duplicate IAPManager detected! Existing={Instance.GetInstanceID()}, destroying this={GetInstanceID()}");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Restore cached premium state so features work immediately,
            // even before IAP store finishes initialising.
            _isSubscribed = PlayerPrefs.GetInt("ft_premium", 0) == 1;
            Debug.Log($"[IAP-DBG] IAPManager singleton set, cached IsSubscribed={_isSubscribed}");
            InitStore();
        }

        private void InitStore()
        {
            Debug.Log("[IAP-DBG] ===== InitStore() START =====");
            try
            {
                Debug.Log("[IAP-DBG] Creating StandardPurchasingModule...");
                var module = StandardPurchasingModule.Instance();
                Debug.Log($"[IAP-DBG] StandardPurchasingModule created, useFakeStoreUIMode={module.useFakeStoreUIMode}, useFakeStoreAlways={module.useFakeStoreAlways}");

                var builder = ConfigurationBuilder.Instance(module);
                Debug.Log("[IAP-DBG] ConfigurationBuilder created");

                builder.AddProduct(ProductMonthly, ProductType.Subscription, new IDs
                {
                    { AppleProductMonthly, AppleAppStore.Name },
                    { ProductMonthly, GooglePlay.Name },
                });
                Debug.Log($"[IAP-DBG] Added product: {ProductMonthly} (Subscription, apple={AppleProductMonthly})");

                builder.AddProduct(ProductYearly, ProductType.Subscription, new IDs
                {
                    { AppleProductYearly, AppleAppStore.Name },
                    { ProductYearly, GooglePlay.Name },
                });
                Debug.Log($"[IAP-DBG] Added product: {ProductYearly} (Subscription, apple={AppleProductYearly})");

                Debug.Log("[IAP-DBG] Calling UnityPurchasing.Initialize()...");
                UnityPurchasing.Initialize(this, builder);
                Debug.Log("[IAP-DBG] UnityPurchasing.Initialize() called (async, waiting for callback)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAP-DBG] EXCEPTION in InitStore(): {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }

        // ── IStoreListener ──────────────────────────────────

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("[IAP-DBG] ===== OnInitialized() SUCCESS =====");
            _store = controller;
            _extensions = extensions;

            // Log all products info
            var sb = new StringBuilder();
            sb.AppendLine("[IAP-DBG] === Product catalog ===");
            foreach (var product in controller.products.all)
            {
                sb.AppendLine($"  ID: {product.definition.id}");
                sb.AppendLine($"    storeSpecificId: {product.definition.storeSpecificId}");
                sb.AppendLine($"    type: {product.definition.type}");
                sb.AppendLine($"    available: {product.availableToPurchase}");
                sb.AppendLine($"    hasReceipt: {product.hasReceipt}");
                if (product.metadata != null)
                {
                    sb.AppendLine($"    localizedTitle: {product.metadata.localizedTitle}");
                    sb.AppendLine($"    localizedPrice: {product.metadata.localizedPriceString}");
                    sb.AppendLine($"    isoCurrencyCode: {product.metadata.isoCurrencyCode}");
                    sb.AppendLine($"    localizedDesc: {product.metadata.localizedDescription}");
                }
                else
                {
                    sb.AppendLine("    metadata: NULL");
                }
                if (product.hasReceipt)
                {
                    sb.AppendLine($"    receipt(first 100): {product.receipt?.Substring(0, Math.Min(100, product.receipt?.Length ?? 0))}...");
                }
            }
            Debug.Log(sb.ToString());

            var m = controller.products.WithID(ProductMonthly);
            var y = controller.products.WithID(ProductYearly);
            RemoteLog.Event("iap_init_ok",
                $"cachedPremium={IsSubscribed}; " +
                $"monthly(avail={m?.availableToPurchase},receipt={m?.hasReceipt},price={m?.metadata?.localizedPriceString}); " +
                $"yearly(avail={y?.availableToPurchase},receipt={y?.hasReceipt},price={y?.metadata?.localizedPriceString})");

            CheckSubscription();
            SyncState("init");
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[IAP-DBG] ===== OnInitializeFailed() =====");
            Debug.LogError($"[IAP-DBG] Reason: {error}");
            Debug.LogError($"[IAP-DBG] Platform: {Application.platform}");
            Debug.LogError($"[IAP-DBG] ProductMonthly={ProductMonthly}, ProductYearly={ProductYearly}");
            Debug.LogError($"[IAP-DBG] Hint: Check that products exist in App Store Connect with EXACT IDs");
            Debug.LogError($"[IAP-DBG] Hint: Ensure 'In-App Purchase' capability is enabled in Xcode");
            Debug.LogError($"[IAP-DBG] Hint: Ensure 'Paid Applications' agreement is signed in App Store Connect");
            RemoteLog.Event("iap_init_failed", $"reason={error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[IAP-DBG] ===== OnInitializeFailed(with message) =====");
            Debug.LogError($"[IAP-DBG] Reason: {error}");
            Debug.LogError($"[IAP-DBG] Message: {message}");
            Debug.LogError($"[IAP-DBG] Platform: {Application.platform}");
            Debug.LogError($"[IAP-DBG] ProductMonthly={ProductMonthly}, ProductYearly={ProductYearly}");
            Debug.LogError($"[IAP-DBG] Hint: Check that products exist in App Store Connect with EXACT IDs");
            Debug.LogError($"[IAP-DBG] Hint: Ensure 'In-App Purchase' capability is enabled in Xcode");
            Debug.LogError($"[IAP-DBG] Hint: Ensure 'Paid Applications' agreement is signed in App Store Connect");
            Debug.LogError($"[IAP-DBG] Hint: If 'NoProductsAvailable' — products may be in 'Missing Metadata' status");
            RemoteLog.Event("iap_init_failed", $"reason={error}; msg={message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args.purchasedProduct;
            Debug.Log($"[IAP-DBG] ===== ProcessPurchase() =====");
            Debug.Log($"[IAP-DBG] Product: {product.definition.id}");
            Debug.Log($"[IAP-DBG] TransactionID: {product.transactionID}");
            Debug.Log($"[IAP-DBG] Receipt length: {product.receipt?.Length ?? 0}");
            Debug.Log($"[IAP-DBG] Receipt(first 200): {product.receipt?.Substring(0, Math.Min(200, product.receipt?.Length ?? 0))}");

            var receipt = product.receipt;
            var productId = product.definition.id;
            RemoteLog.Event("process_purchase",
                $"productId={productId}; txId={product.transactionID}; receiptLen={receipt?.Length ?? 0}");
            Debug.Log("[IAP-DBG] Starting server receipt validation...");
            ValidateOnServer(receipt, productId, e =>
            {
                // User just completed a purchase: if the server is unreachable (technical
                // failure), grant optimistically — the next /status refresh will reconcile.
                // If the server explicitly said "not active" (invalid receipt), do NOT grant.
                ApplyEntitlement(e, optimisticOnError: true);
                bool granted = IsSubscribed;
                Debug.Log($"[IAP-DBG] ProcessPurchase → ok={e.ok}, active={e.active}, granted={granted}");
                RemoteLog.Event("purchase_validated",
                    $"ok={e.ok}; active={e.active}; source={e.source}; granted={granted}");
                SyncState("purchase");
                _purchaseCallback?.Invoke(granted);
                _purchaseCallback = null;
            });

            Debug.Log("[IAP-DBG] Returning PurchaseProcessingResult.Complete");
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            Debug.LogError($"[IAP-DBG] ===== OnPurchaseFailed(reason) =====");
            Debug.LogError($"[IAP-DBG] Product: {product.definition.id}");
            Debug.LogError($"[IAP-DBG] Reason: {reason}");
            Debug.LogError($"[IAP-DBG] Product available: {product.availableToPurchase}");
            Debug.LogError($"[IAP-DBG] Product hasReceipt: {product.hasReceipt}");

            // "You're already subscribed" — StoreKit rejects the duplicate purchase but the
            // user IS entitled. Restore + server-validate so premium turns on.
            if (reason == PurchaseFailureReason.DuplicateTransaction ||
                reason == PurchaseFailureReason.ExistingPurchasePending)
            {
                Debug.Log("[IAP-DBG] Duplicate/existing purchase — triggering Restore to recover entitlement");
                RestorePurchases(null);
            }

            RemoteLog.Event("purchase_failed", $"productId={product.definition.id}; reason={reason}");
            Analytics.Log("purchase_error",
                ("product_id", product.definition.id), ("error_code", reason.ToString()));
            PurchaseFailed?.Invoke(reason.ToString());
            _purchaseCallback?.Invoke(false);
            _purchaseCallback = null;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription desc)
        {
            Debug.LogError($"[IAP-DBG] ===== OnPurchaseFailed(description) =====");
            Debug.LogError($"[IAP-DBG] Product: {product.definition.id}");
            Debug.LogError($"[IAP-DBG] Reason: {desc.reason}");
            Debug.LogError($"[IAP-DBG] Message: {desc.message}");
            Debug.LogError($"[IAP-DBG] Product available: {product.availableToPurchase}");
            Debug.LogError($"[IAP-DBG] Product hasReceipt: {product.hasReceipt}");
            RemoteLog.Event("purchase_failed", $"productId={product.definition.id}; reason={desc.reason}; msg={desc.message}");
            Analytics.Log("purchase_error",
                ("product_id", product.definition.id), ("error_code", desc.reason.ToString()),
                ("error_message", desc.message ?? ""));
            PurchaseFailed?.Invoke(desc.message);
            _purchaseCallback?.Invoke(false);
            _purchaseCallback = null;
        }

        // ── Public API ──────────────────────────────────────

        public void Purchase(string productId, Action<bool> callback = null)
        {
            Debug.Log($"[IAP-DBG] ===== Purchase() called =====");
            Debug.Log($"[IAP-DBG] productId={productId}");
            Debug.Log($"[IAP-DBG] _store is null: {_store == null}");
            Debug.Log($"[IAP-DBG] IsInitialized: {IsInitialized}");

            RemoteLog.Event("purchase_start", $"productId={productId}; storeReady={_store != null}");

            if (_store == null)
            {
                Debug.LogError("[IAP-DBG] Store NOT initialized! Cannot purchase.");
                Debug.LogError("[IAP-DBG] Hint: Was OnInitializeFailed called earlier? Check logs above.");
                RemoteLog.Event("purchase_abort", "store_not_initialized");
                callback?.Invoke(false);
                return;
            }

            var product = _store.products.WithID(productId);
            Debug.Log($"[IAP-DBG] Product found: {product != null}");
            if (product != null)
            {
                Debug.Log($"[IAP-DBG] Product available: {product.availableToPurchase}");
                Debug.Log($"[IAP-DBG] Product price: {product.metadata?.localizedPriceString}");
                Debug.Log($"[IAP-DBG] Product hasReceipt: {product.hasReceipt}");
            }

            _purchaseCallback = callback;
            Debug.Log($"[IAP-DBG] Calling _store.InitiatePurchase({productId})...");
            _store.InitiatePurchase(productId);
        }

        public void RestorePurchases(Action<bool> callback = null)
        {
            Debug.Log("[IAP-DBG] ===== RestorePurchases() called =====");
            Debug.Log($"[IAP-DBG] Platform: {Application.platform}");
            Debug.Log($"[IAP-DBG] _extensions is null: {_extensions == null}");
            RemoteLog.Event("restore_start", $"platform={Application.platform}");

#if UNITY_IOS
            Debug.Log("[IAP-DBG] iOS path — calling RestoreTransactions...");
            try
            {
                _extensions.GetExtension<IAppleExtensions>()
                    .RestoreTransactions((success, error) =>
                    {
                        Debug.Log($"[IAP-DBG] RestoreTransactions callback: success={success}, error={error}");
                        RemoteLog.Event("restore_callback", $"success={success}; error={error}");
                        if (!success)
                        {
                            callback?.Invoke(false);
                            return;
                        }
                        // Wait for the server verdict before answering the UI — avoids the
                        // race where the callback reports a stale IsSubscribed=false.
                        ReconcileEntitlement(premium =>
                        {
                            RemoteLog.Event("restore_result", $"premium={premium}");
                            callback?.Invoke(premium);
                        });
                    });
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAP-DBG] RestoreTransactions EXCEPTION: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                callback?.Invoke(false);
            }
#elif UNITY_ANDROID
            Debug.Log("[IAP-DBG] Android path — Google Play restores on init");
            ReconcileEntitlement(premium =>
            {
                RemoteLog.Event("restore_result", $"premium={premium}");
                callback?.Invoke(premium);
            });
#else
            Debug.Log("[IAP-DBG] Unsupported platform for restore");
            callback?.Invoke(false);
#endif
        }

        public string GetLocalizedPrice(string productId)
        {
            Debug.Log($"[IAP-DBG] GetLocalizedPrice({productId}), _store null={_store == null}");
            if (_store == null)
            {
                Debug.LogWarning($"[IAP-DBG] GetLocalizedPrice: store is null, returning null");
                return null;
            }
            var product = _store.products.WithID(productId);
            var price = product?.metadata.localizedPriceString;
            Debug.Log($"[IAP-DBG] GetLocalizedPrice({productId}): product found={product != null}, price={price}");
            return price;
        }

        public decimal GetDecimalPrice(string productId)
        {
            if (_store == null) return 0m;
            var product = _store.products.WithID(productId);
            return product?.metadata.localizedPrice ?? 0m;
        }

        public string GetIsoCurrencyCode(string productId)
        {
            if (_store == null) return null;
            var product = _store.products.WithID(productId);
            return product?.metadata.isoCurrencyCode;
        }

        public bool HasTrialAvailable(string productId)
        {
            Debug.Log($"[IAP-DBG] HasTrialAvailable({productId})");
            if (_store == null)
            {
                Debug.LogWarning("[IAP-DBG] HasTrialAvailable: store is null");
                return false;
            }
            var product = _store.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                Debug.LogWarning($"[IAP-DBG] HasTrialAvailable: product null={product == null}, available={product?.availableToPurchase}");
                return false;
            }

            try
            {
                Debug.Log($"[IAP-DBG] HasTrialAvailable: checking introductory price info...");
                Debug.Log($"[IAP-DBG] HasTrialAvailable: receipt={product.receipt?.Substring(0, Math.Min(100, product.receipt?.Length ?? 0))}");

                // On iOS, SubscriptionManager needs the Apple product's intro JSON.
                // Passing null can cause exceptions on real devices.
                var introJson = (string)null;
#if UNITY_IOS
                if (_extensions != null)
                {
                    var appleExt = _extensions.GetExtension<IAppleExtensions>();
                    var dict = appleExt.GetIntroductoryPriceDictionary();
                    if (dict != null && dict.ContainsKey(product.definition.storeSpecificId))
                    {
                        introJson = dict[product.definition.storeSpecificId];
                        Debug.Log($"[IAP-DBG] HasTrialAvailable: introJson={introJson}");
                    }
                    else
                    {
                        Debug.LogWarning($"[IAP-DBG] HasTrialAvailable: No intro price info for {product.definition.storeSpecificId}. Dict keys: {(dict != null ? string.Join(", ", dict.Keys) : "null")}");
                    }
                }
#endif

                if (string.IsNullOrEmpty(product.receipt))
                {
                    Debug.Log("[IAP-DBG] HasTrialAvailable: no receipt — trial available");
                    return true;
                }

                var sub = new SubscriptionManager(product, introJson);
                var info = sub.getSubscriptionInfo();
                var isTrial = info.isFreeTrial();
                Debug.Log($"[IAP-DBG] HasTrialAvailable: isFreeTrial={isTrial}, isSubscribed={info.isSubscribed()}, isExpired={info.isExpired()}, remainingTime={info.getRemainingTime()}");
                return isTrial == Result.True;
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAP-DBG] HasTrialAvailable EXCEPTION: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // ── Internal ────────────────────────────────────────

        /// <summary>
        /// Grant-only local scan. Looks for a StoreKit/Play receipt and, if present,
        /// (a) optimistically enables premium offline and (b) sends it to the server for
        /// the authoritative record. It NEVER disables premium — only the server (via
        /// <see cref="RefreshEntitlement"/>) may downgrade, and only on an expired subscription.
        /// </summary>
        private void CheckSubscription()
        {
            Debug.Log("[IAP-DBG] ===== CheckSubscription() =====");
            if (_store == null)
            {
                Debug.LogWarning("[IAP-DBG] CheckSubscription: store is null, aborting");
                return;
            }

            var product = FindReceiptProduct(out var id);
            if (product == null)
            {
                Debug.Log("[IAP-DBG] CheckSub: no product with receipt");
                return;
            }

            // Offline optimistic grant from the local receipt parse (never revokes).
            if (LocalReceiptActive(product) && !IsSubscribed)
            {
                Debug.Log($"[IAP-DBG] CheckSub [{id}]: local receipt active → optimistic grant");
                IsSubscribed = true;
            }

            // Authoritative: let the server validate + record this receipt (fire-and-forget).
            ValidateOnServer(product.receipt, id, e =>
                ApplyEntitlement(e, optimisticOnError: false));
        }

        /// <summary>First owned product that carries a store receipt, or null.</summary>
        private Product FindReceiptProduct(out string productId)
        {
            productId = null;
            if (_store == null) return null;
            foreach (var id in new[] { ProductMonthly, ProductYearly })
            {
                var product = _store.products.WithID(id);
                if (product != null && product.hasReceipt)
                {
                    productId = id;
                    return product;
                }
            }
            return null;
        }

        /// <summary>Local StoreKit/Play receipt parse — true if it reports an active sub.
        /// Grant-only signal; never used to revoke (it's flaky on device).</summary>
        private bool LocalReceiptActive(Product product)
        {
            try
            {
                var introJson = (string)null;
#if UNITY_IOS
                if (_extensions != null)
                {
                    try
                    {
                        var appleExt = _extensions.GetExtension<IAppleExtensions>();
                        var dict = appleExt.GetIntroductoryPriceDictionary();
                        if (dict != null && dict.ContainsKey(product.definition.storeSpecificId))
                            introJson = dict[product.definition.storeSpecificId];
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[IAP-DBG] LocalReceiptActive: intro json failed: {ex.Message}");
                    }
                }
#endif
                var sub = new SubscriptionManager(product, introJson);
                var info = sub.getSubscriptionInfo();
                Debug.Log($"[IAP-DBG] LocalReceiptActive [{product.definition.id}]: isSubscribed={info.isSubscribed()}, isExpired={info.isExpired()}");
                return info.isSubscribed() == Result.True;
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAP-DBG] LocalReceiptActive EXCEPTION: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ask the server for the current entitlement (GET /status) and reconcile local
        /// premium. Call on every app start (after login). Safe to call anytime.
        /// </summary>
        public void RefreshEntitlement()
        {
            var service = FindAnyObjectByType<SubscriptionService>();
            if (service == null)
            {
                Debug.LogWarning("[IAP-DBG] RefreshEntitlement: no SubscriptionService — keeping cache");
                return;
            }
            service.CheckStatus(e =>
            {
                RemoteLog.Event("status_result",
                    $"ok={e.ok}; active={e.active}; source={e.source}; " +
                    $"expires={(e.hasExpiry ? e.expiresUtc.ToString("o") : "null")}; cachedPremium={IsSubscribed}");
                ApplyEntitlement(e, optimisticOnError: false);
            });
        }

        /// <summary>
        /// Authoritative reconcile with a completion that fires ONLY after the server
        /// round-trip — used by Restore so the UI never decides on a stale premium flag.
        /// Validates a store receipt if present, otherwise falls back to /status
        /// (covers promo / server-granted premium). <paramref name="done"/> gets the
        /// final <see cref="IsSubscribed"/>.
        /// </summary>
        public void ReconcileEntitlement(Action<bool> done)
        {
            var service = FindAnyObjectByType<SubscriptionService>();
            var product = FindReceiptProduct(out var id);

            // Local optimistic grant first (cheap, offline).
            if (product != null && LocalReceiptActive(product) && !IsSubscribed)
                IsSubscribed = true;

            if (service == null)
            {
                Debug.LogWarning("[IAP-DBG] ReconcileEntitlement: no SubscriptionService — returning local state");
                done?.Invoke(IsSubscribed);
                return;
            }

            if (product != null)
            {
                service.ValidateReceipt(product.receipt, id, e =>
                {
                    ApplyEntitlement(e, optimisticOnError: false);
                    done?.Invoke(IsSubscribed);
                });
            }
            else
            {
                // Nothing to validate locally — ask the server (promo / other device).
                service.CheckStatus(e =>
                {
                    ApplyEntitlement(e, optimisticOnError: false);
                    done?.Invoke(IsSubscribed);
                });
            }
        }

        /// <summary>
        /// Single reconciliation rule (the "golden rule"):
        ///  • technical failure (no net / 5xx) → keep cache (optionally grant if we just paid);
        ///  • server says active → enable premium;
        ///  • server says NOT active → downgrade ONLY if it also returned an expiry in the past.
        /// Premium is never silently dropped for any other reason.
        /// </summary>
        private void ApplyEntitlement(Entitlement e, bool optimisticOnError)
        {
            if (!e.ok)
            {
                if (optimisticOnError && !IsSubscribed)
                {
                    Debug.Log("[IAP-DBG] ApplyEntitlement: server unreachable after purchase → optimistic grant");
                    IsSubscribed = true;
                }
                return; // keep current cache
            }

            if (e.active)
            {
                PremiumExpiresUtc = e.hasExpiry ? e.expiresUtc : (DateTime?)null;
                if (!string.IsNullOrEmpty(e.source))
                {
                    PlayerPrefs.SetString("ft_premium_source", e.source);
                    PlayerPrefs.Save();
                }
                if (!IsSubscribed) IsSubscribed = true;
                else { Analytics.RefreshUserProps(); OnSubscriptionChanged?.Invoke(true); } // refresh label even if flag unchanged
                return;
            }

            // Server says inactive. Only trust a downgrade backed by an expired date.
            if (e.hasExpiry && e.expiresUtc <= DateTime.UtcNow)
            {
                if (IsSubscribed)
                {
                    Debug.Log($"[IAP-DBG] ApplyEntitlement: server inactive + expired {e.expiresUtc:o} → downgrade");
                    IsSubscribed = false;
                }
            }
            else
            {
                Debug.Log("[IAP-DBG] ApplyEntitlement: inactive without past expiry → keeping cache (no downgrade)");
            }
        }

        /// <summary>
        /// Send a full snapshot of the device's subscription state to the server for
        /// monitoring/control. Safe to call anytime; no-op if store/service not ready.
        /// </summary>
        public void SyncState(string context)
        {
            var service = FindAnyObjectByType<SubscriptionService>();
            if (service == null || _store == null) return;

            var products = new System.Collections.Generic.List<ProductState>();
            foreach (var id in new[] { ProductMonthly, ProductYearly })
            {
                var p = _store.products.WithID(id);
                if (p == null) continue;

                var ps = new ProductState
                {
                    id = id,
                    available = p.availableToPurchase,
                    hasReceipt = p.hasReceipt,
                    price = p.metadata?.localizedPriceString ?? "",
                    currency = p.metadata?.isoCurrencyCode ?? ""
                };

                if (p.hasReceipt)
                {
                    try
                    {
                        var sub = new SubscriptionManager(p, null);
                        var info = sub.getSubscriptionInfo();
                        ps.isSubscribed = info.isSubscribed() == Result.True;
                        ps.isExpired = info.isExpired() == Result.True;
                        try { ps.expiresUtc = info.getExpireDate().ToUniversalTime().ToString("o"); }
                        catch { ps.expiresUtc = ""; }
                    }
                    catch { /* local parse is best-effort */ }
                }
                products.Add(ps);
            }

            var payload = new SyncPayload
            {
                userId = PlayerPrefs.GetString("ft_userId", ""),
                platform = Application.platform.ToString(),
                appVersion = Application.version,
                context = context,
                cachedPremium = IsSubscribed,
                products = products.ToArray(),
                ts = DateTime.UtcNow.ToString("o")
            };
            service.Sync(JsonUtility.ToJson(payload));
        }

        [Serializable]
        private class SyncPayload
        {
            public string userId;
            public string platform;
            public string appVersion;
            public string context;      // "init" | "purchase" | "restore" | ...
            public bool cachedPremium;
            public ProductState[] products;
            public string ts;
        }

        [Serializable]
        private class ProductState
        {
            public string id;
            public bool available;
            public bool hasReceipt;
            public string price;
            public string currency;
            public bool isSubscribed;
            public bool isExpired;
            public string expiresUtc;
        }

        private void ValidateOnServer(string receipt, string productId, Action<Entitlement> callback)
        {
            var service = FindAnyObjectByType<SubscriptionService>();
            Debug.Log($"[IAP-DBG] ValidateOnServer: SubscriptionService found={service != null}, receipt len={receipt?.Length ?? 0}");
            if (service != null)
            {
                service.ValidateReceipt(receipt, productId, callback);
                return;
            }
            // No server component — can't verify. Report technical failure so callers keep cache.
            Debug.LogWarning("[IAP-DBG] ValidateOnServer: no SubscriptionService — reporting failure (keep cache)");
            callback?.Invoke(Entitlement.Failed);
        }
    }
}
