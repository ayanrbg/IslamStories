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
        [SerializeField] private Button btnClose;

        private ReferralService _referral;
        private string _code = "";

        private void Awake()
        {
            _instance = this;

            if (btnClose) btnClose.onClick.AddListener(Hide);
            if (btnCopy) btnCopy.onClick.AddListener(OnCopy);

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

            // The cached code shows instantly (and is all there is offline); the request
            // still runs so a code issued after the last launch is picked up.
            SetCode(ReferralService.CachedCode);

            // Activate BEFORE the request: StartCoroutine on a disabled object does
            // nothing but log an error, and the code would never arrive.
            gameObject.SetActive(true);

            if (_referral != null)
                StartCoroutine(_referral.GetCode(
                    info => SetCode(info.code),
                    _ => { if (string.IsNullOrEmpty(_code)) SetCode(""); }));

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

        private void SetCode(string code)
        {
            _code = code ?? "";
            var hasCode = !string.IsNullOrEmpty(_code);

            if (txtCode)
                txtCode.text = hasCode ? _code : Loc.Get(_referral == null ? "gift_code_error" : "loading");

            // Nothing to put in the clipboard until the code arrives.
            if (btnCopy) btnCopy.interactable = hasCode;
        }

        private void OnCopy()
        {
            if (string.IsNullOrEmpty(_code)) return;

            GUIUtility.systemCopyBuffer = _code;
            Analytics.Log("gift_code_copied");
            Toast.Show(Loc.Get("gift_code_copied"));
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
