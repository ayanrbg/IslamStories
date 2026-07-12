using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.UI.Core;
using FairyTales.UI.Payment;

namespace FairyTales.Editor
{
    public static class PaymentSetup
    {
        private static readonly Color BtnColor = new(0.55f, 0.36f, 0.85f);
        private static readonly Color CardColor = new(0.22f, 0.14f, 0.38f);
        private static readonly Color CardBorder = new(0.55f, 0.36f, 0.85f);
        private static readonly Color CtaColor = new(0.95f, 0.75f, 0.15f);
        private static readonly Color BadgeColor = new(0.9f, 0.25f, 0.2f);
        private static readonly Color OldPriceColor = new(0.7f, 0.5f, 0.5f);
        private static readonly Color LinkColor = new(0.65f, 0.55f, 0.85f);

        [MenuItem("FairyTales/Setup Payment UI")]
        public static void Setup()
        {
            SetupPaymentScreen();
            Debug.Log("[PaymentSetup] Done!");
        }

        private static void SetupPaymentScreen()
        {
            var screen = Object.FindAnyObjectByType<PaymentScreen>(
                FindObjectsInactive.Include);

            if (screen == null)
            {
                var sm = Object.FindAnyObjectByType<ScreenManager>(
                    FindObjectsInactive.Include);
                if (sm == null)
                {
                    Debug.LogError("ScreenManager not found");
                    return;
                }

                foreach (Transform child in sm.transform)
                {
                    if (child.name != "PaymentScreen") continue;
                    if (child.GetComponent<CanvasGroup>() == null)
                        child.gameObject.AddComponent<CanvasGroup>();
                    screen = child.gameObject.AddComponent<PaymentScreen>();
                    break;
                }
            }

            if (screen == null)
            {
                Debug.LogError("PaymentScreen not found");
                return;
            }
            if (screen.transform.childCount > 0) return;

            var root = screen.transform;

            // ── Title ──
            CreateTMP(root, "Title", "Получите доступ ко всем сказкам!",
                36, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.78f), new Vector2(0.85f, 0.9f));

            // ── Close button ──
            var btnClose = CreateCloseButton(root,
                new Vector2(0.85f, 0.87f), new Vector2(0.96f, 0.95f));

            // ── Plan cards container ──
            var cards = CreatePanel(root, "Plans",
                new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.76f));

            // Monthly card
            var monthly = CreatePlanCard(cards.transform, "CardMonthly",
                new Vector2(0f, 0f), new Vector2(0.47f, 1f),
                "449 ₽/мес", null, null);

            // Yearly card
            var yearly = CreatePlanCard(cards.transform, "CardYearly",
                new Vector2(0.53f, 0f), new Vector2(1f, 1f),
                "2 490 ₽/год", "5388 ₽/год", "-55%");

            // Selection outlines
            var monthlySelect = CreateSelectionBorder(
                monthly.transform, "Selection");
            var yearlySelect = CreateSelectionBorder(
                yearly.transform, "Selection");

