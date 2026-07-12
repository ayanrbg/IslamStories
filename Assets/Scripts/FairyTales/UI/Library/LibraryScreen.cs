using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FairyTales.Api;
using FairyTales.Audio;
using FairyTales.Cache;
using FairyTales.Diagnostics;
using FairyTales.Models;
using FairyTales.UI.Core;
using FairyTales.IAP;
using FairyTales.UI.Onboarding;
using FairyTales.UI.Payment;

namespace FairyTales.UI.Library
{
    public class LibraryScreen : BaseScreen
    {
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button btnSettings;
        [SerializeField] private Button btnMusic;
        [SerializeField] private Button btnUnlockAll;
        [SerializeField] private Button btnEmail;
        [SerializeField] private GameObject selectionBtnMusicOff;
        [SerializeField] private GameObject selectionBtnMusicOn;
        [SerializeField] private BackgroundMusicManager backgroundMusicManager;
        
        private readonly List<TaleCard> _cardPool = new();
        private TextMeshProUGUI _unlockLabel;

        private ScreenManager _screens;
        private ApiClient _api;
        private AuthService _auth;
        private TalesService _tales;
        private TaleSummary[] _loadedTales;
        private string _loadedLang;
        private bool _registered;
        private void Awake()
        {
            _screens = GetComponentInParent<ScreenManager>();
            _api = FindAnyObjectByType<ApiClient>();
            _auth = new AuthService(_api);
            _tales = new TalesService(_api);

            if (btnSettings) btnSettings.onClick.AddListener(OnSettings);
            if (btnMusic) btnMusic.onClick.AddListener(OnMusicToggle);
            if (btnUnlockAll) btnUnlockAll.onClick.AddListener(OnUnlockAll);
            if (btnEmail) btnEmail.onClick.AddListener(OnEmail);

            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);

            if (IAPManager.Instance != null)
                IAPManager.Instance.OnSubscriptionChanged += OnSubscriptionChanged;

            DeepLink.OnRoute += HandleDeepLink;
        }

        private void OnDestroy()
        {
            if (IAPManager.Instance != null)
                IAPManager.Instance.OnSubscriptionChanged -= OnSubscriptionChanged;

            DeepLink.OnRoute -= HandleDeepLink;
        }

        protected override void OnPrepare()
        {
            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
            UpdateUnlockButton();

            // Invalidate cache if language changed since last load.
            var currentLang = PlayerPrefs.GetString("ft_lang", "ru");
            if (_loadedTales != null && _loadedLang != currentLang)
                _loadedTales = null;

            // If tales are already loaded, sort + rebuild the grid NOW, before the
            // slide-in animation starts, so the user never sees cards reorder.
            if (_loadedTales != null)
                BuildGridImmediate();
            else
                RefreshCardStates();
        }

        protected override void OnShown()
        {
            Analytics.Log("library_view", ("tales_count", _loadedTales?.Length ?? 0));
            StartCoroutine(EnsureRegisteredAndLoad());
        }

        private IEnumerator EnsureRegisteredAndLoad()
        {
            if (!_registered)
            {
                var userId = AuthService.GetOrCreateUserId();
                var childName = PlayerPrefs.GetString("ft_childName", "");
                var gender = PlayerPrefs.GetString("ft_gender", "male");
                var lang = PlayerPrefs.GetString("ft_lang", "ru");

                yield return _auth.Register(userId, childName, gender, lang,
                    onSuccess: _ => _registered = true,
                    onError: e =>
                    {
                        // Already registered is fine
                        // RELEASE: Debug.LogWarning($"[Library] Register: {e}");
                        _registered = true;
                    });

                // Server is the source of truth for premium — reconcile on every cold start,
                // now that the auth token is set. Safe: only downgrades on an expired sub.
                if (IAPManager.Instance != null)
                    IAPManager.Instance.RefreshEntitlement();
            }

            // Cached list was already sorted + rebuilt in OnPrepare (before the
            // slide-in), so nothing more to do here.
            if (_loadedTales != null)
                yield break;

            yield return LoadTales();
        }

