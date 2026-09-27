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

        [Header("\"A story for <name>\" header")]
        [SerializeField] private GameObject forChildHeader;
        [SerializeField] private TMP_Text txtForChildTitle;
        [SerializeField] private TMP_Text txtForChildSubtitle;

        private readonly List<TaleCard> _cardPool = new();
        private readonly Dictionary<string, int> _catalogOrder = new();
        private TextMeshProUGUI _unlockLabel;

        private ScreenManager _screens;
        private ApiClient _api;
        private AuthService _auth;
        private TalesService _tales;
        private TaleSummary[] _loadedTales;
        private string _loadedLang;
        private bool _registered;
        private int _loadGeneration;
        private bool _needsReload;
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
            UpdateForChildHeader();

            // Invalidate cache if language changed since last load.
            var currentLang = PlayerPrefs.GetString("ft_lang", "ru");
            if (_loadedTales != null && _loadedLang != currentLang)
                _loadedTales = null;

            // If tales are already loaded, sort + rebuild the grid NOW, before the
            // slide-in animation starts, so the user never sees cards reorder.
            // A pending reload still draws the list we have — it is better than an empty
            // grid while the request is in flight, and LoadTales redraws when it lands.
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
            if (_loadedTales != null && !_needsReload && !HasPendingGift())
                yield break;

            yield return LoadTales();
        }

        /// <summary>
        /// Coming back from the background is the other moment a gift can have landed —
        /// the friend redeems the code while the app is not running, and the credit is
        /// never pushed to us. Deliberately not tied to the notification: push delivery is
        /// guaranteed by nobody, and the book already exists by the time it would arrive.
        /// </summary>
        private void OnApplicationFocus(bool focused)
        {
            if (!focused || !IsVisible) return;

            _needsReload = true;
            if (isActiveAndEnabled) StartCoroutine(LoadTales());
        }

        /// <summary>
        /// A gift tale still waiting on a friend makes the catalog worth re-reading every
        /// time the library opens: the unlock happens server-side when the friend types the
        /// code, and nothing pushes it to us.
        /// </summary>
        private bool HasPendingGift()
        {
            if (_loadedTales == null) return false;
            foreach (var t in _loadedTales)
                if (t != null && t.gift && !t.unlocked) return true;
            return false;
        }

        private IEnumerator LoadTales()
        {
            var lang = PlayerPrefs.GetString("ft_lang", "ru");

            // A load already in flight when a promo code lands would answer with the
            // pre-code catalog and put the locks straight back on. Whoever asked last wins.
            var generation = ++_loadGeneration;
            var previous = _loadedTales;

            TaleSummary[] serverTales = null;
            yield return _tales.GetTales(lang,
                onSuccess: tales => serverTales = tales,
                onError: e => { } /* RELEASE: Debug.LogWarning($"[Library] Server: {e}") */);

            if (generation != _loadGeneration) yield break;

            if (serverTales != null)
            {
                // Only a real answer clears the flag: a failed request must be retried the
                // next time the library is shown, not silently accepted as up to date.
                _needsReload = false;
                // Server is authoritative for the catalog — purge tales it removed and
                // drop hidden/removed ones from the grid. Only runs on a FRESH server
                // response, never on the offline bundled fallback (avoids false purges).
                ReconcileLibrary(serverTales);
                _loadedTales = VisibleTales(serverTales);
                SaveUnlocked(_loadedTales);
                AnnounceEarnedGifts(_loadedTales);
            }
            else if (previous != null)
            {
                // Server unreachable but we already had its catalog — keep it. Falling back
                // to the bundled manifest here would shrink a full library down to the few
                // tales shipped in the app, which is what a refresh after a promo code would
                // otherwise do the moment the network drops.
                _loadedTales = previous;
            }
            else
            {
                // Cold start with no network: bundled manifest is all there is.
                yield return BundledTaleLoader.LoadManifest(lang, tales => _loadedTales = tales);
                ApplyCachedUnlocked(_loadedTales);
            }

            if (_loadedTales == null) yield break;

            // A fresh catalog (server or bundled) sets the order; the kept one already has it.
            if (!ReferenceEquals(_loadedTales, previous))
                RememberCatalogOrder(_loadedTales);

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

        // The bundled manifest knows nothing about promo unlocks, so an offline start
        // would re-lock tales the user already opened with a code. Remembered from the
        // last server catalog and BOUND TO THE USER ID that received it — `unlocked` is
        // personal and must never leak to another profile.
        private const string UnlockedKey = "ft_unlocked_tales";
        private const string UnlockedOwnerKey = "ft_unlocked_owner";

        private static void SaveUnlocked(TaleSummary[] tales)
        {
            var ids = new List<string>();
            foreach (var t in tales)
                if (t != null && t.unlocked) ids.Add(t.id);

            PlayerPrefs.SetString(UnlockedKey, string.Join(",", ids));
            PlayerPrefs.SetString(UnlockedOwnerKey, AuthService.GetOrCreateUserId());
            PlayerPrefs.Save();
        }

        private static void ApplyCachedUnlocked(TaleSummary[] tales)
        {
            if (tales == null) return;
            if (PlayerPrefs.GetString(UnlockedOwnerKey, "") != AuthService.GetOrCreateUserId()) return;

            var ids = PlayerPrefs.GetString(UnlockedKey, "");
            if (string.IsNullOrEmpty(ids)) return;

            var set = new HashSet<string>(ids.Split(','));
            foreach (var t in tales)
                if (t != null && set.Contains(t.id)) t.unlocked = true;
        }

        // Gift tales already announced as earned. Without this the friend's redemption
        // arrives as a silently unlocked tale — nothing tells the user their code worked.
        private const string GiftSeenKey = "ft_gift_seen";

        private static void AnnounceEarnedGifts(TaleSummary[] tales)
        {
            var known = PlayerPrefs.HasKey(GiftSeenKey);
            var seen = new HashSet<string>(PlayerPrefs.GetString(GiftSeenKey, "").Split(','));

            var earned = new List<string>();
            var isNew = false;
            foreach (var t in tales)
            {
                if (t == null || !t.gift || !t.unlocked) continue;
                earned.Add(t.id);
                if (!seen.Contains(t.id)) isNew = true;
            }

            PlayerPrefs.SetString(GiftSeenKey, string.Join(",", earned));
            PlayerPrefs.Save();

            // First catalog after the update (or after a profile switch) is the baseline,
            // not a celebration: everything in it would otherwise read as just-earned.
            if (!known || !isNew) return;

            Toast.Show(Loc.Get("gift_earned"));
            Analytics.Log("gift_earned", ("gifts_total", earned.Count));
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

        /// <summary>
        /// Order within a group is the catalog's own, remembered here because
        /// <see cref="_loadedTales"/> is sorted in place. Without it ties came back in
        /// whatever order the previous sort left them, and Array.Sort is not stable.
        /// </summary>
        private void RememberCatalogOrder(TaleSummary[] tales)
        {
            _catalogOrder.Clear();
            for (int i = 0; i < tales.Length; i++)
                if (tales[i] != null) _catalogOrder[tales[i].id] = i;
        }

        private int CatalogIndex(TaleSummary tale) =>
            _catalogOrder.TryGetValue(tale.id, out var i) ? i : int.MaxValue;

        // Access group first, then catalog order — see SortGroup.
        private void SortLoadedTales(bool subscribed)
        {
            Array.Sort(_loadedTales, (a, b) =>
            {
                int ga = SortGroup(a, subscribed);
                int gb = SortGroup(b, subscribed);
                return ga != gb ? ga.CompareTo(gb) : CatalogIndex(a).CompareTo(CatalogIndex(b));
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
            bool locked = tale.IsLocked(subscribed);
            bool downloaded = AssetCache.IsTaleDownloaded(tale.id);
            card.Init(tale, locked, downloaded, _api, OnCardClick);
        }

        // Sort order: open → gift → locked → coming-soon (last).
        // Deliberately by access only, never by download state: downloading a tale used
        // to lift it to the top on the next rebuild, and the grid visibly reshuffled
        // under the reader. Access changes only on a purchase or a promo code.
        // Gift tales sit above the plain locked ones: they are the one locked group the
        // user can actually do something about, so the badge has to be reachable.
        private static int SortGroup(TaleSummary tale, bool subscribed)
        {
            if (tale.comingSoon) return 3;
            if (tale.IsLocked(subscribed)) return tale.gift ? 1 : 2;
            return 0;
        }

        /// <summary>Call when name/gender changes to force reload on next show.</summary>
        /// <summary>
        /// Takes a catalog fetched elsewhere (the onboarding preload) while this screen is
        /// still hidden and builds the grid right away, so the cards are already in place
        /// when the library slides in rather than appearing after it has arrived.
        /// </summary>
        public void SetCatalog(TaleSummary[] tales, bool fromServer)
        {
            if (tales == null) return;
            _loadGeneration++; // an in-flight load must not overwrite this

            if (fromServer)
            {
                _needsReload = false;
                ReconcileLibrary(tales);
                _loadedTales = VisibleTales(tales);
                // Sets the baseline gift announcements are measured against — nothing
                // has been earned on a first launch.
                SaveUnlocked(_loadedTales);
            }
            else
            {
                // The bundled list only stands in: ask the server again once shown.
                _needsReload = true;
                _loadedTales = tales;
                ApplyCachedUnlocked(_loadedTales);
            }

            RememberCatalogOrder(_loadedTales);
            _loadedLang = PlayerPrefs.GetString("ft_lang", "ru");
            BuildGridImmediate();
        }

        public void MarkNeedsRefresh()
        {
            _needsReload = true;
            _loadGeneration++; // discard whatever an in-flight load is about to return
        }

        /// <summary>
        /// A promo code just opened these tales. The server confirmed it, so mark them
        /// straight away instead of waiting for the refreshed catalog — that way the locks
        /// come off even if the network drops before the reload lands.
        /// </summary>
        public void ApplyPromoUnlocks(string[] taleIds)
        {
            if (_loadedTales != null && taleIds != null)
            {
                foreach (var tale in _loadedTales)
                    if (System.Array.IndexOf(taleIds, tale.id) >= 0) tale.unlocked = true;

                SaveUnlocked(_loadedTales);
            }

            MarkNeedsRefresh();
        }

        private void OnCardClick(TaleSummary tale)
        {
            bool subscribed = IAPManager.Instance != null && IAPManager.Instance.IsSubscribed;
            if (tale.IsLocked(subscribed))
            {
                // A gift tale has its own way in — the paywall would be the wrong answer
                // to "how do I get this one?".
                if (tale.gift)
                {
                    GiftPopup.Show(_api, tale);
                    return;
                }

                // Paywall, then back to the library. The parental gate (Kids category,
                // Guideline 1.3) waits on the paywall's purchase and restore buttons.
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
                var email = AppConfig.SupportEmail;
                var subject = Uri.EscapeDataString(Loc.Get("email_subject"));
                Application.OpenURL($"mailto:{email}?subject={subject}");
            });
        }

        /// <summary>
        /// "История для Амира / Подобрано специально для него" — the name is declined for
        /// the phrase and the subtitle follows the child's gender. Hidden without a name.
        /// </summary>
        private void UpdateForChildHeader()
        {
            var childName = PlayerPrefs.GetString("ft_childName", "").Trim();
            var gender = PlayerPrefs.GetString("ft_gender", "male");
            var hasName = !string.IsNullOrEmpty(childName);

            if (forChildHeader) forChildHeader.SetActive(hasName);
            if (!hasName) return;

            if (txtForChildTitle)
                txtForChildTitle.text = Loc.Format("library_for_child",
                    ChildNameForms.ForDedication(childName, gender, Loc.Lang));
            if (txtForChildSubtitle)
                txtForChildSubtitle.text = Loc.Get(gender == "female"
                    ? "library_for_child_sub_female"
                    : "library_for_child_sub_male");
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
