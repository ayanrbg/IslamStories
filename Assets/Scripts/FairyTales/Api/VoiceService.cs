using System;
using System.Collections;
using FairyTales.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FairyTales.Api
{
    public class VoiceService
    {
        private readonly ApiClient _api;

        public VoiceService(ApiClient api) => _api = api;

        // ── Voice Clone ──────────────────────────────────
        public IEnumerator CloneVoice(byte[] audioData, string fileName,
            Action<CloneResponse> onSuccess, Action<string> onError = null)
        {
            var mime = fileName.EndsWith(".wav") ? "audio/wav" : "audio/mpeg";

            yield return _api.PostMultipart("/api/voice/clone",
                audioData, fileName, "voiceSample", mime,
                json =>
                {
                    var response = JsonUtility.FromJson<CloneResponse>(json);
                    onSuccess?.Invoke(response);
                },
                onError
            );
        }

        public IEnumerator DeleteVoice(
            Action onSuccess = null, Action<string> onError = null)
        {
            yield return _api.Delete("/api/voice",
                _ => onSuccess?.Invoke(),
                onError
            );
        }

        // ── Drafts ───────────────────────────────────────
        public IEnumerator GetDrafts(
            Action<Draft[]> onSuccess, Action<string> onError = null)
        {
            yield return _api.Get("/api/voice/drafts",
                json =>
                {
                    var drafts = JsonConvert.DeserializeObject<Draft[]>(json);
                    onSuccess?.Invoke(drafts);
                },
                onError
            );
        }

        public IEnumerator CreateDraft(string narratorName, string taleId,
            Action<Draft> onSuccess, Action<string> onError = null)
        {
            var body = JsonUtility.ToJson(new DraftCreateRequest
            {
                narratorName = narratorName, taleId = taleId
            });

            yield return _api.PostJson("/api/voice/drafts", body,
                json =>
                {
                    var response = JsonUtility.FromJson<DraftCreateResponse>(json);
                    onSuccess?.Invoke(response.draft);
                },
                onError
            );
        }

        public IEnumerator UpdateDraft(int draftId, string voiceId,
            Action onSuccess = null, Action<string> onError = null)
        {
            var body = JsonUtility.ToJson(new DraftUpdateRequest { voiceId = voiceId });
            yield return _api.PutJson($"/api/voice/drafts/{draftId}", body,
                _ => onSuccess?.Invoke(),
                onError
            );
        }

        public IEnumerator GetDraft(int draftId,
            Action<Draft> onSuccess, Action<string> onError = null)
        {
            yield return _api.Get($"/api/voice/drafts/{draftId}",
                json =>
                {
                    var draft = JsonConvert.DeserializeObject<Draft>(json);
                    onSuccess?.Invoke(draft);
                },
                onError
            );
        }

        public IEnumerator DeleteDraft(int draftId,
            Action onSuccess = null, Action<string> onError = null)
        {
            yield return _api.Delete($"/api/voice/drafts/{draftId}",
                _ => onSuccess?.Invoke(),
                onError
            );
        }
    }
}