        private IEnumerator LoadTales()
        {
            var lang = PlayerPrefs.GetString("ft_lang", "ru");

            TaleSummary[] serverTales = null;
            yield return _tales.GetTales(lang,
                onSuccess: tales => serverTales = tales,
                onError: e => { } /* RELEASE: Debug.LogWarning($"[Library] Server: {e}") */);

            if (serverTales != null)
            {
                // Server is authoritative for the catalog — purge tales it removed and
                // drop hidden/removed ones from the grid. Only runs on a FRESH server
                // response, never on the offline bundled fallback (avoids false purges).
                ReconcileLibrary(serverTales);
                _loadedTales = VisibleTales(serverTales);
            }
            else
            {
                // Offline fallback: use bundled manifest (no reconciliation).
                yield return BundledTaleLoader.LoadManifest(lang, tales => _loadedTales = tales);
            }

            if (_loadedTales == null) yield break;

            _loadedLang = lang;
            BuildGrid();

            HandleDeepLink(); // catalog is ready — resolve any pending push route
        }

        /// <summary>
        /// Route a pending push deep-link (CLIENT_TICKET_PUSH §4). Needs the catalog
        /// loaded to resolve a taleId; otherwise it stays pending and is retried once
        /// <see cref="LoadTales"/> finishes.
        /// </summary>
        private void HandleDeepLink()
        {
            var data = DeepLink.Pending;
            if (data == null || _screens == null) return;
            if (_loadedTales == null) return; // retried at the end of LoadTales

            DeepLink.Pending = null;
            data.TryGetValue("type", out var type);
            switch (type)
            {
                case "tale":
                    data.TryGetValue("taleId", out var id);
                    var tale = FindTale(id);
                    if (tale != null) OnCardClick(tale); // reuses the paywall gate for locked tales
                    break;
                case "paywall":
                    PaymentScreen.Open(_screens, source: "push");
                    break;
                case "url":
                    if (data.TryGetValue("url", out var url) && !string.IsNullOrEmpty(url))
                        Application.OpenURL(url);
                    break;
                // "home"/unknown: library is already the current screen — nothing to do.
            }
        }

        private TaleSummary FindTale(string id)
        {
            if (string.IsNullOrEmpty(id) || _loadedTales == null) return null;
            foreach (var t in _loadedTales)
                if (t != null && t.id == id) return t;
            return null;
        }

        /// <summary>Delete local cache for any tale the server marked "removed".</summary>
        private void ReconcileLibrary(TaleSummary[] serverTales)
        {
            foreach (var t in serverTales)
            {
                if (t != null && t.IsRemoved)
                {
                    AssetCache.DeleteTale(t.id);
                    // RELEASE: Debug.Log($"[Library] Purged removed tale {t.id}");
                }
            }
        }

        /// <summary>Catalog minus hidden/removed tales, preserving order.</summary>
        private static TaleSummary[] VisibleTales(TaleSummary[] serverTales)
        {
            var visible = new List<TaleSummary>(serverTales.Length);
            foreach (var t in serverTales)
                if (t != null && !t.IsHidden) visible.Add(t);
            return visible.ToArray();
        }

        private void BuildGrid()
        {
            StartCoroutine(BuildGridStaggered());
        }

        /// <summary>Synchronous sort + populate. Use before a transition so the grid is
        /// already in final order as the screen becomes visible (no visible reordering).</summary>
        private void BuildGridImmediate()
        {
            if (_loadedTales == null) return;

            bool subscribed = IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;
            SortLoadedTales(subscribed);

            for (int i = 0; i < _loadedTales.Length; i++)
                InitCard(GetOrCreateCard(i), _loadedTales[i], subscribed);

            for (int i = _loadedTales.Length; i < _cardPool.Count; i++)
                _cardPool[i].gameObject.SetActive(false);

            UpdateUnlockButton();
        }

        private IEnumerator BuildGridStaggered()
        {
            if (_loadedTales == null) yield break;

            bool subscribed = IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;
            SortLoadedTales(subscribed);

            for (int i = 0; i < _loadedTales.Length; i++)
            {
                InitCard(GetOrCreateCard(i), _loadedTales[i], subscribed);

                if ((i + 1) % 4 == 0)
                    yield return null;
            }

            for (int i = _loadedTales.Length; i < _cardPool.Count; i++)
                _cardPool[i].gameObject.SetActive(false);

            UpdateUnlockButton();
        }

