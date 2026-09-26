using System;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using TMPro;
using UnityEngine;

namespace FairyTales.UI.Onboarding
{
    /// <summary>
    /// First-run intro, right after the profile: the ways a tale can be read or heard.
    /// The profile step decides what comes after it (covers download or the library)
    /// and hands that over through <see cref="SetNext"/>.
    /// </summary>
    public class OnboardingFormatsScreen : OnboardingIntroScreen
    {
        [SerializeField] private TMP_Text txtCardTitle;
        [SerializeField] private TMP_Text txtCardSubtitle;

        private Type _next;

        protected override string StepName => "formats";

        public void SetNext(Type screenType) => _next = screenType;

        protected override void ApplyTexts()
        {
            if (txtTitle) txtTitle.text = Loc.Get("intro_formats_title");
            if (txtCardTitle) txtCardTitle.text = Loc.Get("intro_formats_card_title");
            if (txtCardSubtitle) txtCardSubtitle.text = Loc.Get("intro_formats_card_sub");
        }

        protected override void GoNext() => Screens.Show(_next ?? typeof(LibraryScreen));
    }
}
