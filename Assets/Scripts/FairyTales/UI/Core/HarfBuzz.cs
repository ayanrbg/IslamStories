#if !UNITY_IOS || UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// The slice of the HarfBuzz C API that <see cref="BengaliShaper"/> needs. The library is
    /// the prebuilt libHarfBuzzSharp from NuGet (Assets/Plugins/HarfBuzz). iOS has no build of
    /// it in the project yet, so this file compiles out there and Bengali stays unavailable.
    /// </summary>
    internal static class HarfBuzz
    {
        private const string Lib = "libHarfBuzzSharp";

        public const int MemoryModeDuplicate = 0;
        public const int DirectionLtr = 4;
        public const uint ScriptBengali = 0x42656E67; // 'Beng'
        public const int GlyphInfoSize = 20;          // hb_glyph_info_t: 5 x uint32
        public const int GlyphPositionSize = 20;      // hb_glyph_position_t: 5 x int32

        [DllImport(Lib)] public static extern IntPtr hb_blob_create(byte[] data, uint length, int mode, IntPtr userData, IntPtr destroy);
        [DllImport(Lib)] public static extern void hb_blob_destroy(IntPtr blob);
        [DllImport(Lib)] public static extern IntPtr hb_face_create(IntPtr blob, uint index);
        [DllImport(Lib)] public static extern void hb_face_destroy(IntPtr face);
        [DllImport(Lib)] public static extern uint hb_face_get_upem(IntPtr face);
        [DllImport(Lib)] public static extern IntPtr hb_font_create(IntPtr face);
        [DllImport(Lib)] public static extern void hb_font_set_scale(IntPtr font, int xScale, int yScale);
        [DllImport(Lib)] public static extern int hb_font_get_glyph_h_advance(IntPtr font, uint glyph);
        [DllImport(Lib)] public static extern IntPtr hb_buffer_create();
        [DllImport(Lib)] public static extern void hb_buffer_clear_contents(IntPtr buffer);
        [DllImport(Lib)] public static extern void hb_buffer_add_utf16(IntPtr buffer, [MarshalAs(UnmanagedType.LPWStr)] string text, int textLength, uint itemOffset, int itemLength);
        [DllImport(Lib)] public static extern void hb_buffer_set_direction(IntPtr buffer, int direction);
        [DllImport(Lib)] public static extern void hb_buffer_set_script(IntPtr buffer, uint script);
        [DllImport(Lib)] public static extern void hb_buffer_set_language(IntPtr buffer, IntPtr language);
        [DllImport(Lib)] public static extern IntPtr hb_language_from_string([MarshalAs(UnmanagedType.LPStr)] string str, int len);
        [DllImport(Lib)] public static extern void hb_shape(IntPtr font, IntPtr buffer, IntPtr features, uint numFeatures);
        [DllImport(Lib)] public static extern IntPtr hb_buffer_get_glyph_infos(IntPtr buffer, out uint length);
        [DllImport(Lib)] public static extern IntPtr hb_buffer_get_glyph_positions(IntPtr buffer, out uint length);
    }
}
#endif
