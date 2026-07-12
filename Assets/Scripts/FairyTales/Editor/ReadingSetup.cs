using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Core;
using FairyTales.UI.Reading;

namespace FairyTales.Editor
{
    public static class ReadingSetup
    {
        private static readonly Color BgColor = new(0.12f, 0.08f, 0.18f);
        private static readonly Color BtnColor = new(0.55f, 0.36f, 0.85f);
        private static readonly Color PanelColor = new(0.15f, 0.1f, 0.22f, 0.85f);
        private static readonly Color ThumbBgColor = new(0.2f, 0.14f, 0.3f);

        [MenuItem("FairyTales/Setup Reading UI")]
        public static void Setup()
        {
            SetupReadingScreen();
            CreateThumbnailPrefab();
            Debug.Log("[ReadingSetup] Done!");
        }

        private static void SetupReadingScreen()
        {
            var screen = Object.FindAnyObjectByType<ReadingScreen>(
                FindObjectsInactive.Include);

            if (screen == null)
                screen = FindOrAddComponent<ReadingScreen>("ReadingScreen");

            if (screen == null) { Debug.LogError("ReadingScreen not found"); return; }
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            // Fullscreen illustration
            var illustGo = new GameObject("Illustration", typeof(RectTransform));
            illustGo.transform.SetParent(root, false);
            Stretch(illustGo);
            var illustImg = illustGo.AddComponent<Image>();
            illustImg.color = BgColor;
            illustImg.preserveAspect = false;
            illustImg.raycastTarget = false;

            // EnvelopeParent = fill screen, crop edges (like CSS cover)
            var arf = illustGo.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = 9f / 16f; // default portrait; overridden at runtime

            // Content group (for page fade transitions)
            var contentGo = new GameObject("ContentGroup", typeof(RectTransform));
            contentGo.transform.SetParent(root, false);
            Stretch(contentGo);
            var contentCg = contentGo.AddComponent<CanvasGroup>();

            // Top bar
            var topBar = CreatePanel(contentGo.transform, "TopBar",
                new Vector2(0f, 0.92f), new Vector2(1f, 1f));

            var btnHome = CreateIconButton(topBar.transform, "BtnHome", "\u2190",
                new Vector2(0.02f, 0.1f), new Vector2(0.1f, 0.9f));
            var btnToc = CreateIconButton(topBar.transform, "BtnTOC", "\u2630",
                new Vector2(0.45f, 0.1f), new Vector2(0.55f, 0.9f));
            var btnMusic = CreateIconButton(topBar.transform, "BtnMusic", "\u266B",
                new Vector2(0.9f, 0.1f), new Vector2(0.98f, 0.9f));

            // Bottom text panel with scroll
            var textPanel = new GameObject("TextPanel", typeof(RectTransform));
            textPanel.transform.SetParent(contentGo.transform, false);
            Anchor(textPanel, new Vector2(0f, 0f), new Vector2(1f, 0.3f));
            textPanel.AddComponent<Image>().color = PanelColor;
            textPanel.AddComponent<RectMask2D>();

            var textScroll = textPanel.AddComponent<ScrollRect>();
            textScroll.horizontal = false;
            textScroll.vertical = true;

            var textContent = new GameObject("TextContent", typeof(RectTransform));
            textContent.transform.SetParent(textPanel.transform, false);
            var tcRt = textContent.GetComponent<RectTransform>();
            tcRt.anchorMin = new Vector2(0.08f, 1f);
            tcRt.anchorMax = new Vector2(0.92f, 1f);
            tcRt.pivot = new Vector2(0.5f, 1f);
            tcRt.offsetMin = Vector2.zero;
            tcRt.offsetMax = Vector2.zero;

            var tcFitter = textContent.AddComponent<ContentSizeFitter>();
            tcFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            textScroll.content = tcRt;

            var pageTextGo = new GameObject("PageText", typeof(RectTransform));
            pageTextGo.transform.SetParent(textContent.transform, false);
            Stretch(pageTextGo);
            var pageTmp = pageTextGo.AddComponent<TextMeshProUGUI>();
            pageTmp.text = "";
            pageTmp.fontSize = 22; // default, overridden by ReadingTextScaler
            pageTmp.alignment = TextAlignmentOptions.TopLeft;
            pageTmp.color = Color.white;
            pageTmp.enableWordWrapping = true;
            pageTmp.overflowMode = TextOverflowModes.Overflow;
            pageTmp.lineSpacing = 8f;

            // Nav arrows
            var btnPrev = CreateIconButton(contentGo.transform, "BtnPrev", "\u25C0",
                new Vector2(0.01f, 0.05f), new Vector2(0.1f, 0.2f));
            var btnNext = CreateIconButton(contentGo.transform, "BtnNext", "\u25B6",
                new Vector2(0.9f, 0.05f), new Vector2(0.99f, 0.2f));

            // TOC popup (child of ReadingScreen, initially hidden)
            var tocGo = CreateTocPopup(root);

            // PageNavigator component
            var nav = screen.gameObject.AddComponent<PageNavigator>();
            var navSo = new SerializedObject(nav);
            navSo.FindProperty("illustrationImage").objectReferenceValue = illustImg;
            navSo.FindProperty("pageText").objectReferenceValue = pageTmp;
            navSo.ApplyModifiedProperties();

            // ReadingTextScaler — auto font/panel sizing
            var scaler = screen.gameObject.AddComponent<ReadingTextScaler>();
            var scalerSo = new SerializedObject(scaler);
            scalerSo.FindProperty("pageText").objectReferenceValue = pageTmp;
            scalerSo.FindProperty("textPanel").objectReferenceValue =
                textPanel.GetComponent<RectTransform>();
            scalerSo.ApplyModifiedProperties();

            // Recording overlay panel (above top bar, initially hidden)
            var recOverlay = CreateRecordingOverlay(contentGo.transform);

            // Wire ReadingScreen (no slide — fullscreen illustration)
            var so = new SerializedObject(screen);
            so.FindProperty("slideFromBottom").boolValue = false;
            so.FindProperty("pageNavigator").objectReferenceValue = nav;
            so.FindProperty("btnHome").objectReferenceValue =
                btnHome.GetComponent<Button>();
            so.FindProperty("btnToc").objectReferenceValue =
                btnToc.GetComponent<Button>();
            so.FindProperty("btnMusic").objectReferenceValue =
                btnMusic.GetComponent<Button>();
            so.FindProperty("btnPrev").objectReferenceValue =
                btnPrev.GetComponent<Button>();
            so.FindProperty("btnNext").objectReferenceValue =
                btnNext.GetComponent<Button>();
            so.FindProperty("recordingOverlay").objectReferenceValue =
                recOverlay.GetComponent<RecordingOverlay>();
            so.ApplyModifiedProperties();
        }

