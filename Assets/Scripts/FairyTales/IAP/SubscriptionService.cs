using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using FairyTales.Api;

namespace FairyTales.IAP
{
    /// <summary>
    /// Result of a server entitlement call (/validate or /status).
    /// The server is the single source of truth for premium.
    /// </summary>
    public struct Entitlement
    {
        /// <summary>True when the request technically succeeded (HTTP 2xx + parsed).
        /// False = network error / 5xx — caller must KEEP the current cache, not downgrade.</summary>
        public bool ok;
        /// <summary>Server verdict: is the subscription currently active.</summary>
        public bool active;
        /// <summary>True when the server returned a non-empty expiresAt.</summary>
        public bool hasExpiry;
        /// <summary>Expiry in UTC (valid only when hasExpiry).</summary>
        public DateTime expiresUtc;
        public string source;     // apple | google | promo | null
        public string productId;

        public static Entitlement Failed => new Entitlement { ok = false };
    }

    public class SubscriptionService : MonoBehaviour
    {
        private ApiClient _api;

        private void Awake()
        {
            Debug.Log("[IAP-DBG] SubscriptionService.Awake()");
            _api = FindAnyObjectByType<ApiClient>();
            Debug.Log($"[IAP-DBG] SubscriptionService: ApiClient found={_api != null}");
            if (_api == null)
                Debug.LogError("[IAP-DBG] SubscriptionService: ApiClient NOT FOUND! Server validation will fail.");
        }

        /// <summary>
        /// POST /api/subscription/validate — call after purchase, restore, or "already owned".
        /// Sends the raw Unity receipt wrapper (server unwraps it itself).
        /// </summary>
        public void ValidateReceipt(string receipt, string productId, Action<Entitlement> callback)
        {
            Debug.Log($"[IAP-DBG] SubscriptionService.ValidateReceipt() receipt len={receipt?.Length ?? 0}, productId={productId}");
            if (_api == null)
            {
                Debug.LogError("[IAP-DBG] ValidateReceipt: _api is null!");
                callback?.Invoke(Entitlement.Failed);
                return;
            }
            StartCoroutine(ValidateCoroutine(receipt, productId, callback));
        }

        /// <summary>GET /api/subscription/status — call on every app start (after login).</summary>
        public void CheckStatus(Action<Entitlement> callback)
        {
            Debug.Log("[IAP-DBG] SubscriptionService.CheckStatus() called");
            if (_api == null)
            {
                Debug.LogError("[IAP-DBG] CheckStatus: _api is null!");
                callback?.Invoke(Entitlement.Failed);
                return;
            }
            StartCoroutine(CheckStatusCoroutine(callback));
        }

        /// <summary>
        /// POST /api/subscription/sync — full device/subscription snapshot for server-side
        /// monitoring & control. Fire-and-forget.
        /// </summary>
        public void Sync(string bodyJson)
        {
            if (_api == null) return;
            StartCoroutine(SyncCoroutine(bodyJson));
        }

        private IEnumerator SyncCoroutine(string bodyJson)
        {
            yield return _api.PostJson("/api/subscription/sync", bodyJson,
                _ => Debug.Log("[IAP-DBG] Sync: sent"),
                e => Debug.LogWarning($"[IAP-DBG] Sync error: {e}"));
        }

        private IEnumerator ValidateCoroutine(string receipt, string productId, Action<Entitlement> callback)
        {
            var platform = "google";
#if UNITY_IOS
            platform = "apple";
#endif
            var body = JsonUtility.ToJson(new ReceiptPayload
            {
                receipt = receipt,
                platform = platform,
                productId = productId
            });
            Debug.Log($"[IAP-DBG] ValidateCoroutine: platform={platform}, body len={body.Length}, POST /api/subscription/validate");

            Entitlement result = Entitlement.Failed;

            yield return _api.PostJson("/api/subscription/validate", body,
                onSuccess: response =>
                {
                    Debug.Log($"[IAP-DBG] ValidateCoroutine: SUCCESS response={response}");
                    result = Parse(response);
                },
                onError: e =>
                {
                    // Network error or 5xx — technical failure, NOT "inactive".
                    Debug.LogError($"[IAP-DBG] ValidateCoroutine: ERROR: {e}");
                    result = Entitlement.Failed;
                });

            Debug.Log($"[IAP-DBG] ValidateCoroutine: ok={result.ok}, active={result.active}, expiresUtc={(result.hasExpiry ? result.expiresUtc.ToString("o") : "null")}, source={result.source}");
            callback?.Invoke(result);
        }

        private IEnumerator CheckStatusCoroutine(Action<Entitlement> callback)
        {
            Debug.Log("[IAP-DBG] CheckStatusCoroutine: GET /api/subscription/status");
            Entitlement result = Entitlement.Failed;

            yield return _api.Get("/api/subscription/status",
                onSuccess: json =>
                {
                    Debug.Log($"[IAP-DBG] CheckStatusCoroutine: SUCCESS json={json}");
                    result = Parse(json);
                },
                onError: e =>
                {
                    Debug.LogError($"[IAP-DBG] CheckStatusCoroutine: ERROR: {e}");
                    result = Entitlement.Failed;
                });

            Debug.Log($"[IAP-DBG] CheckStatusCoroutine: ok={result.ok}, active={result.active}, expiresUtc={(result.hasExpiry ? result.expiresUtc.ToString("o") : "null")}, source={result.source}");
            callback?.Invoke(result);
        }

        private static Entitlement Parse(string json)
        {
            var e = new Entitlement { ok = true };
            try
            {
                var dto = JsonUtility.FromJson<StatusResponse>(json);
                if (dto != null)
                {
                    e.active = dto.active;
                    e.source = dto.source;
                    e.productId = dto.productId;
                    if (!string.IsNullOrEmpty(dto.expiresAt) &&
                        DateTime.TryParse(dto.expiresAt, CultureInfo.InvariantCulture,
                            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                            out var exp))
                    {
                        e.hasExpiry = true;
                        e.expiresUtc = exp;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IAP-DBG] Entitlement.Parse failed: {ex.Message} json={json}");
                // Parsed as 200 but unreadable — treat as technical failure so we keep cache.
                return Entitlement.Failed;
            }
            return e;
        }

        [Serializable]
        private class ReceiptPayload
        {
            public string receipt;
            public string platform;
            public string productId;
        }

        [Serializable]
        private class StatusResponse
        {
            public bool active;
            public string expiresAt;
            public string source;
            public string productId;
        }
    }
}
