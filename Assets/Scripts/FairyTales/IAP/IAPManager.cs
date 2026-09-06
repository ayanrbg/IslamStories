using System;
using System.Collections;
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
        public const string ProductMonthly = AppConfig.ProductMonthly;
        public const string ProductYearly = AppConfig.ProductYearly;

        // App Store Connect product IDs. ASC ids cannot be renamed after creation, so
        // they are not guaranteed to match the unified ids above. Google Play uses the
        // unified ids as-is.
        private const string AppleProductMonthly = AppConfig.AppleProductMonthly;
        private const string AppleProductYearly = AppConfig.AppleProductYearly;

        public static IAPManager Instance { get; private set; }

        public bool IsInitialized => _store != null;

        /// <summary>
        /// The store failed to initialise and nothing is retrying right now — the UI
        /// should offer a manual "retry" instead of pretending the store is loading.
        /// </summary>
        public bool StoreUnavailable => _store == null && !_initInFlight;

        /// <summary>Last initialisation failure reason, for UI diagnostics. null while fine.</summary>
        public string LastInitError { get; private set; }

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

        /// <summary>Raised when the product catalog becomes available (possibly after a
        /// retry, i.e. long after the paywall was already shown).</summary>
        public event Action OnStoreReady;

        /// <summary>Raised when initialisation fails and the store stays unusable.</summary>
        public event Action OnStoreFailed;

        private IStoreController _store;
        private IExtensionProvider _extensions;
        private Action<bool> _purchaseCallback;

        private bool _initInFlight;
        private double _initStartedAt;      // realtime stamp of the current attempt
        private int _initAttempts;          // monotonic — also the dedupe key for failures
        private int _autoRetries;           // consecutive automatic retries, reset on manual retry
        private int _failureHandledForAttempt = -1;

        // UnityPurchasing.Initialize registers its product-service listeners with "+=" on a
        // singleton service, so every retry leaves the previous handlers attached and a
        // single fetch failure arrives once per past attempt. Failures are therefore
        // deduped by attempt number. Unity itself already retries the fetch 5 times
        // internally, so our own auto-retries stay few and slow — the real recovery path
        // is the manual retry on the paywall.
        private const int MaxAutoRetries = 2;

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

        /// <summary>
        /// Re-run initialisation after a failure. Safe to call from UI at any time —
        /// no-op when the store is already up or an attempt is in flight.
        /// </summary>
        public void RetryInitialize()
        {
            Debug.Log($"[IAP-DBG] RetryInitialize() requested (store={_store != null}, inFlight={_initInFlight})");
            if (_store != null || _initInFlight) return;
            _autoRetries = 0;
            RemoteLog.Event("iap_retry_manual", $"platform={Application.platform}");
            InitStore();
        }

        private void InitStore()
        {
            Debug.Log($"[IAP-DBG] ===== InitStore() START (attempt {_initAttempts + 1}) =====");
            if (_initInFlight)
            {
                Debug.LogWarning("[IAP-DBG] InitStore: attempt already in flight, skipping");
                return;
            }
            _initInFlight = true;
            _initAttempts++;
            _initStartedAt = Time.realtimeSinceStartupAsDouble;
            RemoteLog.Event("iap_init_start",
                $"attempt={_initAttempts}; auto={_autoRetries}; " +
                $"bundle={Application.identifier}; net={Application.internetReachability}");
            try
            {
                Debug.Log("[IAP-DBG] Creating StandardPurchasingModule...");
                var module = StandardPurchasingModule.Instance();
                Debug.Log($"[IAP-DBG] StandardPurchasingModule created, useFakeStoreUIMode={module.useFakeStoreUIMode}, useFakeStoreAlways={module.useFakeStoreAlways}");

                var builder = ConfigurationBuilder.Instance(module);
                Debug.Log("[IAP-DBG] ConfigurationBuilder created");

                builder.AddProduct(ProductMonthly, ProductType.Subscription,
                    StoreIds(AppleProductMonthly, ProductMonthly));
                Debug.Log($"[IAP-DBG] Added product: {ProductMonthly} (Subscription, storeId={StoreIdFor(AppleProductMonthly, ProductMonthly)})");

                builder.AddProduct(ProductYearly, ProductType.Subscription,
                    StoreIds(AppleProductYearly, ProductYearly));
                Debug.Log($"[IAP-DBG] Added product: {ProductYearly} (Subscription, storeId={StoreIdFor(AppleProductYearly, ProductYearly)})");

                Debug.Log("[IAP-DBG] Calling UnityPurchasing.Initialize()...");
                UnityPurchasing.Initialize(this, builder);
                Debug.Log("[IAP-DBG] UnityPurchasing.Initialize() called (async, waiting for callback)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAP-DBG] EXCEPTION in InitStore(): {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                _initInFlight = false;
                LastInitError = e.Message;
                ScheduleRetry();
            }
        }

        /// <summary>
        /// Store-specific id map for the CURRENT platform only.
        ///
        /// Unity IAP 5.1.2's legacy path (<c>UnityPurchasing.Initialize</c>) fetches the
        /// catalog via <c>CatalogProvider.GetProducts()</c> with no store name, so
        /// <c>UpdateStoreSpecificIDs</c> — the only code that picks the id matching the
        /// running store — never executes. What actually reaches StoreKit / Play is
        /// whatever <c>AddStoreSpecificIds</c> returned: the LAST entry added. Listing both
        /// stores therefore sent Google's id to Apple, which answered "no such products"
        /// and looked exactly like a misconfigured App Store Connect.
        ///
        /// Registering one entry makes "last wins" deterministic and correct.
        /// (The codeless path passes the store name properly — this is a legacy-wrapper bug.)
        /// </summary>
        private static StoreSpecificIds StoreIds(string appleId, string googleId)
        {
#if UNITY_IOS
            return new StoreSpecificIds { { appleId, AppleAppStore.Name } };
#else
            return new StoreSpecificIds { { googleId, GooglePlay.Name } };
#endif
        }

        /// <summary>The id <see cref="StoreIds"/> will actually request, for logging.</summary>
        private static string StoreIdFor(string appleId, string googleId)
        {
#if UNITY_IOS
            return appleId;
#else
            return googleId;
#endif
        }

        /// <summary>Back off and try again — the store is often just slow to answer
        /// right after launch (cold StoreKit, flaky network).</summary>
        private void ScheduleRetry()
        {
            if (_autoRetries >= MaxAutoRetries)
            {
                Debug.LogWarning($"[IAP-DBG] ScheduleRetry: giving up after {_initAttempts} attempts — waiting for manual retry");
                OnStoreFailed?.Invoke();
                return;
            }
            _autoRetries++;
            var delay = 15f * _autoRetries;
            Debug.Log($"[IAP-DBG] ScheduleRetry: retrying in {delay}s (auto-retry {_autoRetries} of {MaxAutoRetries})");
            OnStoreFailed?.Invoke();
            StartCoroutine(RetryAfter(delay));
        }

        private IEnumerator RetryAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_store == null) InitStore();
        }

        // ── Diagnostics helpers ─────────────────────────────

        /// <summary>Milliseconds since the current attempt called Initialize. A fast
        /// failure means StoreKit rejected locally; ~1-3 s means Apple actually answered.
        /// That distinction decides whether to look at the device or at the store.</summary>
        private long ElapsedSinceInitStart()
            => (long)((Time.realtimeSinceStartupAsDouble - _initStartedAt) * 1000d);

        private static int CountAll(IStoreController controller)
            => controller?.products?.all?.Length ?? -1;

        /// <summary>Compact one-line product state for remote reports.</summary>
        private static string Describe(IStoreController controller, string productId)
        {
            var p = controller?.products?.WithID(productId);
            if (p == null) return "absent";
            return $"storeId={p.definition.storeSpecificId}; type={p.definition.type}; " +
                   $"avail={p.availableToPurchase}; receipt={p.hasReceipt}; " +
                   $"price={p.metadata?.localizedPriceString ?? "null"}; " +
                   $"cur={p.metadata?.isoCurrencyCode ?? "null"}; " +
                   $"title={(string.IsNullOrEmpty(p.metadata?.localizedTitle) ? "EMPTY" : "ok")}";
        }

        /// <summary>
        /// Sandbox and production receipts validate against different Apple endpoints, so
        /// knowing which one a device produced explains most "valid receipt rejected"
        /// reports. Derived from the Unity receipt wrapper without parsing the payload.
        /// </summary>
        private static string ReceiptEnvironment(string receipt)
        {
            if (string.IsNullOrEmpty(receipt)) return "none";
            if (receipt.Contains("\"Store\":\"AppleAppStore\"")) return "apple";
            if (receipt.Contains("\"Store\":\"GooglePlay\"")) return "google";
            if (receipt.Contains("fake")) return "fake";
            return "unknown";
        }

        // ── IStoreListener ──────────────────────────────────

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("[IAP-DBG] ===== OnInitialized() SUCCESS =====");
            _store = controller;
            _extensions = extensions;
            _initInFlight = false;
            _autoRetries = 0;
            LastInitError = null;

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

            RemoteLog.Event("iap_init_ok",
                $"attempt={_initAttempts}; elapsedMs={ElapsedSinceInitStart()}; " +
                $"cachedPremium={IsSubscribed}; catalogCount={CountAll(controller)}; " +
                $"monthly[{Describe(controller, ProductMonthly)}]; " +
                $"yearly[{Describe(controller, ProductYearly)}]");

            CheckSubscription();
            SyncState("init");
            OnStoreReady?.Invoke();
        }

        public void OnInitializeFailed(InitializationFailureReason error)
            => HandleInitFailure(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string message)
            => HandleInitFailure(error, message);

        private void HandleInitFailure(InitializationFailureReason error, string message)
        {
            if (_store != null) return; // late callback from a stale listener
            if (_failureHandledForAttempt == _initAttempts)
            {
                Debug.Log($"[IAP-DBG] Duplicate init failure for attempt {_initAttempts} — ignored");
                return;
            }
            _failureHandledForAttempt = _initAttempts;
            _initInFlight = false;
            LastInitError = message ?? error.ToString();

            Debug.LogError($"[IAP-DBG] ===== OnInitializeFailed (attempt {_initAttempts}) =====");
            Debug.LogError($"[IAP-DBG] Reason: {error}");
            if (message != null) Debug.LogError($"[IAP-DBG] Message: {message}");
            Debug.LogError($"[IAP-DBG] Platform: {Application.platform}");
            Debug.LogError($"[IAP-DBG] Bundle id: {Application.identifier}");
            // The unified ids are what the code asks for; the store-specific ids below are
            // what actually goes to StoreKit / Play. A mismatch here is the usual culprit.
            Debug.LogError($"[IAP-DBG] Unified ids: {ProductMonthly}, {ProductYearly}");
            Debug.LogError($"[IAP-DBG] Requested store ids: " +
                $"{StoreIdFor(AppleProductMonthly, ProductMonthly)}, " +
                $"{StoreIdFor(AppleProductYearly, ProductYearly)}");
            Debug.LogError($"[IAP-DBG] Hint: Paid Applications agreement must be Active (ASC → Business)");
            Debug.LogError($"[IAP-DBG] Hint: Check bank account + tax forms — they gate the agreement");
            Debug.LogError($"[IAP-DBG] Hint: Ensure 'In-App Purchase' capability is enabled in Xcode");
            Debug.LogError($"[IAP-DBG] Hint: Product ids must match App Store Connect EXACTLY");

            RemoteLog.Event("iap_init_failed",
                $"reason={error}; msg={message}; attempt={_initAttempts}; " +
                $"elapsedMs={ElapsedSinceInitStart()}; " +
                $"bundle={Application.identifier}; net={Application.internetReachability}; " +
                $"requested={StoreIdFor(AppleProductMonthly, ProductMonthly)}/" +
                $"{StoreIdFor(AppleProductYearly, ProductYearly)}");

            ScheduleRetry();
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
                $"productId={productId}; storeId={product.definition.storeSpecificId}; " +
                $"txId={product.transactionID}; receiptLen={receipt?.Length ?? 0}; " +
                $"receiptEnv={ReceiptEnvironment(receipt)}");

