using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using FairyTales.UI.Reading;

namespace FairyTales.Editor
{
    /// <summary>
    /// Builds the background-music volume UI on the reading screen: a "Volume" row in the
    /// settings panel and a slider panel behind it. Both are cloned from the text-size row
    /// and panel already in the scene, so the result matches the app without a single new
    /// art asset — which is also what makes this portable to the twin project, where the
    /// asset GUIDs differ but the layout does not.
    ///
    /// Idempotent: a second run only re-wires what is already there. Layout — the panel
    /// growing by one row — happens once, on first creation, so a hand-adjusted panel is
    /// never shifted again.
    /// </summary>
    public static class VolumeUISetup
    {
        private const string TableName = "Table";
        private const string VolumeKey = "volume";
        private const string PanelName = "volume panel";
        private const string ButtonName = "volume-btn";
        private const string SliderName = "volume-slider";
        private const string IconPath = "Assets/UI/lang/sound-on.png";

        private static readonly Dictionary<string, string> Translations = new()
        {
            { "en", "Volume" },
            { "ru-RU", "Громкость" },
            { "kk-KZ", "Дыбыс деңгейі" },
            { "uz", "Ovoz balandligi" },
        };

        [MenuItem("FairyTales/Setup Volume UI")]
        public static void Setup()
        {
            var rs = Object.FindAnyObjectByType<ReadingScreen>(FindObjectsInactive.Include);
            if (rs == null)
            {
                Debug.LogError("[VolumeUISetup] No ReadingScreen in the scene — is SampleScene open?");
                return;
            }

            // Everything is located through ReadingScreen's own references rather than by
            // name: the text-size row is literally called "size-btn " (trailing space) in
            // one repo, and nothing guarantees the twin spells it the same.
            var so = new SerializedObject(rs);
            var settings = GameObjectOf(so, "settingsPanel");
            var sizeButton = ComponentOf<Button>(so, "btnSettingsTextSize");
            var sizePanel = GameObjectOf(so, "textSizePanel");
            var sizeSlider = ComponentOf<Slider>(so, "textSizeSlider");
            var tocButton = ComponentOf<Button>(so, "btnSettingsToc");

            if (settings == null || sizeButton == null || sizePanel == null || sizeSlider == null)
            {
                Debug.LogError("[VolumeUISetup] The text-size UI this clones is not wired on " +
                               "ReadingScreen (settingsPanel / btnSettingsTextSize / textSizePanel / " +
                               "textSizeSlider). Wire those first — there is nothing to copy from.");
                return;
            }

            long keyId = EnsureLocalizationKey();
            if (keyId == 0) return;

            var panel = BuildPanel(rs, sizePanel, sizeSlider, keyId, out var panelClose, out var slider);
            var button = BuildButton(settings.transform, sizeButton, keyId,
                tocButton, sizeButton.transform as RectTransform);

            so.Update();
            so.FindProperty("btnSettingsVolume").objectReferenceValue = button;
            so.FindProperty("volumePanel").objectReferenceValue = panel;
            so.FindProperty("btnVolumeClose").objectReferenceValue = panelClose;
            so.FindProperty("volumeSlider").objectReferenceValue = slider;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(rs.gameObject.scene);
            Debug.Log($"[VolumeUISetup] Done (localization key '{VolumeKey}' = {keyId}). Save the scene to keep it.");
        }

        // ── Localization ─────────────────────────────────────
        /// <returns>The shared-table id of the "volume" entry, or 0 if the table is missing.</returns>
        private static long EnsureLocalizationKey()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            if (collection == null)
            {
                Debug.LogError($"[VolumeUISetup] No string table collection named '{TableName}'.");
                return 0;
            }

            var shared = collection.SharedData;
            var entry = shared.GetEntry(VolumeKey) ?? shared.AddKey(VolumeKey);

            foreach (var table in collection.StringTables)
            {
                if (!Translations.TryGetValue(table.LocaleIdentifier.Code, out var text)) continue;

                var line = table.GetEntry(entry.Id);
                if (line == null) table.AddEntry(entry.Id, text);
                else if (string.IsNullOrEmpty(line.Value)) line.Value = text; // never overwrite a human edit
                else continue;

                EditorUtility.SetDirty(table);
            }

            EditorUtility.SetDirty(shared);
            AssetDatabase.SaveAssets();
            return entry.Id;
        }

        // ── The slider panel ─────────────────────────────────
        private static GameObject BuildPanel(ReadingScreen rs, GameObject sizePanel, Slider sizeSlider,
            long keyId, out Button close, out Slider slider)
        {
            var existing = FindChild(rs.transform, PanelName);
            var panel = existing != null ? existing.gameObject : ClonePanel(sizePanel, sizeSlider);

            slider = FindChild(panel.transform, SliderName)?.GetComponent<Slider>();
            if (slider == null)
            {
                // A freshly cloned panel still carries the text-size slider's name.
                slider = panel.GetComponentInChildren<Slider>(true);
                if (slider != null) slider.name = SliderName;
            }

            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
            }

            // The close button is the only Button that is a direct child of the panel.
            close = null;
            foreach (Transform child in panel.transform)
            {
                var btn = child.GetComponent<Button>();
                if (btn != null) { close = btn; break; }
            }

