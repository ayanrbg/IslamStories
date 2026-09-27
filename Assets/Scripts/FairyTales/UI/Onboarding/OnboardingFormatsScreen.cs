using System.Collections;
using FairyTales.Cache;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using TMPro;
using UnityEngine;

namespace FairyTales.UI.Onboarding
{
    /// <summary>
    /// First-run intro, right after the profile: the ways a tale can be read or heard.
    /// The tales load in the background while it is on screen (see
    /// <see cref="TalesPreloader"/>); Continue goes straight to the library when that
    /// load is complete, and through the download screen when it isn't.
    /// </summary>
    public class OnboardingFormatsScreen : OnboardingIntroScreen
    {
        [SerializeField] private TMP_Text txtCardTitle;
        [SerializeField] private TMP_Text txtCardSubtitle;

        private TalesPreloader _preload;
        private bool _handedOver;

        protected override string StepName => "formats";

        public void SetPreload(TalesPreloader preload)
        {
            _preload = preload;
            _handedOver = false;
        }

        protected override void OnShown()
        {
            base.OnShown();
            StartCoroutine(HandOverWhenLoaded());
        }

        // The library builds its grid the moment the load is in, while this screen is
        // still up — not in the frame of the tap that opens it.
        private IEnumerator HandOverWhenLoaded()
        {
            if (_preload == null) yield break;
            while (_preload.IsRunning) yield return null;
            HandOver();
        }

        private void HandOver()
        {
            if (_handedOver || _preload?.Tales == null) return;
            _handedOver = true;
            Screens.Get<LibraryScreen>()?.SetCatalog(_preload.Tales, _preload.FromServer);
        }

        protected override void ApplyTexts()
        {
            if (txtTitle) txtTitle.text = Loc.Get("intro_formats_title");
            if (txtCardTitle) txtCardTitle.text = Loc.Get("intro_formats_card_title");
            if (txtCardSubtitle) txtCardSubtitle.text = Loc.Get("intro_formats_card_sub");
        }

        protected override void GoNext()
        {
            var download = Screens.Get<DownloadScreen>();
            if (_preload != null && !_preload.AllReady && download != null)
            {
                download.SetPreload(_preload);
                Screens.Show<DownloadScreen>();
                return;
            }

            // Everything is already on disk: no loading screen, and the grid is built.
            if (_preload != null) HandOver();
            else Screens.Get<LibraryScreen>()?.MarkNeedsRefresh();
            Screens.Show<LibraryScreen>();
        }
    }
}
