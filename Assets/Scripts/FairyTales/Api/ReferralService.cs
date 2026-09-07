using System;
using System.Collections;
using UnityEngine;

namespace FairyTales.Api
{
    /// <summary>
    /// The user's own referral code — a friend types it into the promo field and the
    /// gift tale unlocks HERE, on the inviter's account. The code is issued and owned
    /// by the server; the client only shows it.
    /// </summary>
    public class ReferralService
    {
        private readonly ApiClient _api;

        public ReferralService(ApiClient api) => _api = api;

        // Cached so the popup can open instantly (and offline) after the first fetch.
        // Bound to the userId that received it: the code is personal, exactly like the
        // `unlocked` flag, and must never be shown to another profile.
        private const string CodeKey = "ft_referral_code";
        private const string OwnerKey = "ft_referral_owner";

        public static string CachedCode =>
            PlayerPrefs.GetString(OwnerKey, "") == AuthService.GetOrCreateUserId()
                ? PlayerPrefs.GetString(CodeKey, "")
                : "";

        public IEnumerator GetCode(Action<ReferralInfo> onSuccess, Action<string> onError = null)
        {
            yield return _api.Get("/api/referral",
                json =>
                {
                    ReferralInfo info = null;
                    // An empty or malformed body would otherwise throw inside the callback
                    // and kill the coroutine, leaving the popup stuck on "Загрузка...".
                    try { info = JsonUtility.FromJson<ReferralInfo>(json); }
                    catch (Exception e) { Debug.LogWarning($"[Referral] bad response: {e.Message} json={json}"); }

                    if (info == null || string.IsNullOrEmpty(info.code))
                    {
                        onError?.Invoke("no code");
                        return;
                    }

                    PlayerPrefs.SetString(CodeKey, info.code);
                    PlayerPrefs.SetString(OwnerKey, AuthService.GetOrCreateUserId());
                    PlayerPrefs.Save();
                    onSuccess?.Invoke(info);
                },
                onError);
        }

        /// <summary>
        /// [Preserve] on purpose — read by reflection, and IL2CPP managed stripping is on
        /// Medium for both stores (see <see cref="Models.NarratePayload"/>).
        /// </summary>
        [Serializable]
        [UnityEngine.Scripting.Preserve]
        public class ReferralInfo
        {
            public string code;      // 5 characters, stable for the lifetime of the account
            public int invited;      // friends who already redeemed it
            public int giftsEarned;  // tales the server has opened for those invites
        }
    }
}
