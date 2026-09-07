using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FairyTales.Api;
using FairyTales.Diagnostics;
using FairyTales.Models;
using FairyTales.UI.Core;

namespace FairyTales.UI.Library
{
    /// <summary>
    /// "Book as a gift": tapping a gift tale explains how to earn it — invite a friend,
    /// the friend types this user's personal 5-character code into the promo field, and
    /// the server opens the tale for the inviter.
    /// </summary>
    public class GiftPopup : MonoBehaviour
    {
        private static GiftPopup _instance;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text txtTitle;
        [SerializeField] private TMP_Text txtInstruction;
        [SerializeField] private TMP_Text txtCodeCaption;
        [SerializeField] private TMP_Text txtCode;
        [SerializeField] private Button btnCopy;
        [SerializeField] private TMP_Text txtCopyLabel;
        [SerializeField] private Button btnShare;
        [SerializeField] private TMP_Text txtShareLabel;
        [SerializeField] private Button btnRetry;
        [SerializeField] private TMP_Text txtRetryLabel;
        [SerializeField] private TMP_Text txtCounters;
        [SerializeField] private TMP_Text txtCountersNote;
        [SerializeField] private Button btnClose;

        private ReferralService _referral;
        private string _code = "";
        private bool _statsLoaded;

        private void Awake()
        {
            _instance = this;

            if (btnClose) btnClose.onClick.AddListener(Hide);
            if (btnCopy) btnCopy.onClick.AddListener(OnCopy);
            if (btnShare) btnShare.onClick.AddListener(OnShare);
            if (btnRetry) btnRetry.onClick.AddListener(Fetch);

            if (canvasGroup)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        /// <summary>Opens the popup. <paramref name="tale"/> is used for analytics only.</summary>
        public static void Show(ApiClient api, TaleSummary tale = null)
        {
            if (_instance == null) return;
            _instance.Present(api, tale);
        }

        private void Present(ApiClient api, TaleSummary tale)
        {
            if (api != null && _referral == null) _referral = new ReferralService(api);

            Analytics.Log("gift_popup_view", ("tale_id", tale != null ? tale.id : ""));

            if (txtTitle) txtTitle.text = Loc.Get("gift_title");
            if (txtInstruction) txtInstruction.text = GiftInstruction();
            if (txtCodeCaption) txtCodeCaption.text = Loc.Get("gift_your_code");
            if (txtCopyLabel) txtCopyLabel.text = Loc.Get("copy");
            if (txtShareLabel) txtShareLabel.text = Loc.Get("share");
            if (txtRetryLabel) txtRetryLabel.text = Loc.Get("retry");

            // The cached code shows instantly (and is all there is offline); the request
            // still runs so a code issued after the last launch is picked up.
            _statsLoaded = false;
            SetCode(ReferralService.CachedCode, requestPending: true);

            // Activate BEFORE the request: StartCoroutine on a disabled object does
            // nothing but log an error, and the code would never arrive.
            gameObject.SetActive(true);
            Fetch();

            if (!canvasGroup) return;
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        /// <summary>
        /// The instruction names two buttons the friend has to find. They are filled in from
        /// their own Loc keys, so the quoted names can never drift from the real UI.
        /// </summary>
        public static string GiftInstruction()
            => Loc.Format("gift_instruction", Loc.Get("unlock_all"), Loc.Get("promo_code"));

        /// <summary>
        /// Asks the server for the code and the counters. Safe to call again — this is
        /// also what the Retry button runs after a failed first attempt.
        /// </summary>
        private void Fetch()
        {
            if (_referral == null)
            {
                SetCode(_code, requestPending: false);
                return;
            }

            SetCode(_code, requestPending: true);
            StartCoroutine(_referral.GetCode(
                info =>
                {
                    SetCode(info.code, requestPending: false);
                    SetCounters(info.invited, info.giftsEarned);
                },
                _ => SetCode(_code, requestPending: false)));
        }

        private void SetCode(string code, bool requestPending)
        {
            _code = code ?? "";
            var hasCode = !string.IsNullOrEmpty(_code);

            if (txtCode)
                txtCode.text = hasCode
                    ? _code
                    : Loc.Get(requestPending ? "loading" : "gift_code_error");

            // Nothing to put in the clipboard, and nothing to share, until it arrives.
            if (btnCopy) btnCopy.gameObject.SetActive(hasCode);
            if (btnShare) btnShare.gameObject.SetActive(hasCode);

            // Retry belongs to the one state the user can act on: no code and no request
            // still running. While a cached code is on screen there is nothing to retry.
            if (btnRetry) btnRetry.gameObject.SetActive(!hasCode && !requestPending);

            // A cached code with counters that never arrived must say so, or the numbers
            // from a previous session read as current.
            if (hasCode && !_statsLoaded && !requestPending)
            {
                if (txtCounters) txtCounters.gameObject.SetActive(false);
                if (txtCountersNote)
                {
                    txtCountersNote.text = Loc.Get("gift_stats_stale");
                    txtCountersNote.gameObject.SetActive(true);
                }
            }
            else if (!_statsLoaded)
            {
                if (txtCounters) txtCounters.gameObject.SetActive(false);
                if (txtCountersNote) txtCountersNote.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Both numbers, always printed separately. They legitimately disagree — one gift
        /// tale exists and the server caps daily credits — so collapsing them into one
        /// figure would tell the user they have books the library does not show.
        /// </summary>
        private void SetCounters(int invited, int giftsEarned)
        {
            _statsLoaded = true;

            if (txtCounters)
            {
                txtCounters.text = Loc.Format("gift_counters", invited, giftsEarned);
                txtCounters.gameObject.SetActive(true);
            }

            if (txtCountersNote)
            {
                // The app is never told WHY the two differ, so the note stays neutral.
                var explain = invited > giftsEarned;
                txtCountersNote.text = explain ? Loc.Get("gift_counters_note") : "";
                txtCountersNote.gameObject.SetActive(explain);
            }
        }

        private void OnCopy()
        {
            if (string.IsNullOrEmpty(_code)) return;

            GUIUtility.systemCopyBuffer = _code;
            Analytics.Log("gift_code_copied");
            Toast.Show(Loc.Get("gift_code_copied"));
        }

        /// <summary>
        /// Most people forward the code in a messenger rather than dictate it, so the
        /// share sheet carries a ready sentence that explains what the code does.
        /// </summary>
        private void OnShare()
        {
            if (string.IsNullOrEmpty(_code)) return;

            var text = Loc.Format("gift_share_text", _code, Application.productName);
            Analytics.Log("gift_code_copied");

            // Falls back to the clipboard where no sheet exists — say what actually
            // happened rather than claiming a sheet opened.
            if (!NativeShare.Share(text)) Toast.Show(Loc.Get("gift_code_copied"));
        }

        private void Hide()
        {
            if (!canvasGroup)
            {
                gameObject.SetActive(false);
                return;
            }

            canvasGroup.interactable = false;
            canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
            });
        }
    }
}
