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
                var response = JsonUtility.FromJson<PromoCheckResponse>(resultJson);
                onSuccess?.Invoke(response);
            }
            else
            {
                onError?.Invoke(resultErr ?? "unknown error");
            }
        }

        public IEnumerator Purchase(string code,
            Action<string> onSuccess, Action<string> onError = null)
        {
            var body = JsonUtility.ToJson(new PromoCheckRequest { code = code, app = "BALA_STORIES" });
            yield return _api.PostJson("/api/promo/purchase", body, onSuccess, onError);
        }

        [Serializable]
        private class PromoCheckRequest
        {
            public string code;
            public string app = "BALA_STORIES";
        }

        [Serializable]
        public class PromoCheckResponse
        {
            public string type;
            public int durationDays;
            public string bloggerName;
            public string message;
        }
    }
}
