using System;
using System.Collections;
using FairyTales.Models;
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

            string json = null;
            yield return _api.Get($"/api/tales{query}", j => json = j, onError);
            if (json == null) yield break;

            yield return ParseOrFail<TaleSummary[]>(json, onSuccess, onError);
        }

        public IEnumerator GetTale(string taleId,
            Action<TaleDetail> onSuccess, Action<string> onError = null,
            string lang = null)
        {
            var query = string.IsNullOrEmpty(lang) ? "" : $"?lang={lang}";

            string json = null;
            yield return _api.Get($"/api/tales/{taleId}{query}", j => json = j, onError);
            if (json == null) yield break;

            yield return ParseOrFail<TaleDetail>(json, onSuccess, onError);
        }

        public IEnumerator Personalize(string taleId, string name, string gender,
            Action<string[]> onSuccess, Action<string> onError = null, string lang = null)
        {
            var body = JsonUtility.ToJson(new PersonalizeRequest
            {
                name = name, gender = gender
            });

            var query = string.IsNullOrEmpty(lang) ? "" : $"?lang={lang}";
            string json = null;
            yield return _api.PostJson($"/api/tales/{taleId}/personalize{query}", body,
                j => json = j, onError);
            if (json == null) yield break;

            yield return ParseOrFail<PersonalizeResponse>(json,
                response => onSuccess?.Invoke(response.pages), onError);
        }

        /// <summary>Parses off the main thread; a body that doesn't parse is an error.</summary>
        private static IEnumerator ParseOrFail<T>(string json, Action<T> onSuccess,
            Action<string> onError) where T : class
        {
            T result = null;
            yield return JsonAsync.Parse<T>(json, r => result = r);
            if (result != null) onSuccess?.Invoke(result);
            else onError?.Invoke("malformed response");
        }
    }
}
