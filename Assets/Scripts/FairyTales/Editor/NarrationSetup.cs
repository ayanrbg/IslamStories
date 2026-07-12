using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Core;
using FairyTales.UI.Narration;

namespace FairyTales.Editor
{
    public static class NarrationSetup
    {
        private static readonly Color BtnColor = new(0.55f, 0.36f, 0.85f);
        private static readonly Color PanelColor = new(0.18f, 0.12f, 0.25f);
        private static readonly Color RecordColor = new(0.85f, 0.2f, 0.2f);

        [MenuItem("FairyTales/Setup Narration UI")]
        public static void Setup()
        {
            SetupNarrationSetupScreen();
            SetupVoiceRecordingScreen();
            SetupNarrationProgressScreen();
            CreateDraftItemPrefab();
            Debug.Log("[NarrationSetup] Done!");
        }

        private static void SetupNarrationSetupScreen()
        {
            var screen = FindScreen<NarrationSetupScreen>();
            if (screen == null) return;
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            // Back button
            var btnBack = CreateIconButton(root, "BtnBack", "\u2190",
                new Vector2(0.02f, 0.92f), new Vector2(0.1f, 0.98f));

            // Cover
            var coverGo = new GameObject("Cover", typeof(RectTransform));
            coverGo.transform.SetParent(root, false);
            Anchor(coverGo, new Vector2(0.05f, 0.55f), new Vector2(0.4f, 0.88f));
            var coverImg = coverGo.AddComponent<Image>();
            coverImg.color = PanelColor;
            coverImg.preserveAspect = true;

            // Title
            var titleGo = CreateTMP(root, "Title", "Название",
                28, TextAlignmentOptions.Left,
                new Vector2(0.45f, 0.78f), new Vector2(0.95f, 0.88f));

            // Tab buttons
            var btnTabNew = CreateButton(root, "BtnTabNew", "Новая запись",
                new Vector2(0.05f, 0.45f), new Vector2(0.48f, 0.52f));
            var btnTabDrafts = CreateButton(root, "BtnTabDrafts", "Черновики",
                new Vector2(0.52f, 0.45f), new Vector2(0.95f, 0.52f));

            // Panel New
            var panelNew = CreatePanel(root, "PanelNew",
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.43f));

            var nameInput = CreateInputField(panelNew.transform, "NarratorName",
                "Имя рассказчика",
                new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.85f));

            var btnStart = CreateButton(panelNew.transform, "BtnStart", "Начать",
                new Vector2(0.25f, 0.1f), new Vector2(0.75f, 0.4f));

