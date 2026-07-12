using System;
using System.Collections;
using FairyTales.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FairyTales.Api
{
    public class NarrationService
    {
        private readonly ApiClient _api;

        public NarrationService(ApiClient api) => _api = api;

        // ── Single page narration ────────────────────────
        public IEnumerator NarratePage(string taleId, int page,
            Action<byte[]> onSuccess, Action<string> onError = null,
            string voice = null, string lang = null, string text = null)
        {
            var url = $"/api/tales/{taleId}/narrate?page={page}";
            if (!string.IsNullOrEmpty(voice)) url += $"&voice={voice}";
            if (!string.IsNullOrEmpty(lang)) url += $"&lang={lang}";

            if (!string.IsNullOrEmpty(text))
            {
                var body = JsonConvert.SerializeObject(new { text },
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                yield return _api.PostJsonForAudio(url, body, onSuccess, onError);
            }
            else
            {
                yield return _api.PostForAudio(url, onSuccess, onError);
            }
        }

        // ── Full book narration (async) ──────────────────
        public IEnumerator NarrateAll(string taleId, string childName, string gender,
            Action<NarrateAllResponse> onSuccess, Action<string> onError = null,
            string voice = null, string lang = null, string[] pages = null,
            string narratorGender = null)
        {
            var payload = new NarrateAllPayload
            {
                name = childName,
                gender = gender,
                voice = voice,
                narratorGender = narratorGender,
                lang = lang,
                pages = pages
            };
            var body = JsonConvert.SerializeObject(payload,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // RELEASE: Debug.Log($"[NarrationService] NarrateAll body: {body}");
            yield return _api.PostJson($"/api/tales/{taleId}/narrate-all", body,
                json =>
                {
                    var response = JsonUtility.FromJson<NarrateAllResponse>(json);
                    onSuccess?.Invoke(response);
                },
                onError
            );
        }

        public IEnumerator GetNarrationStatus(string taleId,
            Action<NarrationStatusResponse> onSuccess, Action<string> onError = null)
        {
            yield return _api.Get($"/api/tales/{taleId}/narration-status",
                json =>
                {
                    var response = JsonUtility.FromJson<NarrationStatusResponse>(json);
                    onSuccess?.Invoke(response);
                },
                onError
            );
        }

        public IEnumerator DownloadNarratedPage(string taleId, int page,
            Action<byte[]> onSuccess, Action<string> onError = null)
        {
            yield return _api.GetBytes(
                $"/api/tales/{taleId}/narration/{page}",
                onSuccess, onError
            );
        }
    }
}
