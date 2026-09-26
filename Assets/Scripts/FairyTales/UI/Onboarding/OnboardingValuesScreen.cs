using FairyTales.UI.Core;
using TMPro;
using UnityEngine;

namespace FairyTales.UI.Onboarding
{
    /// <summary>First-run intro, right after the language: what the app is for.</summary>
    public class OnboardingValuesScreen : OnboardingIntroScreen
    {
        [SerializeField] private TMP_Text txtSubtitle;
        [SerializeField] private TMP_Text txtQuote;
        [SerializeField] private TMP_Text txtQuoteSource;

        protected override string StepName => "values";

        protected override void ApplyTexts()
        {
            if (txtTitle) txtTitle.text = Loc.Get("intro_values_title");
            if (txtSubtitle) txtSubtitle.text = Loc.Get("intro_values_sub");
            if (txtQuote) txtQuote.text = Loc.Get("intro_values_quote");
            if (txtQuoteSource) txtQuoteSource.text = Loc.Get("intro_values_quote_source");
        }

        protected override void GoNext() => Screens.Show<PersonalizationScreen>();
    }
}
