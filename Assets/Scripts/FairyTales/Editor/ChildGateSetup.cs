using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Core;

namespace FairyTales.Editor
{
    public static class ChildGateSetup
    {
        private static readonly Color PopupBg = new(0.28f, 0.18f, 0.52f);
        private static readonly Color InputBg = new(0.38f, 0.28f, 0.62f);
        private static readonly Color KeyBg = new(0.22f, 0.15f, 0.38f);
        private static readonly Color KeyTextColor = Color.white;
        private static readonly Color DimColor = new(0, 0, 0, 0.7f);
        private static readonly Color CloseBtnColor = new(0.55f, 0.36f, 0.85f);

        [MenuItem("FairyTales/Setup Child Gate")]
        public static void Setup()
        {
            var existing = Object.FindAnyObjectByType<ChildGatePopup>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[ChildGateSetup] Already exists");
                return;
            }

            var sm = Object.FindAnyObjectByType<ScreenManager>(
                FindObjectsInactive.Include);
            if (sm == null)
            {
                Debug.LogError("[ChildGateSetup] ScreenManager not found");
                return;
            }

            var root = CreateRoot(sm.transform);
            Debug.Log("[ChildGateSetup] Done!");
        }

        private static GameObject CreateRoot(Transform parent)
        {
            var go = new GameObject("ChildGatePopup", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            Stretch(go);

            var cg = go.AddComponent<CanvasGroup>();

            // Dim
            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.transform.SetParent(go.transform, false);
            Stretch(dim);
            dim.AddComponent<Image>().color = DimColor;

            // Panel
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(go.transform, false);
            Anchor(panel, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.7f));
            panel.AddComponent<Image>().color = PopupBg;

            // Close button (top right)
            var btnClose = CreateButton(panel.transform, "BtnClose", "\u2715", 24,
                new Vector2(0.88f, 0.82f), new Vector2(0.97f, 0.97f), CloseBtnColor);

            // Title "Решите пример"
            var title = CreateTMP(panel.transform, "Title", "Решите пример",
                22, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.75f), new Vector2(0.85f, 0.95f));

            // Problem text "24+32=?"
            var problem = CreateTMP(panel.transform, "ProblemText", "24+32=?",
                28, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.58f), new Vector2(0.5f, 0.78f));

            // Input field area
            var inputBg = new GameObject("InputBg", typeof(RectTransform));
            inputBg.transform.SetParent(panel.transform, false);
            Anchor(inputBg, new Vector2(0.05f, 0.4f), new Vector2(0.5f, 0.58f));
            inputBg.AddComponent<Image>().color = InputBg;

            var placeholder = CreateTMP(inputBg.transform, "Placeholder",
                "секретный код", 16, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
            placeholder.GetComponent<TMP_Text>().color =
                new Color(1, 1, 1, 0.5f);

            var inputText = CreateTMP(inputBg.transform, "InputText",
                "", 24, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);

            // Numpad (right side) — 4 rows: [1 2 3] [4 5 6] [7 8 9] [- 0 ⌫]
            var numpad = new GameObject("Numpad", typeof(RectTransform));
            numpad.transform.SetParent(panel.transform, false);
            Anchor(numpad, new Vector2(0.52f, 0.05f), new Vector2(0.97f, 0.95f));

            var grid = numpad.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.spacing = new Vector2(6, 6);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.cellSize = new Vector2(60, 50);
            grid.childAlignment = TextAnchor.MiddleCenter;

            // Buttons array: 1-9, then spacer, 0, backspace
            var buttons = new Button[10];
            for (int i = 1; i <= 9; i++)
            {
                var btn = CreateKeyButton(numpad.transform, i.ToString());
                buttons[i] = btn.GetComponent<Button>();
            }

            // Empty spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(numpad.transform, false);

            // 0
            var btn0 = CreateKeyButton(numpad.transform, "0");
            buttons[0] = btn0.GetComponent<Button>();

            // Backspace
            var btnBs = CreateKeyButton(numpad.transform, "\u232B");
            btnBs.GetComponent<Image>().color = CloseBtnColor;

            // Wire ChildGatePopup
            var popup = go.AddComponent<ChildGatePopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("problemText").objectReferenceValue =
                problem.GetComponent<TMP_Text>();
            so.FindProperty("inputText").objectReferenceValue =
                inputText.GetComponent<TMP_Text>();
            so.FindProperty("placeholderText").objectReferenceValue =
                placeholder.GetComponent<TMP_Text>();
            so.FindProperty("btnClose").objectReferenceValue =
                btnClose.GetComponent<Button>();
            so.FindProperty("btnBackspace").objectReferenceValue =
                btnBs.GetComponent<Button>();

            var numBtnsProp = so.FindProperty("numButtons");
            numBtnsProp.arraySize = 10;
            for (int i = 0; i < 10; i++)
                numBtnsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];

            so.ApplyModifiedProperties();

            go.SetActive(false);
            return go;
        }

        private static GameObject CreateKeyButton(Transform parent, string label)
        {
            var go = new GameObject($"Key_{label}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = KeyBg;
            go.AddComponent<Button>();

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = KeyTextColor;

            return go;
        }

        private static GameObject CreateButton(Transform parent, string name,
            string icon, int fontSize, Vector2 anchorMin, Vector2 anchorMax,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            go.AddComponent<Image>().color = color;
            go.AddComponent<Button>();

            var textGo = new GameObject("Icon", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = icon;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

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
