using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Core;
using FairyTales.UI.Library;

namespace FairyTales.Editor
{
    public static class LibrarySetup
    {
        private static readonly Color BtnColor = new(0.55f, 0.36f, 0.85f);
        private static readonly Color BtnSelectedColor = new(0.72f, 0.52f, 1f);
        private static readonly Color CardBgColor = new(0.2f, 0.14f, 0.3f);

        [MenuItem("FairyTales/Setup Library UI")]
        public static void Setup()
        {
            SetupLibraryScreen();
            SetupTaleDetailScreen();
            CreateCardPrefab();
            Debug.Log("[LibrarySetup] Done!");
        }

        private static void SetupLibraryScreen()
        {
            var screen = Object.FindAnyObjectByType<LibraryScreen>(
                FindObjectsInactive.Include);

            if (screen == null)
                screen = FindOrAddComponent<LibraryScreen>("LibraryScreen");

            if (screen == null) { Debug.LogError("LibraryScreen not found"); return; }
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            // Top bar
            var topBar = CreatePanel(root, "TopBar",
                new Vector2(0f, 0.9f), new Vector2(1f, 1f));

            CreateIconButton(topBar.transform, "BtnSettings", "\u2699",
                new Vector2(0.02f, 0.1f), new Vector2(0.1f, 0.9f));
            CreateIconButton(topBar.transform, "BtnMail", "\u2709",
                new Vector2(0.11f, 0.1f), new Vector2(0.19f, 0.9f));

            var btnUnlock = CreateButton(topBar.transform, "BtnUnlockAll",
                "Разблокировать все книги",
                new Vector2(0.25f, 0.15f), new Vector2(0.75f, 0.85f));

            var btnMusic = CreateIconButton(topBar.transform, "BtnMusic", "\u266B",
                new Vector2(0.9f, 0.1f), new Vector2(0.98f, 0.9f));

            // Scroll view with grid
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(root, false);
            Anchor(scrollGo, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.89f));

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

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(240, 300);
            grid.spacing = new Vector2(16, 16);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var so = new SerializedObject(screen);
            so.FindProperty("cardContainer").objectReferenceValue = content.transform;
            so.FindProperty("btnSettings").objectReferenceValue =
                topBar.transform.Find("BtnSettings")?.GetComponent<Button>();
            so.FindProperty("btnMusic").objectReferenceValue =
                btnMusic.GetComponent<Button>();
            so.FindProperty("btnUnlockAll").objectReferenceValue = btnUnlock;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(screen.gameObject, "Setup LibraryScreen");
        }

        private static void SetupTaleDetailScreen()
        {
            var screen = Object.FindAnyObjectByType<TaleDetailScreen>(
                FindObjectsInactive.Include);

            if (screen == null)
                screen = FindOrAddComponent<TaleDetailScreen>("TaleDetailScreen");

            if (screen == null) { Debug.LogError("TaleDetailScreen not found"); return; }
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            // Back button
            var btnBack = CreateIconButton(root, "BtnBack", "\u2190",
                new Vector2(0.02f, 0.92f), new Vector2(0.1f, 0.98f));

            // Cover image
            var coverGo = new GameObject("Cover", typeof(RectTransform));
            coverGo.transform.SetParent(root, false);
            Anchor(coverGo, new Vector2(0.25f, 0.45f), new Vector2(0.75f, 0.88f));
            var coverImg = coverGo.AddComponent<Image>();
            coverImg.color = CardBgColor;
            coverImg.preserveAspect = true;

            // Title
            var titleGo = CreateTMP(root, "Title", "Название сказки",
                32, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.44f));

