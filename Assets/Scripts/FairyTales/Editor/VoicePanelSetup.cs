using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FairyTales.UI.Library;

namespace FairyTales.Editor
{
    public static class VoicePanelSetup
    {
        [MenuItem("FairyTales/Setup Voice Panel in TaleDetail")]
        public static void Setup()
        {
            var screen = Object.FindAnyObjectByType<TaleDetailScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogError("[VoicePanelSetup] TaleDetailScreen not found!");
                return;
            }

            var so = new SerializedObject(screen);
            if (so.FindProperty("voicePanel").objectReferenceValue != null)
            {
                Debug.LogWarning("[VoicePanelSetup] voicePanel already assigned. Skipping.");
                return;
            }

            var screenRt = screen.GetComponent<RectTransform>();

            // ── VoicePanel (inactive by default) ─────────────
            var panel = CreateChild("VoicePanel", screenRt);
            Stretch(panel);
            panel.gameObject.SetActive(false);

            // ── Title ────────────────────────────────────────
            var titleRt = CreateChild("VoiceTitle", panel);
            titleRt.anchorMin = new Vector2(0.2f, 0.82f);
            titleRt.anchorMax = new Vector2(0.8f, 0.92f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            var titleTmp = titleRt.gameObject.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Выберите голос диктора";
            titleTmp.fontSize = 42;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;
            titleTmp.fontStyle = FontStyles.Bold;

            // ── Back button (top-left) ───────────────────────
            var backRt = CreateChild("BtnVoiceBack", panel);
            backRt.anchorMin = new Vector2(0f, 1f);
            backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(24, -24);
            backRt.sizeDelta = new Vector2(80, 80);
            var backBg = backRt.gameObject.AddComponent<Image>();
            backBg.color = new Color(1f, 1f, 1f, 0.85f);
            var backBtn = backRt.gameObject.AddComponent<Button>();
            backBtn.targetGraphic = backBg;

            var backIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/back.png");
            if (backIconSprite != null)
            {
                var iconRt = CreateChild("Icon", backRt);
                Stretch(iconRt);
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = backIconSprite;
                iconImg.preserveAspect = true;
            }

            // ── Voice card (center) ──────────────────────────
            var cardRt = CreateChild("VoiceCard", panel);
            cardRt.anchorMin = new Vector2(0.25f, 0.5f);
            cardRt.anchorMax = new Vector2(0.75f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;

            var cardImg = cardRt.gameObject.AddComponent<Image>();
            cardImg.color = new Color(1f, 1f, 1f, 0.95f);

            var vlg = cardRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 20, 20);
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = cardRt.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var maleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/reading/male-dict.png");
            var femaleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/reading/female-dict.png");

            var btnMale = CreateVoiceButton("BtnVoiceMale", cardRt, "Диктор муж.", maleIcon);
            CreateSeparator(cardRt);
            var btnFemale = CreateVoiceButton("BtnVoiceFemale", cardRt, "Диктор жен.", femaleIcon);
            CreateSeparator(cardRt);
            var btnParent = CreateVoiceButton("BtnVoiceParent", cardRt, "Голос родителя", maleIcon);

            // ── Wire mainPanel — find existing content container ──
            // Wrap existing children (except VoicePanel) into MainPanel if needed
            // For now just wire references — user sets mainPanel manually
            so.FindProperty("voicePanel").objectReferenceValue = panel.gameObject;
            so.FindProperty("voiceTitleText").objectReferenceValue = titleTmp;
            so.FindProperty("btnVoiceMale").objectReferenceValue = btnMale;
            so.FindProperty("btnVoiceFemale").objectReferenceValue = btnFemale;
            so.FindProperty("btnVoiceParent").objectReferenceValue = btnParent;
            so.FindProperty("btnVoiceBack").objectReferenceValue = backBtn;

            // Wire btnNarrateText
            var btnNarrateProp = so.FindProperty("btnNarrate");
            if (btnNarrateProp.objectReferenceValue != null)
            {
                var narBtn = (Button)btnNarrateProp.objectReferenceValue;
                var narText = narBtn.GetComponentInChildren<TMP_Text>();
                if (narText != null)
                    so.FindProperty("btnNarrateText").objectReferenceValue = narText;
            }

            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create VoicePanel");
            Debug.Log("[VoicePanelSetup] Voice panel created inside TaleDetailScreen!\n" +
                      "NOTE: Set 'mainPanel' field manually — drag the GameObject that contains " +
                      "title, buttons (Read/Listen/Narrate) so they hide when voice panel shows.");
        }

        private static Button CreateVoiceButton(string name, RectTransform parent,
            string label, Sprite icon)
        {
            var root = CreateChild(name, parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 64;

            var hlg = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 8, 8);
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            if (icon != null)
            {
                var iconRt = CreateChild("Icon", root);
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                var iconLe = iconRt.gameObject.AddComponent<LayoutElement>();
                iconLe.preferredWidth = 48;
                iconLe.preferredHeight = 48;
            }

            var labelRt = CreateChild("Label", root);
            var labelTmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 32;
            labelTmp.color = new Color(0.2f, 0.15f, 0.3f);
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.fontStyle = FontStyles.Bold;
            var labelLe = labelRt.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1;

            var btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            return btn;
        }

        private static void CreateSeparator(RectTransform parent)
        {
            var sep = CreateChild("Separator", parent);
            var le = sep.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 1;
            var img = sep.gameObject.AddComponent<Image>();
            img.color = new Color(0.85f, 0.82f, 0.9f);
        }

        private static RectTransform CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
