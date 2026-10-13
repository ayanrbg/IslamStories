using System;
using System.Collections;
using System.Linq;
using FairyTales.UI.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FairyTales.Api
{
    /// <summary>
    /// Which languages to offer: what GET /api/languages serves AND what this build can show
    /// (<see cref="Loc.IsSupported"/>). The server doesn't know what the app is ready for,
    /// and the app doesn't know what the server has dropped.
    ///
    /// The last answer is kept, so an offline start shows the same list. Before any answer
    /// at all (first launch with no network) everything the build supports is offered —
    /// the server has had every one of them since 05.10.2026.
    /// </summary>
    public static class LanguageCatalog
    {
        private const string Endpoint = "/api/languages";
        private const string Pref = "ft_server_langs";

        public static bool IsOffered(string lang)
        {
            if (!Loc.IsSupported(lang)) return false;
            var served = PlayerPrefs.GetString(Pref, "");
            return served.Length == 0 || Array.IndexOf(served.Split(','), lang) >= 0;
        }

        public static IEnumerator Refresh(ApiClient api)
        {
            if (api == null) yield break;
            yield return api.Get(Endpoint, json =>
            {
                // JArray, not a DTO: nothing for IL2CPP stripping to take away.
                string[] codes = null;
                try { codes = JArray.Parse(json).Select(l => (string)l["code"]).Where(c => !string.IsNullOrEmpty(c)).ToArray(); }
                catch (JsonException) { }
                catch (InvalidCastException) { }
                // An empty answer is a server fault, not "no languages": keep the last list.
                if (codes == null || codes.Length == 0) return;
                PlayerPrefs.SetString(Pref, string.Join(",", codes));
                PlayerPrefs.Save();
            }, _ => { });
        }
    }
}