        private static GameObject CreateRecordingOverlay(Transform parent)
        {
            var recColor = new Color(0.12f, 0.08f, 0.18f, 0.92f);
            var recBtnColor = new Color(0.85f, 0.25f, 0.25f);
            var recDoneDisabled = new Color(0.4f, 0.4f, 0.4f);

            var panel = new GameObject("RecordingPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Anchor(panel, new Vector2(0f, 0.84f), new Vector2(1f, 0.92f));
            panel.AddComponent<Image>().color = recColor;

            // Timer
            var timerGo = CreateTMP(panel.transform, "Timer", "00:00",
                20, TextAlignmentOptions.Center,
                new Vector2(0.02f, 0.1f), new Vector2(0.2f, 0.9f));

            // Status
            var statusGo = CreateTMP(panel.transform, "Status", "",
                16, TextAlignmentOptions.Center,
                new Vector2(0.2f, 0.1f), new Vector2(0.6f, 0.9f));

            // BtnRecord / Rerecord (single button, icon swaps)
            var btnRec = CreateIconButton(panel.transform, "BtnRecord", "\u23FA",
                new Vector2(0.62f, 0.1f), new Vector2(0.78f, 0.9f));
            btnRec.GetComponent<Image>().color = recBtnColor;
            var recIconTmp = btnRec.GetComponentInChildren<TMP_Text>();

            // BtnDone (enabled/disabled visual via Image color)
            var btnDone = CreateIconButton(panel.transform, "BtnDone", "\u2713",
                new Vector2(0.82f, 0.1f), new Vector2(0.98f, 0.9f));
            var doneImg = btnDone.GetComponent<Image>();
            doneImg.color = recDoneDisabled;

            // Add RecordingOverlay component
            var overlay = panel.AddComponent<RecordingOverlay>();
            var overlaySo = new SerializedObject(overlay);
            overlaySo.FindProperty("panel").objectReferenceValue = panel;
            overlaySo.FindProperty("timerText").objectReferenceValue =
                timerGo.GetComponent<TMP_Text>();
            overlaySo.FindProperty("statusText").objectReferenceValue =
                statusGo.GetComponent<TMP_Text>();
            overlaySo.FindProperty("btnRecord").objectReferenceValue =
                btnRec.GetComponent<Button>();
            overlaySo.FindProperty("recordIcon").objectReferenceValue = recIconTmp;
            overlaySo.FindProperty("btnDone").objectReferenceValue =
                btnDone.GetComponent<Button>();
            overlaySo.FindProperty("doneImage").objectReferenceValue = doneImg;
            overlaySo.ApplyModifiedProperties();

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateTocPopup(Transform root)
        {
            var go = new GameObject("TableOfContents", typeof(RectTransform));
            go.transform.SetParent(root, false);
            Stretch(go);

            // Dim background
            var dimGo = new GameObject("Dim", typeof(RectTransform));
            dimGo.transform.SetParent(go.transform, false);
            Stretch(dimGo);
            dimGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);

            // Panel
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            Anchor(panel, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.7f));
            panel.AddComponent<Image>().color = new Color(0.15f, 0.1f, 0.22f);

            // Title
            var titleGo = CreateTMP(panel.transform, "Title", "",
                28, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.8f), new Vector2(0.8f, 0.95f));