#if UNITY_EDITOR
            // FakeStore receipts can never pass server validation, so a purchase in the
            // editor would always end with "not active" and premium would stay off.
            // Grant locally so the paywall flow stays testable outside a device build.
            Debug.Log("[IAP-DBG] Editor: fake purchase — granting premium locally, skipping server validation");
            IsSubscribed = true;
            _purchaseCallback?.Invoke(true);
            _purchaseCallback = null;
            return PurchaseProcessingResult.Complete;
#else
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
#endif
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

            RemoteLog.Event("purchase_failed",
                $"productId={product.definition.id}; storeId={product.definition.storeSpecificId}; " +
                $"reason={reason}; avail={product.availableToPurchase}; receipt={product.hasReceipt}");
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
            RemoteLog.Event("purchase_failed",
                $"productId={product.definition.id}; storeId={product.definition.storeSpecificId}; " +
                $"reason={desc.reason}; msg={desc.message}; " +
                $"avail={product.availableToPurchase}; receipt={product.hasReceipt}");
            Analytics.Log("purchase_error",
                ("product_id", product.definition.id), ("error_code", desc.reason.ToString()),
                ("error_message", desc.message ?? ""));
            // The reason, not the raw message: the UI has to tell a user cancellation
            // apart from a real failure, and desc.message is free-form store text.
            PurchaseFailed?.Invoke(desc.reason.ToString());
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

            RemoteLog.Event("purchase_start",
                $"productId={productId}; storeReady={_store != null}; " +
                $"cachedPremium={IsSubscribed}; net={Application.internetReachability}; " +
                $"state[{Describe(_store, productId)}]");

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

            // Unity IAP silently ignores InitiatePurchase for an id the store never
            // returned — no callback, no failure event — which leaves the caller waiting
            // forever and its CTA stuck disabled. Answer here instead so the UI can react.
            if (product == null || !product.availableToPurchase)
            {
                Debug.LogError($"[IAP-DBG] Product unusable (found={product != null}, available={product?.availableToPurchase}) — aborting purchase");
                RemoteLog.Event("purchase_abort",
                    $"product_unavailable; productId={productId}; found={product != null}");
                PurchaseFailed?.Invoke(PurchaseFailureReason.ProductUnavailable.ToString());
                callback?.Invoke(false);
                return;
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
                    // No receipt means "never purchased", NOT "a trial exists". Claiming a
                    // free trial the store doesn't actually offer is exactly the misleading
                    // marketing Guideline 3.1.2(c) rejects — so on iOS require positive
                    // evidence from StoreKit's introductory-offer data.
#if UNITY_IOS
                    var hasIntroOffer = !string.IsNullOrEmpty(introJson);
                    Debug.Log($"[IAP-DBG] HasTrialAvailable: no receipt, introOffer={hasIntroOffer}");
                    return hasIntroOffer;
#else
                    Debug.Log("[IAP-DBG] HasTrialAvailable: no receipt — trial available");
                    return true;
#endif
                }

                if (!TryGetSubscriptionInfo(product, introJson, out var info)) return false;

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

        /// <summary>
        /// Local receipt parse, guarded. SubscriptionManager refuses stores it cannot read —
        /// notably the editor's fake store — by throwing, which is a normal outcome here,
        /// not an error. Returns false and leaves <paramref name="info"/> null in that case.
        /// </summary>
        private static bool TryGetSubscriptionInfo(Product product, string introJson, out SubscriptionInfo info)
        {
            info = null;
            try
            {
                info = new SubscriptionManager(product, introJson).getSubscriptionInfo();
                return info != null;
            }
            catch (StoreSubscriptionInfoNotSupportedException e)
            {
                Debug.Log($"[IAP-DBG] No local subscription info for {product.definition.id}: {e.Message}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAP-DBG] Subscription info parse failed for {product.definition.id}: {e.GetType().Name}: {e.Message}");
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
                if (!TryGetSubscriptionInfo(product, introJson, out var info)) return false;

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

                if (p.hasReceipt && TryGetSubscriptionInfo(p, null, out var info))
                {
                    try
                    {
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
