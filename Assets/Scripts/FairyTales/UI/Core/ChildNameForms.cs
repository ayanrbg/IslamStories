namespace FairyTales.UI.Core
{
    /// <summary>
    /// Puts the child's name into the grammatical case a sentence around it needs:
    /// "История для Амира" (Russian genitive), "Әмірге арналған" (Kazakh dative).
    /// English and Uzbek don't inflect the name here, so it comes back unchanged.
    ///
    /// Rule-based, not dictionary-based: it covers the regular endings, and falls back to
    /// the bare name whenever a rule can't be applied with confidence — an undeclined name
    /// reads as a little stiff, a wrongly declined one reads as a typo.
    /// </summary>
    public static class ChildNameForms
    {
        /// <summary>The name as it reads after "для" / before "арналған" in <paramref name="lang"/>.</summary>
        public static string ForDedication(string name, string gender, string lang)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) return name;

            return lang switch
            {
                "ru" => RussianGenitive(name, gender == "female"),
                "kz" => KazakhDative(name),
                _ => name
            };
        }

        // ── Russian ──────────────────────────────────────────

        private static string RussianGenitive(string name, bool female)
        {
            // A name typed in Latin letters or in several words is left alone.
            if (!IsCyrillicWord(name)) return name;

            var last = char.ToLowerInvariant(name[name.Length - 1]);
            var stem = name.Substring(0, name.Length - 1);
            var beforeLast = name.Length > 1 ? char.ToLowerInvariant(name[name.Length - 2]) : '\0';

            switch (last)
            {
                // Айша → Айши, Мадина → Мадины, Муса → Мусы (male names in -а decline the same way)
                case 'а':
                    return stem + ("гкхжшщч".IndexOf(beforeLast) >= 0 ? Match(name, 'и') : Match(name, 'ы'));
                // Мария → Марии, Алия → Алии, Яхья → Яхьи
                case 'я':
                    return stem + Match(name, 'и');
                // Андрей → Андрея
                case 'й':
                    return female ? name : stem + Match(name, 'я');
                // Игорь → Игоря, Асель → Асели
                case 'ь':
                    return stem + Match(name, female ? 'и' : 'я');
            }

            // Names ending in any other vowel (Али, Нелли, Марго) don't decline.
            if ("аеёиоуыэюя".IndexOf(last) >= 0) return name;

            // Consonant ending: Амир → Амира for boys; girls' names (Айгерим) don't decline.
            return female ? name : name + Match(name, 'а');
        }

        // ── Kazakh ───────────────────────────────────────────

        private const string KzBackVowels = "аоұыяё";
        // и counts as front: in names it reads as [ій] — Амир → Амирге, Әли → Әлиге.
        private const string KzFrontVowels = "әеөүіэи";

        private static string KazakhDative(string name)
        {
            // Latin-script names get no Cyrillic suffix glued on.
            if (!IsCyrillicWord(name)) return name;

            var lower = name.ToLowerInvariant();
            var last = lower[lower.Length - 1];

            // Voiceless (and word-final voiced stops, which are pronounced voiceless) take қ/к.
            bool hardStart = "кқпстшщфхцчһбвгд".IndexOf(last) >= 0;
            bool front = IsFrontHarmony(lower);

            var suffix = hardStart
                ? (front ? "ке" : "қа")
                : (front ? "ге" : "ға");
            return name + (IsUpper(name) ? suffix.ToUpperInvariant() : suffix);
        }

        /// <summary>
        /// Vowel harmony follows the name's last back or front vowel; a name with no
        /// telling vowel leans front.
        /// </summary>
        private static bool IsFrontHarmony(string lower)
        {
            for (int i = lower.Length - 1; i >= 0; i--)
            {
                var c = lower[i];
                if (KzFrontVowels.IndexOf(c) >= 0) return true;
                if (KzBackVowels.IndexOf(c) >= 0) return false;
                // у after a consonant is the vowel [ұу] (Тимур → Тимурға); after a vowel
                // it is the glide [w] and says nothing about harmony.
                if (c == 'у' && i > 0 && !IsKzVowel(lower[i - 1])) return false;
            }
            return true;
        }

        private static bool IsKzVowel(char c) =>
            KzBackVowels.IndexOf(c) >= 0 || KzFrontVowels.IndexOf(c) >= 0 || c == 'у';

        // ── Helpers ──────────────────────────────────────────

        private static bool IsCyrillicWord(string s)
        {
            foreach (var c in s)
                if (!(c >= 'Ѐ' && c <= 'ӿ') && c != '-') return false;
            return true;
        }

        private static bool IsUpper(string s) => s.Length > 1 && s == s.ToUpperInvariant();

        /// <summary>Keeps an ALL-CAPS name all caps when an ending is added.</summary>
        private static string Match(string name, char ending) =>
            IsUpper(name) ? char.ToUpperInvariant(ending).ToString() : ending.ToString();
    }
}
