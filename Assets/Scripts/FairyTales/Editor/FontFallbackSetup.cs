using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace FairyTales.Editor
{
    /// <summary>
    /// Builds the fallback fonts that draw what the styled fonts lack (Kazakh in the static
    /// Gilroy atlases, ʻ, Arabic, Bengali) and wires them in.
    ///
    /// The rule: a fallback must have the same SDF spread (atlas padding / sampling size) as
    /// the font it backs. TMP copies the primary material's dilate, outline and underlay onto
    /// the fallback in SDF units, so on a fallback with a wider spread the same settings draw
    /// thicker. G-Medium's drop shadow on a 0.244 fallback made every Kazakh letter look bolder
    /// and bigger than the rest of the word (10.2026). Hence two sets:
    ///   0.10 — G-Medium (shadow), Montserrat, TT Rounds, and the global list;
    ///   0.24 — G-ORANGE and G-M-OUTLINESDF (dilate + outline).
    /// Safe to re-run: an asset already built with the right settings is kept as it is.
    /// </summary>
    public static class FontFallbackSetup
    {
        private const string Dir = "Assets/Fonts/";

        [MenuItem("FairyTales/Rebuild Font Fallbacks")]
        public static void Rebuild()
        {
            var gilroy = Dynamic("Gilroy-Medium-KZ.ttf", "Gilroy-Medium Dynamic", 90, 9, 1024);
            var montserrat = Dynamic("Montserrat-Bold.ttf", "Montserrat-Bold Dynamic", 90, 9, 1024);
            var noto = Dynamic("NotoSans-Medium.ttf", "NotoSans-Medium Dynamic", 70, 7, 1024);
            var naskh = Naskh(Dynamic("NotoNaskhArabic-Medium.ttf", "NotoNaskhArabic-Medium Dynamic", 70, 7, 2048));
            var naskhBold = Naskh(Dynamic("NotoNaskhArabic-Bold.ttf", "NotoNaskhArabic-Bold Dynamic", 70, 7, 2048));
            var bengali = Bengali("NotoSansBengali PUA", 70, 7);

            var gilroyOutline = Dynamic("Gilroy-Medium-KZ.ttf", "Gilroy-Medium Outline Dynamic", 100, 24, 1024);
            var notoOutline = Dynamic("NotoSans-Medium.ttf", "NotoSans-Medium Outline Dynamic", 100, 24, 1024);
            var naskhOutline = Naskh(Dynamic("NotoNaskhArabic-Medium.ttf", "NotoNaskhArabic-Medium Outline Dynamic", 100, 24, 1024));
            var bengaliOutline = Bengali("NotoSansBengali PUA Outline", 50, 12);

            SetFallbacks("G-Medium", gilroy);
            SetFallbacks("G-ORANGE", gilroyOutline, notoOutline, naskhOutline, bengaliOutline);
            SetFallbacks("G-M-OUTLINESDF", gilroyOutline, notoOutline, naskhOutline, bengaliOutline);
            SetFallbacks("Montserrat-Bold SDF", montserrat, naskhBold);
            SetFallbacks("TTRoundsNeue-Bold SDF", naskhBold);
            SetFallbacks("TTRoundsNeue-ExtraBold SDF", naskhBold);
            SetGlobalFallbacks(noto, naskh, bengali);
            AssetDatabase.SaveAssets();
            Debug.Log("[FontFallbackSetup] done");
        }

        private static TMP_FontAsset Dynamic(string ttf, string name, int pointSize, int padding, int atlasSize)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir + name + ".asset");
            if (Matches(existing, pointSize, padding)) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(Dir + ttf);
            var asset = TMP_FontAsset.CreateFontAsset(font, pointSize, padding, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            asset = SaveKeepingGuid(asset);
            // Glyphs pile up in the atlas while the editor renders text; builds start empty.
            var so = new SerializedObject(asset);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static TMP_FontAsset Bengali(string name, int pointSize, int padding)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir + name + ".asset");
            return Matches(existing, pointSize, padding) ? existing : BengaliFontBaker.Bake(name, pointSize, padding);
        }

        private static bool Matches(TMP_FontAsset asset, int pointSize, int padding) =>
            asset != null && asset.faceInfo.pointSize == pointSize && asset.atlasPadding == padding;

        /// <summary>
        /// Naskh's own line box (1.7 em) is built for stacked harakat; it made one-line Arabic
        /// taller than boxes sized for Latin, and Ellipsis/Truncate then dropped the whole line.
        /// The 1.12 scale brings its small letter bodies up to the Latin fonts beside them.
        /// </summary>
        private static TMP_FontAsset Naskh(TMP_FontAsset asset)
        {
            var face = asset.faceInfo;
            face.ascentLine = 0.90f * face.pointSize;
            face.descentLine = -0.32f * face.pointSize;
            face.lineHeight = 1.30f * face.pointSize;
            face.scale = 1.12f;
            asset.faceInfo = face;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void SetFallbacks(string fontName, params TMP_FontAsset[] fallbacks)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Dir + fontName + ".asset");
            font.fallbackFontAssetTable = new List<TMP_FontAsset>(fallbacks);
            EditorUtility.SetDirty(font);
        }

        private static void SetGlobalFallbacks(params TMP_FontAsset[] fallbacks)
        {
            var settings = new SerializedObject(TMP_Settings.instance);
            var list = settings.FindProperty("m_fallbackFontAssets");
            list.ClearArray();
            foreach (var f in fallbacks)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = f;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(TMP_Settings.instance);
        }

        /// <summary>
        /// Saves a freshly created font asset (with its atlas and material) under its name.
        /// When that file already exists its contents are replaced but its .meta is kept, so
        /// the GUID — and every reference to the asset — survives a rebuild.
        /// </summary>
        public static TMP_FontAsset SaveKeepingGuid(TMP_FontAsset asset)
        {
            var name = asset.name;
            var path = Dir + name + ".asset";
            var temp = Dir + "_new_" + name + ".asset";
            AssetDatabase.CreateAsset(asset, temp); // renames the asset after the temp file
            asset.name = name;
            asset.atlasTexture.name = name + " Atlas";
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.SaveAssets();

            if (File.Exists(path))
            {
                File.Copy(temp, path, true);
                AssetDatabase.DeleteAsset(temp);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                AssetDatabase.MoveAsset(temp, path);
            }
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }
    }
}
