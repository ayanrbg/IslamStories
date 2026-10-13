using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// Bengali for TMP. Bengali builds conjuncts and moves vowel signs around the consonant;
    /// TMP only maps one character to one glyph, so drawn as-is half the words break.
    ///
    /// HarfBuzz shapes every Bengali word into glyph ids of Noto Sans Bengali. Each id is
    /// written as a private-use character (<see cref="PuaBase"/> + id), and the baked
    /// "NotoSansBengali PUA" font asset (TMP global fallback) maps it back to that glyph.
    /// Mark offsets and advance corrections from GPOS become &lt;voffset&gt;/&lt;space&gt; tags.
    /// </summary>
    public static class BengaliShaper
    {
        /// <summary>Start of Supplementary Private Use Area-A: no font in the app uses it.</summary>
        public const int PuaBase = 0xF0000;

        /// <summary>Same file as Assets/Fonts/NotoSansBengali-Medium.ttf — glyph ids must match the baked asset.</summary>
        private const string FontResource = "Fonts/NotoSansBengali-Medium";

        private static bool? _available;
        private static IntPtr _font, _buffer, _language;
        private static float _upem;
        private static readonly Dictionary<uint, int> Advances = new();
        private static readonly StringBuilder Out = new();

        /// <summary>False when the native library or the font is missing (iOS for now).</summary>
        public static bool Available => _available ??= Init();

        public static bool IsBengali(char c) => c >= 'ঀ' && c <= '৿';

        public static bool HasBengali(string s)
        {
            if (s == null) return false;
            foreach (var c in s) if (IsBengali(c)) return true;
            return false;
        }

        /// <summary>Bengali runs replaced by shaped glyphs; everything else is left as it was.</summary>
        public static string Shape(string text, bool richText)
        {
            if (!Available || !HasBengali(text)) return text;
            Out.Clear();
            int i = 0;
            while (i < text.Length)
            {
                if (!IsBengali(text[i])) { Out.Append(text[i++]); continue; }
                int start = i;
                while (i < text.Length && IsRunChar(text[i])) i++;
                ShapeRun(text, start, i - start, richText);
            }
            return Out.ToString();
        }

        // Danda and the joiners belong to the word around them; spaces and tags end it.
        private static bool IsRunChar(char c) =>
            IsBengali(c) || c == '।' || c == '॥' || c == '‌' || c == '‍';

        private static bool Init()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return false;
#else
            try
            {
                var asset = Resources.Load<TextAsset>(FontResource);
                if (asset == null) return false;
                var bytes = asset.bytes;
                var blob = HarfBuzz.hb_blob_create(bytes, (uint)bytes.Length, HarfBuzz.MemoryModeDuplicate, IntPtr.Zero, IntPtr.Zero);
                var face = HarfBuzz.hb_face_create(blob, 0);
                HarfBuzz.hb_blob_destroy(blob);
                Resources.UnloadAsset(asset);

                _upem = HarfBuzz.hb_face_get_upem(face);
                _font = HarfBuzz.hb_font_create(face);
                HarfBuzz.hb_face_destroy(face);
                HarfBuzz.hb_font_set_scale(_font, (int)_upem, (int)_upem);
                _buffer = HarfBuzz.hb_buffer_create();
                _language = HarfBuzz.hb_language_from_string("bn", -1);
                return _upem > 0;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                return false;
            }
#endif
        }

#if !UNITY_IOS || UNITY_EDITOR
        private static void ShapeRun(string text, int start, int length, bool richText)
        {
            HarfBuzz.hb_buffer_clear_contents(_buffer);
            // The whole string goes in as context; only the run is shaped.
            HarfBuzz.hb_buffer_add_utf16(_buffer, text, text.Length, (uint)start, length);
            HarfBuzz.hb_buffer_set_direction(_buffer, HarfBuzz.DirectionLtr);
            HarfBuzz.hb_buffer_set_script(_buffer, HarfBuzz.ScriptBengali);
            HarfBuzz.hb_buffer_set_language(_buffer, _language);
            HarfBuzz.hb_shape(_font, _buffer, IntPtr.Zero, 0);

            var infos = HarfBuzz.hb_buffer_get_glyph_infos(_buffer, out var count);
            var positions = HarfBuzz.hb_buffer_get_glyph_positions(_buffer, out _);
            for (int g = 0; g < count; g++)
            {
                var glyph = (uint)Marshal.ReadInt32(infos, g * HarfBuzz.GlyphInfoSize);
                int p = g * HarfBuzz.GlyphPositionSize;
                int xAdvance = Marshal.ReadInt32(positions, p);
                int xOffset = Marshal.ReadInt32(positions, p + 8);
                int yOffset = Marshal.ReadInt32(positions, p + 12);
                AppendGlyph(glyph, xAdvance, xOffset, yOffset, richText);
            }
        }

        /// <summary>
        /// TMP advances by the glyph's own width; HarfBuzz may want it shifted (marks) or
        /// spaced differently. The difference goes in as em-based tags around the glyph.
        /// </summary>
        private static void AppendGlyph(uint glyph, int xAdvance, int xOffset, int yOffset, bool richText)
        {
            if (richText && xOffset != 0) Tag("space", xOffset);
            if (richText && yOffset != 0) Tag("voffset", yOffset);
            Out.Append(char.ConvertFromUtf32(PuaBase + (int)glyph));
            if (richText && yOffset != 0) Out.Append("</voffset>");
            int rest = xAdvance - DefaultAdvance(glyph) - xOffset;
            if (richText && rest != 0) Tag("space", rest);
        }

        private static void Tag(string name, int fontUnits) =>
            Out.Append('<').Append(name).Append('=')
               .Append((fontUnits / _upem).ToString("0.####", CultureInfo.InvariantCulture)).Append("em>");

        private static int DefaultAdvance(uint glyph)
        {
            if (!Advances.TryGetValue(glyph, out var advance))
                Advances[glyph] = advance = HarfBuzz.hb_font_get_glyph_h_advance(_font, glyph);
            return advance;
        }
#else
        private static void ShapeRun(string text, int start, int length, bool richText) =>
            Out.Append(text, start, length);
#endif
    }
}
