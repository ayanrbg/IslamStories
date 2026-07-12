using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Onboarding;

namespace FairyTales.Editor
{
    public static class OnboardingSetup
    {
        private static readonly Color BtnColor = new(0.55f, 0.36f, 0.85f);
        private static readonly Color BtnSelectedColor = new(0.72f, 0.52f, 1f);
        private static readonly Color TextColor = Color.white;

        [MenuItem("FairyTales/Setup Onboarding UI")]
        public static void Setup()
        {
            SetupLanguageSelect();
            SetupPersonalization();
            SetupLoading();
            Debug.Log("[OnboardingSetup] Done!");
        }

        // ── LanguageSelectScreen ─────────────────────────────
        private static void SetupLanguageSelect()
        {
            var screen = Object.FindAnyObjectByType<LanguageSelectScreen>(
                FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogError("LanguageSelectScreen not found");
                return;
            }
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            var title = CreateTMP(root, "Title", "Выберите язык",
                36, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.85f));

            var btnRu = CreateButton(root, "BtnRussian", "Русский",
                new Vector2(0.15f, 0.58f), new Vector2(0.85f, 0.66f));
            var selRu = CreateSelection(btnRu.transform);

            var btnKz = CreateButton(root, "BtnKazakh", "Қазақ",
                new Vector2(0.15f, 0.48f), new Vector2(0.85f, 0.56f));
            var selKz = CreateSelection(btnKz.transform);

            var btnEn = CreateButton(root, "BtnEnglish", "English",
                new Vector2(0.15f, 0.38f), new Vector2(0.85f, 0.46f));
            var selEn = CreateSelection(btnEn.transform);

            var btnCont = CreateButton(root, "BtnContinue", "Продолжить",
                new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.23f));

            var so = new SerializedObject(screen);
            so.FindProperty("btnRussian").objectReferenceValue = btnRu;
            so.FindProperty("btnKazakh").objectReferenceValue = btnKz;
            so.FindProperty("btnEnglish").objectReferenceValue = btnEn;
            so.FindProperty("btnContinue").objectReferenceValue = btnCont;
            so.FindProperty("selectedRu").objectReferenceValue = selRu;
            so.FindProperty("selectedKz").objectReferenceValue = selKz;
            so.FindProperty("selectedEn").objectReferenceValue = selEn;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(screen.gameObject,
                "Setup LanguageSelect");
        }

        // ── PersonalizationScreen ────────────────────────────
        private static void SetupPersonalization()
        {
            var screen = Object.FindAnyObjectByType<PersonalizationScreen>(
                FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogError("PersonalizationScreen not found");
                return;
            }
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            CreateTMP(root, "Title", "Как зовут ребёнка?",
                36, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.85f));

            var nameInput = CreateInputField(root, "NameInput", "Введите имя",
                new Vector2(0.1f, 0.6f), new Vector2(0.9f, 0.68f));

            CreateTMP(root, "GenderLabel", "Пол",
                28, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.57f));

            var btnBoy = CreateButton(root, "BtnBoy", "Мальчик",
                new Vector2(0.1f, 0.4f), new Vector2(0.48f, 0.48f));
            var selBoy = CreateSelection(btnBoy.transform);

            var btnGirl = CreateButton(root, "BtnGirl", "Девочка",
                new Vector2(0.52f, 0.4f), new Vector2(0.9f, 0.48f));
            var selGirl = CreateSelection(btnGirl.transform);

            var btnCont = CreateButton(root, "BtnContinue", "Продолжить",
                new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.23f));

            var btnLang = CreateButton(root, "BtnChangeLang", "🌐",
                new Vector2(0.02f, 0.92f), new Vector2(0.12f, 0.97f));

            var so = new SerializedObject(screen);
            so.FindProperty("nameInput").objectReferenceValue = nameInput;
            so.FindProperty("btnBoy").objectReferenceValue = btnBoy;
            so.FindProperty("btnGirl").objectReferenceValue = btnGirl;
            so.FindProperty("btnContinue").objectReferenceValue = btnCont;
            so.FindProperty("btnChangeLang").objectReferenceValue = btnLang;
            so.FindProperty("selectedBoy").objectReferenceValue = selBoy;
            so.FindProperty("selectedGirl").objectReferenceValue = selGirl;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(screen.gameObject,
                "Setup Personalization");
        }

        // ── LoadingScreen ────────────────────────────────────
        private static void SetupLoading()
        {
            var screen = Object.FindAnyObjectByType<LoadingScreen>(
                FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogError("LoadingScreen not found");
                return;
            }
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            var statusText = CreateTMP(root, "StatusText",
                "Подготавливаем библиотеку...",
                30, TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.63f));

            var slider = CreateSlider(root, "ProgressBar",
                new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.46f));

            var so = new SerializedObject(screen);
            so.FindProperty("statusText").objectReferenceValue =
                statusText.GetComponent<TMP_Text>();
            so.FindProperty("progressBar").objectReferenceValue = slider;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(screen.gameObject,
                "Setup Loading");
        }

        // ── UI Helpers ───────────────────────────────────────

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
            tmp.color = TextColor;
            return go;
        }

        private static Button CreateButton(Transform parent, string name,
            string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);

            var img = go.AddComponent<Image>();
            img.color = BtnColor;

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
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = TextColor;

            return btn;
        }

        private static GameObject CreateSelection(Transform btnParent)
        {
            var go = new GameObject("Selection", typeof(RectTransform));
            go.transform.SetParent(btnParent, false);
            var rt = Stretch(go);
            rt.offsetMin = new Vector2(-4, -4);
            rt.offsetMax = new Vector2(4, 4);

            var outline = go.AddComponent<Outline>();

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0);
            outline.effectColor = BtnSelectedColor;
            outline.effectDistance = new Vector2(3, 3);

            go.SetActive(false);
            return go;
        }

        private static TMP_InputField CreateInputField(Transform parent,
            string name, string placeholder,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.1f);

            // Text area
            var textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            var areaRt = Stretch(textArea);
            areaRt.offsetMin = new Vector2(16, 4);
            areaRt.offsetMax = new Vector2(-16, -4);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(textArea.transform, false);
            Stretch(phGo);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 26;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color = new Color(1, 1, 1, 0.4f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Input text
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(textArea.transform, false);
            Stretch(txtGo);
            var txtTmp = txtGo.AddComponent<TextMeshProUGUI>();
            txtTmp.fontSize = 26;
            txtTmp.color = TextColor;
            txtTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = areaRt;
            input.textComponent = txtTmp;
            input.placeholder = phTmp;
            input.fontAsset = txtTmp.font;

            return input;
        }

        private static Slider CreateSlider(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);

            // Background
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(go.transform, false);
            Stretch(bgGo);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(1, 1, 1, 0.15f);

            // Fill area
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch(fillArea);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillArea.transform, false);
            Stretch(fillGo);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = BtnSelectedColor;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.handleRect = null;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 0;
            slider.interactable = false;

            return slider;
        }

        // ── RectTransform utils ──────────────────────────────

        private static RectTransform Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>()
                     ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform Anchor(GameObject go,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = go.GetComponent<RectTransform>()
                     ?? go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}