            SetLocalizedTitle(panel.transform, keyId);
            panel.SetActive(false);
            return panel;
        }

        private static GameObject ClonePanel(GameObject sizePanel, Slider sizeSlider)
        {
            var panel = Object.Instantiate(sizePanel, sizePanel.transform.parent);
            panel.name = PanelName;
            panel.transform.SetSiblingIndex(sizePanel.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(panel, "Setup Volume UI");
            CopyRect(sizePanel.transform as RectTransform, panel.transform as RectTransform);

            // The text-size panel labels its ends "A -" / "A +"; for volume the letter
            // means nothing, so the signs stand alone. The two labels sit beside the
            // slider, one per sibling of its row — found that way rather than by name.
            var cloneSlider = panel.GetComponentInChildren<Slider>(true);
            if (cloneSlider != null && cloneSlider.transform.parent != null)
            {
                var labels = new List<TMP_Text>();
                foreach (Transform child in cloneSlider.transform.parent)
                {
                    var label = child.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) labels.Add(label);
                }
                if (labels.Count >= 2)
                {
                    labels[0].text = "-";
                    labels[1].text = "+";
                }
            }

            return panel;
        }

        // ── The settings row ─────────────────────────────────
        private static Button BuildButton(Transform settings, Button sizeButton, long keyId,
            Button tocButton, RectTransform sizeRect)
        {
            var existing = FindChild(settings, ButtonName);
            if (existing != null)
            {
                SetLocalizedLabel(existing, keyId);
                return existing.GetComponent<Button>();
            }

            var row = Object.Instantiate(sizeButton.gameObject, settings);
            row.name = ButtonName;
            row.transform.SetSiblingIndex(sizeButton.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(row, "Setup Volume UI");
            CopyRect(sizeRect, row.transform as RectTransform);

            SetLocalizedLabel(row.transform, keyId);
            SetIcon(row.transform);

            // Row pitch comes from the two rows already there, so a twin that lays its
            // panel out differently still gets an evenly spaced third row.
            var tocRect = tocButton != null ? tocButton.transform as RectTransform : null;
            float pitch = tocRect != null
                ? Mathf.Abs(tocRect.anchoredPosition.y - sizeRect.anchoredPosition.y)
                : sizeRect.rect.height;

            GrowPanel(settings as RectTransform, pitch, tocRect, sizeRect, row.transform as RectTransform);
            return row.GetComponent<Button>();
        }

        /// <summary>
        /// Makes room for one more row. The panel hangs from its top edge, so it grows
        /// downwards: the centre drops by half a row and every row moves back up by the
        /// same amount, leaving the header and close button exactly where they were.
        /// </summary>
        private static void GrowPanel(RectTransform panel, float pitch,
            RectTransform toc, RectTransform size, RectTransform added)
        {
            if (panel == null) return;

            float shift = pitch / 2f;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + pitch);
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, panel.anchoredPosition.y - shift);

            foreach (var row in new[] { toc, size, added })
            {
                if (row == null) continue;
                row.anchoredPosition = new Vector2(row.anchoredPosition.x, row.anchoredPosition.y + shift);
            }

            added.anchoredPosition = new Vector2(added.anchoredPosition.x, size.anchoredPosition.y - pitch);
        }

        private static void SetIcon(Transform row)
        {
            Sprite speaker = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(IconPath))
                if (asset is Sprite sprite) speaker = sprite;

            if (speaker == null)
            {
                Debug.LogWarning($"[VolumeUISetup] No sprite at {IconPath}; the new row keeps the " +
                                 "text-size icon. Swap it by hand.");
                return;
            }

            var icon = FindChild(row, "icon");
            var image = icon != null ? icon.GetComponent<Image>() : null;
            if (image == null) return;

            image.sprite = speaker;
            var rect = (RectTransform)icon;
            // The speaker is square where the "Tt" glyph is wide; keep its left inset.
            float side = Mathf.Min(rect.sizeDelta.x, rect.sizeDelta.y);
            rect.sizeDelta = new Vector2(side, side);
        }

        // ── Helpers ──────────────────────────────────────────
        private static void SetLocalizedTitle(Transform panel, long keyId)
        {
            foreach (Transform child in panel)
            {
                var localized = child.GetComponent<LocalizeStringEvent>();
                if (localized == null) continue;
                localized.StringReference.SetReference(TableName, keyId);
                EditorUtility.SetDirty(localized);
                return;
            }
        }

        private static void SetLocalizedLabel(Transform row, long keyId)
        {
            var localized = row.GetComponentInChildren<LocalizeStringEvent>(true);
            if (localized == null) return;
            localized.StringReference.SetReference(TableName, keyId);
            EditorUtility.SetDirty(localized);
        }

        private static void CopyRect(RectTransform from, RectTransform to)
        {
            if (from == null || to == null) return;
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localScale = from.localScale;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child;
            return null;
        }

        private static GameObject GameObjectOf(SerializedObject so, string field)
            => so.FindProperty(field)?.objectReferenceValue as GameObject;

        private static T ComponentOf<T>(SerializedObject so, string field) where T : Component
            => so.FindProperty(field)?.objectReferenceValue as T;
    }
}