            // Panel Drafts
            var panelDrafts = CreatePanel(root, "PanelDrafts",
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.43f));

            var scrollGo = new GameObject("DraftsScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panelDrafts.transform, false);
            Stretch(scrollGo);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollGo.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(scrollGo.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            panelDrafts.SetActive(false);

            // Panel Quick Narrate (voice already cloned)
            var panelQuick = CreatePanel(root, "PanelQuickNarrate",
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.43f));

            CreateTMP(panelQuick.transform, "QuickLabel",
                "Голос уже записан. Озвучить эту сказку?",
                24, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.6f), new Vector2(0.95f, 0.85f));

            var btnNarrateNow = CreateButton(panelQuick.transform, "BtnNarrateNow",
                "Озвучить этим голосом",
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.55f));

            var btnRerecord = CreateButton(panelQuick.transform, "BtnRerecord",
                "Перезаписать голос",
                new Vector2(0.2f, 0.05f), new Vector2(0.8f, 0.25f));
            btnRerecord.GetComponent<Image>().color = PanelColor;

            panelQuick.SetActive(false);

            // Assign fields
            var so = new SerializedObject(screen);
            so.FindProperty("coverImage").objectReferenceValue = coverImg;
            so.FindProperty("titleText").objectReferenceValue =
                titleGo.GetComponent<TMP_Text>();
            so.FindProperty("narratorNameInput").objectReferenceValue =
                nameInput.GetComponent<TMP_InputField>();
            so.FindProperty("btnStart").objectReferenceValue =
                btnStart.GetComponent<Button>();
            so.FindProperty("btnBack").objectReferenceValue =
                btnBack.GetComponent<Button>();
            so.FindProperty("btnTabNew").objectReferenceValue =
                btnTabNew.GetComponent<Button>();
            so.FindProperty("btnTabDrafts").objectReferenceValue =
                btnTabDrafts.GetComponent<Button>();
            so.FindProperty("panelNew").objectReferenceValue = panelNew;
            so.FindProperty("panelDrafts").objectReferenceValue = panelDrafts;
            so.FindProperty("panelQuickNarrate").objectReferenceValue = panelQuick;
            so.FindProperty("btnNarrateNow").objectReferenceValue =
                btnNarrateNow.GetComponent<Button>();
            so.FindProperty("btnRerecord").objectReferenceValue =
                btnRerecord.GetComponent<Button>();
            so.FindProperty("draftsContainer").objectReferenceValue =
                content.transform;
            so.ApplyModifiedProperties();
        }

        private static void SetupVoiceRecordingScreen()
        {
            var screen = FindScreen<VoiceRecordingScreen>();
            if (screen == null) return;
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            // Back button
            var btnBack = CreateIconButton(root, "BtnBack", "\u2190",
                new Vector2(0.02f, 0.92f), new Vector2(0.1f, 0.98f));

            // Status text
            var statusGo = CreateTMP(root, "StatusText", "Нажмите запись",
                22, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.92f));

            // Timer
            var timerGo = CreateTMP(root, "Timer", "00:00",
                48, TextAlignmentOptions.Center,
                new Vector2(0.3f, 0.75f), new Vector2(0.7f, 0.85f));

            // Record / Play / Send buttons
            var btnRecord = CreateButton(root, "BtnRecord", "\u25CF Запись",
                new Vector2(0.1f, 0.62f), new Vector2(0.36f, 0.72f));
            // Make record button red
            btnRecord.GetComponent<Image>().color = RecordColor;

            var btnPlay = CreateButton(root, "BtnPlay", "\u25B6 Слушать",
                new Vector2(0.38f, 0.62f), new Vector2(0.62f, 0.72f));

            var btnSend = CreateButton(root, "BtnSend", "Отправить",
                new Vector2(0.64f, 0.62f), new Vector2(0.9f, 0.72f));

            // Sample text area
            var textPanel = CreatePanel(root, "TextPanel",
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.58f));
            textPanel.AddComponent<Image>().color = PanelColor;

            var scrollGo = new GameObject("TextScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(textPanel.transform, false);
            Stretch(scrollGo);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollGo.AddComponent<RectMask2D>();

            var textContent = new GameObject("Content", typeof(RectTransform));
            textContent.transform.SetParent(scrollGo.transform, false);
            var contentRt = textContent.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var sampleText = textContent.AddComponent<TextMeshProUGUI>();
            sampleText.fontSize = 24;
            sampleText.color = Color.white;
            sampleText.text = "Текст для чтения...";

            var textFitter = textContent.AddComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;

            // Assign fields
            var so = new SerializedObject(screen);
            so.FindProperty("sampleText").objectReferenceValue = sampleText;
            so.FindProperty("timerText").objectReferenceValue =
                timerGo.GetComponent<TMP_Text>();
            so.FindProperty("statusText").objectReferenceValue =
                statusGo.GetComponent<TMP_Text>();
            so.FindProperty("btnRecord").objectReferenceValue =
                btnRecord.GetComponent<Button>();
            so.FindProperty("btnPlay").objectReferenceValue =
                btnPlay.GetComponent<Button>();
            so.FindProperty("btnSend").objectReferenceValue =
                btnSend.GetComponent<Button>();
            so.FindProperty("btnBack").objectReferenceValue =
                btnBack.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }

        private static void SetupNarrationProgressScreen()
        {
            var screen = FindScreen<NarrationProgressScreen>();
            if (screen == null) return;
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            // Back button
            var btnBack = CreateIconButton(root, "BtnBack", "\u2190",
                new Vector2(0.02f, 0.92f), new Vector2(0.1f, 0.98f));

            // Status
            var statusGo = CreateTMP(root, "StatusText", "Идёт озвучка...",
                28, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.6f), new Vector2(0.9f, 0.7f));

            // Progress bar
            var sliderGo = new GameObject("ProgressBar", typeof(RectTransform));
            sliderGo.transform.SetParent(root, false);
            Anchor(sliderGo, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.55f));

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(sliderGo.transform, false);
            Stretch(bgGo);
            bgGo.AddComponent<Image>().color = PanelColor;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Stretch(fillArea);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillArea.transform, false);
            Stretch(fillGo);
            fillGo.AddComponent<Image>().color = BtnColor;

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.interactable = false;
            slider.minValue = 0;
            slider.maxValue = 1;

            // Pages text
            var pagesGo = CreateTMP(root, "PagesText", "",
                22, TextAlignmentOptions.Center,
                new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.48f));

            // Done button (hidden initially)
            var btnDone = CreateButton(root, "BtnDone", "Готово",
                new Vector2(0.3f, 0.25f), new Vector2(0.7f, 0.35f));
            btnDone.gameObject.SetActive(false);

            // Assign fields
            var so = new SerializedObject(screen);
            so.FindProperty("progressBar").objectReferenceValue = slider;
            so.FindProperty("statusText").objectReferenceValue =
                statusGo.GetComponent<TMP_Text>();
            so.FindProperty("pagesText").objectReferenceValue =
                pagesGo.GetComponent<TMP_Text>();
            so.FindProperty("btnDone").objectReferenceValue =
                btnDone.GetComponent<Button>();
            so.FindProperty("btnBack").objectReferenceValue =
                btnBack.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }

        private static void CreateDraftItemPrefab()
        {
            var prefabPath = "Assets/Prefabs/UI/DraftItem.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssignDraftPrefab(prefabPath);
                return;
            }

            var go = new GameObject("DraftItem", typeof(RectTransform));
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 60;

            go.AddComponent<Image>().color = PanelColor;
            go.AddComponent<Button>();

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Draft";
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.margin = new Vector4(16, 0, 16, 0);

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            AssignDraftPrefab(prefabPath);
            Debug.Log($"[NarrationSetup] Draft prefab: {prefabPath}");
        }

        private static void AssignDraftPrefab(string path)
        {
            var screen = FindScreen<NarrationSetupScreen>();
            if (screen == null) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            var so = new SerializedObject(screen);
            so.FindProperty("draftItemPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
        }

        // ── Helpers ─────────────────────────────────────────

        private static T FindScreen<T>() where T : BaseScreen
        {
            var s = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (s == null) Debug.LogError($"{typeof(T).Name} not found in scene");
            return s;
        }

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            return go;
        }

        private static GameObject CreateTMP(Transform parent, string name,
            string text, int fontSize, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            return go;
        }

        private static Button CreateButton(Transform parent, string name,
            string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            go.AddComponent<Image>().color = BtnColor;
            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return btn;
        }

        private static GameObject CreateIconButton(Transform parent, string name,
            string icon, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            go.AddComponent<Image>().color = BtnColor;
            go.AddComponent<Button>();
            var textGo = new GameObject("Icon", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = icon;
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return go;
        }

        private static GameObject CreateInputField(Transform parent, string name,
            string placeholder, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            go.AddComponent<Image>().color = new Color(0.25f, 0.18f, 0.35f);

            var textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            Stretch(textArea);
            textArea.AddComponent<RectMask2D>();

            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(textArea.transform, false);
            Stretch(phGo);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 22;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(1, 1, 1, 0.4f);

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(textArea.transform, false);
            Stretch(txtGo);
            var txtTmp = txtGo.AddComponent<TextMeshProUGUI>();
            txtTmp.fontSize = 22;
            txtTmp.color = Color.white;

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = txtTmp;
            input.placeholder = phTmp;

            return go;
        }

        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(GameObject go,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