            // Page count
            var pagesGo = CreateTMP(root, "PageCount", "",
                22, TextAlignmentOptions.Center,
                new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.36f));

            // Buttons
            var btnRead = CreateButton(root, "BtnRead", "Читать",
                new Vector2(0.1f, 0.18f), new Vector2(0.36f, 0.27f));
            var btnListen = CreateButton(root, "BtnListen", "Слушать",
                new Vector2(0.38f, 0.18f), new Vector2(0.62f, 0.27f));
            var btnNarrate = CreateButton(root, "BtnNarrate", "Озвучить",
                new Vector2(0.64f, 0.18f), new Vector2(0.9f, 0.27f));

            var so = new SerializedObject(screen);
            so.FindProperty("coverImage").objectReferenceValue = coverImg;
            so.FindProperty("titleText").objectReferenceValue =
                titleGo.GetComponent<TMP_Text>();
            so.FindProperty("pageCountText").objectReferenceValue =
                pagesGo.GetComponent<TMP_Text>();
            so.FindProperty("btnRead").objectReferenceValue = btnRead;
            so.FindProperty("btnListen").objectReferenceValue = btnListen;
            so.FindProperty("btnNarrate").objectReferenceValue = btnNarrate;
            so.FindProperty("btnBack").objectReferenceValue =
                btnBack.GetComponent<Button>();
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(screen.gameObject, "Setup TaleDetailScreen");
        }

        private static void CreateCardPrefab()
        {
            var prefabPath = "Assets/Prefabs/UI/TaleCard.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssignCardPrefab(prefabPath);
                return;
            }

            var go = new GameObject("TaleCard", typeof(RectTransform));

            var bg = go.AddComponent<Image>();
            bg.color = CardBgColor;
            var btn = go.AddComponent<Button>();

            // Cover
            var coverGo = new GameObject("Cover", typeof(RectTransform));
            coverGo.transform.SetParent(go.transform, false);
            Anchor(coverGo, new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.95f));
            var coverImg = coverGo.AddComponent<Image>();
            coverImg.color = new Color(0.3f, 0.2f, 0.4f);
            coverImg.preserveAspect = true;

            // Title
            var titleGo = CreateTMP(go.transform, "Title", "Title",
                18, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.22f));

            // Lock icon
            var lockGo = new GameObject("LockIcon", typeof(RectTransform));
            lockGo.transform.SetParent(go.transform, false);
            Anchor(lockGo, new Vector2(0.75f, 0.75f), new Vector2(0.95f, 0.95f));
            var lockImg = lockGo.AddComponent<Image>();
            lockImg.color = Color.white;
            lockGo.SetActive(false);

            var card = go.AddComponent<TaleCard>();
            var so = new SerializedObject(card);
            so.FindProperty("coverImage").objectReferenceValue = coverImg;
            so.FindProperty("titleText").objectReferenceValue =
                titleGo.GetComponent<TMP_Text>();
            so.FindProperty("lockIcon").objectReferenceValue = lockGo;
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedProperties();

            // Save prefab
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            AssignCardPrefab(prefabPath);
            Debug.Log($"[LibrarySetup] Card prefab saved: {prefabPath}");
        }

        private static void AssignCardPrefab(string prefabPath)
        {
            var screen = Object.FindAnyObjectByType<LibraryScreen>(
                FindObjectsInactive.Include);
            if (screen == null) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            var so = new SerializedObject(screen);
            so.FindProperty("cardPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
        }

        // ── UI Helpers ───────────────────────────────────────

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
            var colors = btn.colors;
            colors.highlightedColor = BtnSelectedColor;
            colors.pressedColor = BtnSelectedColor;
            btn.colors = colors;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
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

        private static T FindOrAddComponent<T>(string goName)
            where T : Component
        {
            // First try FindAnyObjectByType (works with inactive)
            var existing = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            // Fallback: search by name under ScreenManager
            var sm = Object.FindAnyObjectByType<ScreenManager>(
                FindObjectsInactive.Include);
            if (sm == null) return null;

            GameObject go = null;
            foreach (Transform child in sm.transform)
            {
                if (child.name == goName)
                {
                    go = child.gameObject;
                    break;
                }
            }

            if (go == null) return null;

            if (go.GetComponent<CanvasGroup>() == null)
                go.AddComponent<CanvasGroup>();
            var comp = go.AddComponent<T>();
            Debug.Log($"[LibrarySetup] Added {typeof(T).Name} to {goName}");
            return comp;
        }
    }
}
