using System.Reflection;
using FairyTales.UI.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace FairyTales.Editor
{
    /// <summary>
    /// Bakes a "NotoSansBengali PUA" asset: a static TMP font asset holding EVERY glyph of
    /// Noto Sans Bengali, glyph id N mapped to character <see cref="BengaliShaper.PuaBase"/> + N.
    /// HarfBuzz emits those characters at runtime (<see cref="BengaliShaper"/>), so conjuncts
    /// that have no Unicode code point of their own still reach the screen.
    /// The font must stay the same file as the runtime copy in Resources/Fonts, or glyph ids
    /// won't match. Called by <see cref="FontFallbackSetup"/>, which wires the result in.
    /// </summary>
    public static class BengaliFontBaker
    {
        private const string FontPath = "Assets/Fonts/NotoSansBengali-Medium.ttf";
        private const int GlyphCount = 418; // maxp.numGlyphs of the font

        public static TMP_FontAsset Bake(string name, int pointSize, int padding)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var asset = TMP_FontAsset.CreateFontAsset(font, pointSize, padding, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, false);
            asset.name = name;

            // TMP adds glyphs only by Unicode publicly; by glyph id is internal.
            var addGlyph = typeof(TMP_FontAsset).GetMethod("TryAddGlyphInternal",
                BindingFlags.Instance | BindingFlags.NonPublic);
            int added = 0;
            for (uint id = 1; id < GlyphCount; id++)
            {
                var args = new object[] { id, null };
                if (!(bool)addGlyph.Invoke(asset, args)) continue;
                asset.characterTable.Add(new TMP_Character((uint)BengaliShaper.PuaBase + id, asset, (Glyph)args[1]));
                added++;
            }
            asset.atlasTexture.Apply(false, false);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.ReadFontAssetDefinition();

            // The font's own line box (1.33 em), plus the <voffset> HarfBuzz puts on some marks,
            // made one-line Bengali taller than boxes sized for Latin, and Ellipsis/Truncate
            // then dropped the whole line. Glyphs may still draw past these lines.
            var face = asset.faceInfo;
            face.ascentLine = 0.85f * face.pointSize;
            face.descentLine = -0.30f * face.pointSize;
            face.lineHeight = 1.25f * face.pointSize;
            asset.faceInfo = face;

            if (added < GlyphCount - 1)
                Debug.LogWarning($"[BengaliFontBaker] {name}: only {added} glyphs fit the atlas");
            return FontFallbackSetup.SaveKeepingGuid(asset);
        }
    }
}
