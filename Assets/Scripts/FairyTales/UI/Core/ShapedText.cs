using System.Reflection;
using RTLTMPro;
using TMPro;
using UnityEngine;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// Arabic, Urdu and Bengali for every TMP text in the app, without swapping components.
    ///
    /// TMP can't join Arabic letters, lay a line out right to left, or build Bengali
    /// conjuncts by itself. So each text gets this preprocessor: RTLTMPro turns Arabic
    /// letters into their joined presentation forms and the text is put into TMP's
    /// right-to-left layout; Bengali goes through <see cref="BengaliShaper"/>.
    /// It decides by CONTENT, not by app language: a text with neither script passes
    /// through untouched, and the "العربية" tile on the language screen is shaped even
    /// while the app is in Russian.
    ///
    /// The direction and alignment are written straight into TMP's fields. The public
    /// setters would mark the text dirty from inside the parse, which runs inside the
    /// canvas rebuild — Unity refuses that.
    /// </summary>
    public sealed class ShapedText : ITextPreprocessor
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo RtlField = typeof(TMP_Text).GetField("m_isRightToLeft", Flags);
        private static readonly FieldInfo AlignField = typeof(TMP_Text).GetField("m_HorizontalAlignment", Flags);
        private static readonly FastStringBuilder Shaped = new FastStringBuilder(RTLSupport.DefaultBufferSize);

        private readonly TMP_Text _text;
        private bool _rtl;
        private bool _flippedAlign;

        private ShapedText(TMP_Text text) => _text = text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

        /// <summary>Scene texts get it before their first frame, inactive screens included.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToScene()
        {
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Attach(t);
        }

        /// <summary>
        /// Texts spawned later (cards, popups) are caught on their first render. That pass
        /// ran unshaped, so an Arabic or Bengali one is rebuilt at once, before the frame is drawn.
        /// </summary>
        private static void OnTextChanged(Object obj)
        {
            if (obj is not TMP_Text t || t.textPreprocessor != null) return;
            Attach(t);
            if (HasArabic(t.text) || BengaliShaper.HasBengali(t.text)) t.ForceMeshUpdate(false, true);
        }

        private static void Attach(TMP_Text t)
        {
            if (t.textPreprocessor != null) return;
            // An input field maps its caret onto the raw characters: leave its text alone.
            var input = t.GetComponentInParent<TMP_InputField>(true);
            t.textPreprocessor = input != null && input.textComponent == t
                ? Passthrough.Instance
                : new ShapedText(t);
        }

        public string PreprocessText(string text)
        {
            text = BengaliShaper.Shape(text, _text.richText);
            bool rtl = IsMostlyArabic(text);
            SetDirection(rtl);
            if (!rtl) return text;

            Shaped.Clear();
            // Western digits stay as typed; farsi mode keeps Urdu's ی from turning into Arabic ي.
            RTLSupport.FixRTL(text, Shaped, farsi: HasUrduLetters(text), fixTextTags: true, preserveNumbers: true);
            Shaped.Reverse();
            return Shaped.ToString();
        }

        /// <summary>
        /// Right-to-left layout. Left-aligned PARAGRAPHS move to the right edge; a single-line
        /// caption stays put, since it usually sits next to an icon laid out around it.
        /// </summary>
        private void SetDirection(bool rtl)
        {
            if (rtl == _rtl) return;
            _rtl = rtl;
            RtlField?.SetValue(_text, rtl);
            if (rtl)
            {
                _flippedAlign = _text.horizontalAlignment == HorizontalAlignmentOptions.Left
                                && _text.textWrappingMode != TextWrappingModes.NoWrap;
                if (_flippedAlign) AlignField?.SetValue(_text, HorizontalAlignmentOptions.Right);
            }
            else if (_flippedAlign)
            {
                _flippedAlign = false;
                AlignField?.SetValue(_text, HorizontalAlignmentOptions.Left);
            }
        }

        private static bool IsArabic(char c) => TextUtils.IsArabicCharacter(c) || c >= 0x0750 && c <= 0x077F;

        private static bool HasArabic(string s)
        {
            if (s == null) return false;
            foreach (var c in s) if (IsArabic(c)) return true;
            return false;
        }

        /// <summary>
        /// Arabic letters outnumber all others (tags skipped) — or there are any at all while
        /// the app itself is in Arabic or Urdu, so a long Latin child name inside an Arabic
        /// sentence keeps the sentence right-to-left.
        /// </summary>
        private static bool IsMostlyArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int arabic = 0, other = 0;
            bool inTag = false;
            foreach (var c in s)
            {
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (inTag || !char.IsLetter(c)) continue;
                else if (IsArabic(c)) arabic++;
                else other++;
            }
            return arabic > 0 && (arabic >= other || Loc.IsRtl(Loc.Lang));
        }

        /// <summary>Letters Urdu uses and Arabic never does.</summary>
        private static bool HasUrduLetters(string s)
        {
            foreach (var c in s)
                switch (c)
                {
                    case 'ٹ': case 'ڈ': case 'ڑ': case 'ک': case 'گ':
                    case 'ں': case 'ھ': case 'ہ': case 'ۂ': case 'ی':
                    case 'ے': case 'ۓ':
                        return true;
                }
            return false;
        }

        private sealed class Passthrough : ITextPreprocessor
        {
            public static readonly Passthrough Instance = new();
            public string PreprocessText(string text) => text;
        }
    }
}
