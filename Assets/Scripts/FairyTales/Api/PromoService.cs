using System;
using System.Collections;
using UnityEngine;

namespace FairyTales.Api
{
    public class PromoService
    {
        private readonly ApiClient _api;

        public PromoService(ApiClient api) => _api = api;

        public IEnumerator CheckPromo(string code,
            Action<PromoCheckResponse> onSuccess, Action<string> onError = null)
        {
            var body = JsonUtility.ToJson(new PromoCheckRequest { code = code, app = "BALA_STORIES" });
            Debug.Log($"[Promo] CheckPromo request: code={code}, body={body}, hasToken={_api.HasToken}");
            string resultJson = null;
            string resultErr = null;

            yield return _api.PostJson("/api/promo/check", body,
                json => resultJson = json,
                err => resultErr = err
            );

            Debug.Log($"[Promo] CheckPromo done: json={resultJson}, err={resultErr}");

            if (resultJson != null)
            {
                PromoCheckResponse response = null;
                // FromJson throws on an empty or malformed body. Uncaught, it kills the
                // coroutine mid-callback and leaves the Apply button disabled forever.
                try { response = JsonUtility.FromJson<PromoCheckResponse>(resultJson); }
                catch (Exception e) { Debug.LogWarning($"[Promo] bad response: {e.Message} json={resultJson}"); }
                onSuccess?.Invoke(response);
            }
            else
            {
                onError?.Invoke(resultErr ?? "unknown error");
            }
        }

        // /api/promo/purchase is gone on purpose: the server now stores the user→blogger
        // link itself and sees every payment (receipt validation + Apple S2S), so the
        // client no longer has to remember a code and report the purchase back.

        /// <summary>
        /// Raw ApiClient error ("[409] … — {…}") → Loc key. Keyed on the HTTP status,
        /// which is the stable part of the contract: on 404 the server's `error` field
        /// holds a Russian sentence, not a machine code, so the body can't be trusted
        /// to classify. Anything without a status is a network failure.
        /// </summary>
        public static string ErrorKey(string error)
        {
            var e = error ?? "";
            var end = e.IndexOf(']');
            if (end > 1 && e[0] == '[' && int.TryParse(e.Substring(1, end - 1), out var code))
            {
                switch (code)
                {
                    case 409: return "promo_already_used";   // this account already spent its one code
                    case 410: return "promo_exhausted";      // code hit its redemption limit
                    case 403: return "promo_self";           // own referral code — nobody was invited
                    case 404: return "promo_not_found";
                }
            }
            return "promo_no_connection";
        }

        // A code is redeemable once per account — the rule lives on the server (409).
        // This flag only keeps the UI from offering a field that can no longer succeed.
        private const string UsedKey = "ft_promo_used";

        public static bool WasUsed => !string.IsNullOrEmpty(UsedCode);
        public static string UsedCode => PlayerPrefs.GetString(UsedKey, "");

        public static void MarkUsed(string code)
        {
            PlayerPrefs.SetString(UsedKey, code);
            PlayerPrefs.Save();
        }

        [Serializable]
        private class PromoCheckRequest
        {
            public string code;
            public string app = "BALA_STORIES";
        }

        /// <summary>
        /// [Preserve] on purpose: IL2CPP managed stripping is on Medium for both stores,
        /// and a response model read by reflection is exactly what got emptied on device
        /// in build 2.6 (see <see cref="Models.NarratePayload"/>). Editor/Mono would never
        /// catch it.
        /// </summary>
        [Serializable]
        [UnityEngine.Scripting.Preserve]
        public class PromoCheckResponse
        {
            public string type;          // premium | blogger | tale | referral
            public int durationDays;
            public string bloggerName;
            public string message;
            public string[] taleIds;     // type == "tale": what the code opened
        }
    }
}