        // Sort: accessible+downloaded → accessible+not-downloaded → locked
        private void SortLoadedTales(bool subscribed)
        {
            Array.Sort(_loadedTales, (a, b) =>
            {
                int ga = SortGroup(a, subscribed);
                int gb = SortGroup(b, subscribed);
                return ga != gb ? ga.CompareTo(gb) : 0;
            });
        }

        private TaleCard GetOrCreateCard(int i)
        {
            if (i < _cardPool.Count)
            {
                var pooled = _cardPool[i];
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var go = Instantiate(cardPrefab, cardContainer);
            var card = go.GetComponent<TaleCard>();
            _cardPool.Add(card);
            return card;
        }

        private void InitCard(TaleCard card, TaleSummary tale, bool subscribed)
        {
            bool locked = !tale.free && !subscribed;
            bool downloaded = AssetCache.IsTaleDownloaded(tale.id);
            card.Init(tale, locked, downloaded, _api, OnCardClick);
        }

        // Sort order: downloaded → ready-to-download → locked → coming-soon (always last)
        private static int SortGroup(TaleSummary tale, bool subscribed)
        {
            if (tale.comingSoon) return 3;
            bool accessible = tale.free || subscribed;
            if (!accessible) return 2;
            return AssetCache.IsTaleDownloaded(tale.id) ? 0 : 1;
        }

        /// <summary>Call when name/gender changes to force reload on next show.</summary>
        public void MarkNeedsRefresh() => _loadedTales = null;

        private void OnCardClick(TaleSummary tale)
        {
            bool subscribed = IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;
            if (!tale.free && !subscribed)
            {
                // gate first, then paywall (back to library)
                PaymentScreen.Open(_screens, source: "tale_locked");
                return;
            }

            var detail = _screens.Get<TaleDetailScreen>();
            if (detail == null) return;

            detail.SetTale(tale);
            _screens.Show<TaleDetailScreen>();
        }

        private void OnSettings()
        {
            _screens.Show<PersonalizationScreen>();
        }

        private void OnUnlockAll()
        {
            PaymentScreen.Open(_screens, source: "library");
        }

        private void OnEmail()
        {
            ChildGatePopup.Show(() =>
            {
                var email = "support@fairytales.app";
                var subject = Uri.EscapeDataString(Loc.Get("email_subject"));
                Application.OpenURL($"mailto:{email}?subject={subject}");
            });
        }

        private void UpdateUnlockButton()
        {
            if (!btnUnlockAll) return;

            var iap = IAPManager.Instance;
            bool subscribed = iap != null && iap.IsSubscribed;

            // Keep the button visible when subscribed — it now shows the expiry date
            // instead of disappearing. Not clickable while premium is active.
            btnUnlockAll.gameObject.SetActive(true);
            btnUnlockAll.interactable = !subscribed;

            if (_unlockLabel == null)
                _unlockLabel = btnUnlockAll.GetComponentInChildren<TextMeshProUGUI>(true);
            if (!_unlockLabel) return;

            if (!subscribed)
            {
                _unlockLabel.text = Loc.Get("unlock_all");
                return;
            }

            var exp = iap.PremiumExpiresUtc;
            _unlockLabel.text = exp.HasValue
                ? Loc.Format("premium_active_until", exp.Value.ToLocalTime().ToString("dd.MM.yyyy"))
                : Loc.Get("premium_active");
        }

        private void RefreshCardStates()
        {
            foreach (var card in _cardPool)
                if (card.gameObject.activeSelf)
                    card.RefreshDownloadState();
        }

        private void OnSubscriptionChanged(bool subscribed)
        {
            UpdateUnlockButton();
            BuildGrid();
        }

        private void OnMusicToggle()
        {
            if (backgroundMusicManager) backgroundMusicManager.SetMuted(!backgroundMusicManager.IsMuted);
            selectionBtnMusicOff.SetActive(backgroundMusicManager.IsMuted);
            selectionBtnMusicOn.SetActive(!backgroundMusicManager.IsMuted);
        }

    }
}
