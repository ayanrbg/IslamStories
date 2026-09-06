using System;
using System.Collections;
using FairyTales.Diagnostics;
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
            string voice = null, string lang = null, string text = null,
            string narratorGender = null)
        {
            var url = $"/api/tales/{taleId}/narrate?page={page}";
            if (!string.IsNullOrEmpty(voice)) url += $"&voice={voice}";
            if (!string.IsNullOrEmpty(lang)) url += $"&lang={lang}";

            // The body ALWAYS carries who speaks and for whom. Callers used to pass
            // narratorGender only from the voice panel, and a page without local text
            // was posted with no body at all — so the server never saw the choice and
            // fell back to its default (male) on every request.
            var body = JsonConvert.SerializeObject(new NarratePayload
            {
                text = text,
                narratorGender = Narrator(narratorGender),
                name = Name(),
                gender = UserProfile.ChildGender
            }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // The single point where the narrator used to go missing, so log exactly
            // what leaves the device — not what we intended to send.
            if (NarrationConfig.Verbose)
                RemoteLog.Event("narrate_request",
                    $"tale={taleId}; page={page}; voice={voice ?? "-"}; lang={lang ?? "-"}; " +
                    $"narrator={Narrator(narratorGender)}; arg={narratorGender ?? "null"}; " +
                    $"name={Name()}; gender={UserProfile.ChildGender}; " +
                    $"textLen={text?.Length ?? -1}; bodyLen={body.Length}");

            // TTS providers fail transiently (502 from Edge/Fish, dropped mobile
            // connection). One page that lost the coin toss stayed silent until the
            // cache was cleared, so retry once before giving up — but never on a 4xx,
            // where the same request would be rejected the same way.
            byte[] audio = null;
            string error = null;
            int used = 0;
            var started = Time.realtimeSinceStartup;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                error = null;
                used = attempt + 1;
                yield return _api.PostJsonForAudio(url, body,
                    data => audio = data, e => error = e);

                if (audio != null || IsClientError(error)) break;
                if (attempt == 0) yield return new WaitForSecondsRealtime(1.5f);
            }

            if (NarrationConfig.Verbose)
                RemoteLog.Event("narrate_response",
                    $"tale={taleId}; page={page}; ok={audio != null}; " +
                    $"bytes={audio?.Length ?? 0}; attempts={used}; " +
                    $"ms={(int)((Time.realtimeSinceStartup - started) * 1000)}; " +
                    $"err={error ?? "-"}");

            if (audio != null) onSuccess?.Invoke(audio);
            else onError?.Invoke(error);
        }

        // ApiClient formats errors as "[<code>] <error> — <body>".
        private static bool IsClientError(string error)
            => error != null && error.StartsWith("[4");

        // Fall back to the persisted UI choice instead of letting the server guess.
        private static string Narrator(string narratorGender)
            => string.IsNullOrEmpty(narratorGender)
                ? UserProfile.NarratorGender : narratorGender;

        // Onboarding cannot be passed without a name, so this is always set — the
        // server must never fall back to the profile it registered at startup.
        private static string Name(string name = null)
            => string.IsNullOrEmpty(name) ? UserProfile.ChildName : name;

        // ── Full book narration (async) ──────────────────
        public IEnumerator NarrateAll(string taleId, string childName, string gender,
            Action<NarrateAllResponse> onSuccess, Action<string> onError = null,
            string voice = null, string lang = null, string[] pages = null,
            string narratorGender = null)
        {
            var payload = new NarrateAllPayload
            {
                name = Name(childName),
                gender = string.IsNullOrEmpty(gender) ? UserProfile.ChildGender : gender,
                voice = voice,
                narratorGender = Narrator(narratorGender),
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
