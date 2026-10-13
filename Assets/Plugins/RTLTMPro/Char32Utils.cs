using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLTMPro {
    public static class Char32Utils {

        public static bool IsUnicode16Char(int ch) {
            return ch < 0xFFFF;
        }

        // Wrappers for TextUtil methods
        public static bool IsRTLCharacter(int ch) {
            if (!IsUnicode16Char(ch)) return false;
            return TextUtils.IsRTLCharacter((char)ch);
        }

        // Any left-to-right letter, not only A-Z: a Cyrillic or accented child name inside
        // an Arabic sentence must keep its order too. (Changed from upstream RTLTMPro.)
        public static bool IsEnglishLetter(int ch) {
            if (!IsUnicode16Char(ch)) return false;
            char c = (char)ch;
            return TextUtils.IsEnglishLetter(c) || char.IsLetter(c) && !TextUtils.IsRTLCharacter(c);
        }

        public static bool IsNumber(int ch, bool preserveNumbers, bool farsi) {
            if (!IsUnicode16Char(ch)) return false;
            return TextUtils.IsNumber((char)ch, preserveNumbers, farsi);
        }

        // Wrappers for System.Char (char) methods
        public static bool IsSymbol(int ch) {
            if (!IsUnicode16Char(ch)) return false;
            return char.IsSymbol((char)ch);
        }

        public static bool IsLetter(int ch) {
            if (!IsUnicode16Char(ch)) return false;
            return char.IsLetter((char)ch);
        }

        public static bool IsPunctuation(int ch) {
            if (!IsUnicode16Char(ch)) return false;
            return char.IsPunctuation((char)ch);
        }

        public static bool IsWhiteSpace(int ch) {
            if (!IsUnicode16Char(ch)) return false;
            return char.IsWhiteSpace((char)ch);
        }

        
    }
}