            // ── CTA button ──
            var btnTrial = CreateCtaButton(root, "BtnTrial",
                "Попробовать 3 дня бесплатно",
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.39f));

            // ── Bottom links ──
            var btnTerms = CreateLinkButton(root, "BtnTerms",
                "Условия использования",
                new Vector2(0.02f, 0.03f), new Vector2(0.34f, 0.1f));
            var btnRestore = CreateLinkButton(root, "BtnRestore",
                "Восстановить покупку",
                new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.1f));
            var btnPrivacy = CreateLinkButton(root, "BtnPrivacy",
                "Политика конфиденциальности",
                new Vector2(0.66f, 0.03f), new Vector2(0.98f, 0.1f));

            // ── Wire fields ──
            var so = new SerializedObject(screen);
            so.FindProperty("btnClose").objectReferenceValue =
                btnClose.GetComponent<Button>();
            so.FindProperty("btnMonthly").objectReferenceValue =
                monthly.GetComponent<Button>();
            so.FindProperty("btnYearly").objectReferenceValue =
                yearly.GetComponent<Button>();
            so.FindProperty("btnTrial").objectReferenceValue =
                btnTrial.GetComponent<Button>();
            so.FindProperty("btnTerms").objectReferenceValue =
                btnTerms.GetComponent<Button>();
            so.FindProperty("btnRestore").objectReferenceValue =
                btnRestore.GetComponent<Button>();
            so.FindProperty("btnPrivacy").objectReferenceValue =
                btnPrivacy.GetComponent<Button>();
            so.FindProperty("monthlySelect").objectReferenceValue = monthlySelect;
            so.FindProperty("yearlySelect").objectReferenceValue = yearlySelect;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(screen.gameObject,
                "Setup PaymentScreen");
        }

        // ── Plan card ──────────────────────────────────────────

        private static GameObject CreatePlanCard(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string price, string oldPrice, string badge)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);

            var img = go.AddComponent<Image>();
            img.color = CardColor;
            go.AddComponent<Button>();

            // Star icon placeholder
            CreateTMP(go.transform, "Star", "★",
                32, FontStyles.Normal, TextAlignmentOptions.Center,
                new Vector2(0.35f, 0.7f), new Vector2(0.65f, 0.92f));

            // Price
            CreateTMP(go.transform, "Price", price,
                28, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.6f));

            // Old price (strikethrough)
            if (!string.IsNullOrEmpty(oldPrice))
            {
                var oldGo = CreateTMP(go.transform, "OldPrice",
                    $"<s>{oldPrice}</s>",
                    20, FontStyles.Normal, TextAlignmentOptions.Center,
                    new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.3f));
                oldGo.GetComponent<TextMeshProUGUI>().color = OldPriceColor;
                oldGo.GetComponent<TextMeshProUGUI>().richText = true;
            }

            // Discount badge
            if (!string.IsNullOrEmpty(badge))
            {
                var badgeGo = new GameObject("Badge", typeof(RectTransform));
                badgeGo.transform.SetParent(go.transform, false);
                Anchor(badgeGo, new Vector2(0.7f, 0.8f), new Vector2(1.05f, 1.0f));
                badgeGo.AddComponent<Image>().color = BadgeColor;

                var badgeText = new GameObject("Text", typeof(RectTransform));
                badgeText.transform.SetParent(badgeGo.transform, false);
                Stretch(badgeText);
                var tmp = badgeText.AddComponent<TextMeshProUGUI>();
                tmp.text = badge;
                tmp.fontSize = 18;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }

            return go;
        }

        private static GameObject CreateSelectionBorder(
            Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = Stretch(go);
            rt.offsetMin = new Vector2(-3, -3);
            rt.offsetMax = new Vector2(3, 3);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = CardBorder;
            outline.effectDistance = new Vector2(3, 3);

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0);

            go.SetActive(false);
            return go;
        }

        // ── CTA button ─────────────────────────────────────────

        private static GameObject CreateCtaButton(Transform parent, string name,
            string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);

            go.AddComponent<Image>().color = CtaColor;
            go.AddComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 28;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.15f, 0.1f, 0.2f);

            return go;
        }

        // ── Close button ────────────────────────────────────────

        private static GameObject CreateCloseButton(Transform parent,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("BtnClose", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);

            go.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 0.9f);
            go.AddComponent<Button>();

            var textGo = new GameObject("Icon", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "\u2715";
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return go;
        }

        // ── Link button ─────────────────────────────────────────

        private static GameObject CreateLinkButton(Transform parent, string name,
            string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);

            go.AddComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            Stretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = LinkColor;

            return go;
        }

        // ── Helpers ─────────────────────────────────────────────

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            return go;
        }

        private static GameObject CreateTMP(Transform parent, string name,
            string text, int fontSize, FontStyles style,
            TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = Color.white;
            return go;
        }

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
