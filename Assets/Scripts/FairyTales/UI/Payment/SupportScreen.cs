using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FairyTales.Diagnostics;
using FairyTales.IAP;
using FairyTales.UI.Core;
using FairyTales.UI.Library;

namespace FairyTales.UI.Payment
{
    /// <summary>
    /// "Help us make more Islamic stories" — a gentle ask that leads to the paywall.
    /// Shown when a tale is closed, at most once an hour (see <see cref="IsDue"/>); the
    /// close cross only appears after a pause, so the message gets read first.
    /// </summary>
    public class SupportScreen : BaseScreen
    {
        [SerializeField] private TMP_Text titleText;
        [Tooltip("New stories, more languages, more children, growing Nooria — in that order.")]
        [SerializeField] private TMP_Text[] chipTexts;
        [SerializeField] private TMP_Text cardText;
        [SerializeField] private TMP_Text supportText;
        [SerializeField] private Button btnSupport;
        [SerializeField] private Button btnClose;
        [SerializeField] private CanvasGroup closeGroup;
        [SerializeField] private float closeDelay = 1.5f;
        [SerializeField] private float closeFadeDuration = 0.35f;

        private static readonly string[] ChipKeys =
            { "support_new_stories", "support_more_langs", "support_more_kids", "support_growth" };

        // ── When to ask ─────────────────────────────────────
        // Every tale close may ask, however much was read — but no more than once an
        // hour, so a child hopping in and out of books doesn't meet it each time.
        private const string LastShownKey = "ft_support_last";
        private const double HoursBetweenAsks = 1;

        private ScreenManager _screens;
        private string _source = "unknown";

        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            if (btnSupport) btnSupport.onClick.AddListener(OnSupport);
            if (btnClose) btnClose.onClick.AddListener(OnClose);
        }

        /// <summary>Whether closing a tale now should ask for support. No side effects.</summary>
        public static bool IsDue()
        {
            if (IAPManager.Instance != null && IAPManager.Instance.IsSubscribed) return false;
            if (!long.TryParse(PlayerPrefs.GetString(LastShownKey, ""), out var ticks) || ticks <= 0)
                return true;
            var since = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            // A clock moved backwards must not silence the ask for good.
            return since.TotalHours >= HoursBetweenAsks || since < TimeSpan.Zero;
        }

        public static void Open(ScreenManager screens, string source)
        {
            if (screens == null) return;
            var screen = screens.Get<SupportScreen>();
            if (screen == null)
            {
                screens.Show<LibraryScreen>();
                return;
            }

            PlayerPrefs.SetString(LastShownKey, DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();

            screen._source = source;
            screens.Show<SupportScreen>();
        }

        protected override void OnPrepare()
        {
            if (titleText) titleText.text = Loc.Get("support_title");
            var chips = ChipLabels();
            for (int i = 0; i < ChipKeys.Length && i < chips.Length; i++)
                if (chips[i]) chips[i].text = Loc.Get(ChipKeys[i]);
            if (cardText) cardText.text = Loc.Get("support_card");
            if (supportText) supportText.text = Loc.Get("support_button");

            CancelInvoke(nameof(RevealClose));
            SetCloseVisible(false);
        }

        /// <summary>
        /// The chip labels in order. Chips duplicated in the scene aren't always wired
        /// into <see cref="chipTexts"/>, so a missing entry is taken from the row the
        /// first chip sits in — the Nth chip of that row.
        /// </summary>
        private TMP_Text[] ChipLabels()
        {
            var labels = new TMP_Text[ChipKeys.Length];
            for (int i = 0; i < labels.Length && chipTexts != null && i < chipTexts.Length; i++)
                labels[i] = chipTexts[i];

            var first = chipTexts != null && chipTexts.Length > 0 ? chipTexts[0] : null;
            var row = first && first.transform.parent ? first.transform.parent.parent : null;
            if (row == null) return labels;

            for (int i = 0; i < labels.Length && i < row.childCount; i++)
                if (!labels[i]) labels[i] = row.GetChild(i).GetComponentInChildren<TMP_Text>(true);
            return labels;
        }

        protected override void OnShown()
        {
            Analytics.Log("support_view", ("source", _source));
            Invoke(nameof(RevealClose), closeDelay);
        }

        protected override void OnHidden()
        {
            CancelInvoke(nameof(RevealClose));
            SetCloseVisible(false);
        }

        private void RevealClose()
        {
            if (!closeGroup) return;
            closeGroup.DOKill();
            closeGroup.DOFade(1f, closeFadeDuration).SetEase(Ease.OutQuad).SetLink(gameObject)
                .OnComplete(() => SetCloseVisible(true));
        }

        private void SetCloseVisible(bool visible)
        {
            if (!closeGroup) return;
            closeGroup.DOKill();
            closeGroup.alpha = visible ? 1f : 0f;
            closeGroup.interactable = visible;
            closeGroup.blocksRaycasts = visible;
        }

        private void OnSupport()
        {
            Analytics.Log("support_tap", ("source", _source));
            // Closing the paywall continues to the library, like closing this screen.
            PaymentScreen.Open(_screens, typeof(LibraryScreen), source: "support");
        }

        private void OnClose()
        {
            Analytics.Log("support_dismiss", ("source", _source));
            _screens.Show<LibraryScreen>();
        }
    }
}