            // Close button
            var btnClose = CreateIconButton(panel.transform, "BtnClose", "\u2715",
                new Vector2(0.88f, 0.82f), new Vector2(0.97f, 0.97f));

            // Horizontal scroll for thumbnails
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            Anchor(scrollGo, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.78f));
            scrollGo.AddComponent<RectMask2D>();

            var scrollRect = scrollGo.AddComponent<ScrollRect>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(scrollGo.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(0, 1);
            contentRt.pivot = new Vector2(0, 0.5f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;

            // Wire TOC component
            var cg = go.AddComponent<CanvasGroup>();
            var toc = go.AddComponent<TableOfContentsPopup>();
            var tocSo = new SerializedObject(toc);
            tocSo.FindProperty("canvasGroup").objectReferenceValue = cg;
            tocSo.FindProperty("titleText").objectReferenceValue =
                titleGo.GetComponent<TMP_Text>();
            tocSo.FindProperty("btnClose").objectReferenceValue =
                btnClose.GetComponent<Button>();
            tocSo.FindProperty("thumbnailContainer").objectReferenceValue =
                content.transform;
            tocSo.ApplyModifiedProperties();

            go.SetActive(false);
            return go;
        }

        private static void CreateThumbnailPrefab()
        {
            var prefabPath = "Assets/Prefabs/UI/TocThumbnail.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssignThumbnailPrefab(prefabPath);
                return;
            }

            var go = new GameObject("TocThumbnail", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 160);

            var bg = go.AddComponent<Image>();
            bg.color = ThumbBgColor;
            go.AddComponent<Button>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 120;
            le.preferredHeight = 160;

            // Cover
            var coverGo = new GameObject("Cover", typeof(RectTransform));
            coverGo.transform.SetParent(go.transform, false);
            Anchor(coverGo, new Vector2(0.05f, 0.2f), new Vector2(0.95f, 0.95f));
            var coverImg = coverGo.AddComponent<Image>();
            coverImg.color = new Color(0.3f, 0.2f, 0.4f);
            coverImg.preserveAspect = true;

            // Label
            CreateTMP(go.transform, "Label", "1",
                16, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.18f));

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            AssignThumbnailPrefab(prefabPath);
            Debug.Log($"[ReadingSetup] Thumbnail prefab saved: {prefabPath}");
        }

        private static void AssignThumbnailPrefab(string prefabPath)
        {
            var toc = Object.FindAnyObjectByType<TableOfContentsPopup>(
                FindObjectsInactive.Include);
            if (toc == null) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            var so = new SerializedObject(toc);
            so.FindProperty("thumbnailPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
        }

        // ── Helpers ─────────────────────────────────────────

        private static T FindOrAddComponent<T>(string goName) where T : Component
        {
            var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var sm = Object.FindAnyObjectByType<ScreenManager>(
                FindObjectsInactive.Include);
            if (sm == null) return null;

            foreach (Transform child in sm.transform)
            {
                if (child.name != goName) continue;
                if (child.GetComponent<CanvasGroup>() == null)
                    child.gameObject.AddComponent<CanvasGroup>();
                var comp = child.gameObject.AddComponent<T>();
                Debug.Log($"[ReadingSetup] Added {typeof(T).Name} to {goName}");
                return comp;
            }

            return null;
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

        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
