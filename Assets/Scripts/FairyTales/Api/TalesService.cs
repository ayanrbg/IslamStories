using System;
using System.Collections;
using FairyTales.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FairyTales.Api
{
    public class TalesService
    {
        private readonly ApiClient _api;

        public TalesService(ApiClient api) => _api = api;

        public IEnumerator GetTales(string lang,
            Action<TaleSummary[]> onSuccess, Action<string> onError = null)
        {
            var query = string.IsNullOrEmpty(lang) ? "" : $"?lang={lang}";

            yield return _api.Get($"/api/tales{query}",
                json =>
                {
                    var tales = JsonConvert.DeserializeObject<TaleSummary[]>(json);
                    onSuccess?.Invoke(tales);
                },
                onError
            );
        }

        public IEnumerator GetTale(string taleId,
            Action<TaleDetail> onSuccess, Action<string> onError = null,
            string lang = null)
        {
            var query = string.IsNullOrEmpty(lang) ? "" : $"?lang={lang}";

            yield return _api.Get($"/api/tales/{taleId}{query}",
                json =>
                {
                    var tale = JsonConvert.DeserializeObject<TaleDetail>(json);
                    onSuccess?.Invoke(tale);
                },
                onError
            );
        }

        public IEnumerator Personalize(string taleId, string name, string gender,
            Action<string[]> onSuccess, Action<string> onError = null, string lang = null)
        {
            var body = JsonUtility.ToJson(new PersonalizeRequest
            {
                name = name, gender = gender
            });

            var query = string.IsNullOrEmpty(lang) ? "" : $"?lang={lang}";
            yield return _api.PostJson($"/api/tales/{taleId}/personalize{query}", body,
                json =>
                {
                    var response = JsonConvert.DeserializeObject<PersonalizeResponse>(json);
                    onSuccess?.Invoke(response.pages);
                },
                onError
            );
        }
    }
}
