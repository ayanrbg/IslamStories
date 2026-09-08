using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using FairyTales.UI.Payment;

namespace FairyTales.Editor
{
    /// <summary>
    /// Builds the "book as a gift" UI: the GiftPopup under [UICanvas] and the gift badge
    /// inside the TaleCard prefab. Sprites and fonts are copied from the existing popups
    /// so the result matches the app before a single art asset is replaced.
    ///
    /// Idempotent: run it again and it only fills in what is missing — an existing
    /// GiftPopup (with your own sprites) is left alone and just re-wired.
    /// </summary>
    public static class GiftUISetup
    {
        private const string CardPrefabPath = "Assets/Prefabs/UI/TaleCard.prefab";

        [MenuItem("FairyTales/Setup Gift UI")]
        public static void Setup()
        {
            SetupPopup();
            SetupPromoButtonLabel();
            SetupCardBadge();
            Debug.Log("[GiftUISetup] Done. Save the scene to keep the popup.");
        }

        // ── Popup ─────────────────────────────────────────
        private static void SetupPopup()
        {
            var sm = Object.FindAnyObjectByType<ScreenManager>();
            if (sm == null)
            {
                Debug.LogError("[GiftUISetup] No ScreenManager in the scene — is SampleScene open?");
                return;
            }

            var canvas = sm.transform;
            var existing = FindChild(canvas, "GiftPopup");
            var root = existing != null ? existing.gameObject : NewUI("GiftPopup", canvas);

            // Sizes and positions are written ONCE, when the popup is first created. A popup
            // that is already in the scene may have been laid out by hand; a second run only
            // re-wires the references and refreshes the texts, and must not undo that work.
            var layout = existing == null;

            var cg = Ensure<CanvasGroup>(root);
            var popup = Ensure<GiftPopup>(root);
            if (layout) Stretch(root.GetComponent<RectTransform>());

            // The object must stay ACTIVE — GiftPopup.Awake is what registers the instance
            // Show() talks to, and it deactivates itself from there. Invisible and click-through
            // in the meantime, so it does not sit on top of the library in the Scene view.
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            // Below Toast and the parental gate: a "code copied" toast has to land on top
            // of the popup, not behind it.
            var toast = FindChild(canvas, "Toast");
            if (layout)
                root.transform.SetSiblingIndex(toast != null ? toast.GetSiblingIndex() : canvas.childCount - 1);

            // Style donors — an existing popup in the scene, so nothing is hardcoded.
            var gatePanel = FindByPath(canvas, "ChildGatePopup/Panel");
            var gateClose = FindByPath(canvas, "ChildGatePopup/Panel/BtnClose");
            var gateInput = FindByPath(canvas, "ChildGatePopup/Panel/InputBg");
            var gateTitle = FindByPath(canvas, "ChildGatePopup/Panel/Title");
            var promoApply = FindByPath(canvas, "PaymentScreen/promo panel/cont/btn confirm");

            var titleFont = FontOf(gateTitle);
            var titleMat = MaterialOf(gateTitle);

            var dim = FindChild(root.transform, "Dim") ?? NewUI("Dim", root.transform).transform;
            var dimImg = Ensure<Image>(dim.gameObject);
            if (layout) Stretch(dim as RectTransform);
            if (dimImg.sprite == null) dimImg.color = new Color(0f, 0f, 0f, 0.6f);

            var panel = FindChild(root.transform, "Panel") ?? NewUI("Panel", root.transform).transform;
            var panelRt = (RectTransform)panel;
            var panelImg = Ensure<Image>(panel.gameObject);
            CopyImage(gatePanel, panelImg);
            if (layout) Center(panelRt, new Vector2(900f, 1040f));

            // Close (top-right corner of the panel)
            var close = FindChild(panel, "BtnClose") ?? NewUI("BtnClose", panel).transform;
            var closeRt = (RectTransform)close;
            var closeImg = Ensure<Image>(close.gameObject);
            CopyImage(gateClose, closeImg);
            Ensure<Button>(close.gameObject);
            Ensure<ButtonScaleEffect>(close.gameObject);
            if (layout)
            {
                closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(0.5f, 0.5f);
                closeRt.sizeDelta = new Vector2(130f, 130f);
                closeRt.anchoredPosition = new Vector2(-10f, -10f);
            }

            var title = Text(panel, "Title", titleFont, titleMat, 70f, Loc.Get("gift_title"), layout);
            if (layout) TopStretch(title.rectTransform, top: 70f, height: 160f, margin: 60f);

            var instruction = Text(panel, "Instruction", titleFont, titleMat, 38f, GiftPopup.GiftInstruction(), layout);
            if (layout) TopStretch(instruction.rectTransform, top: 250f, height: 330f, margin: 60f);

            var caption = Text(panel, "CodeCaption", titleFont, titleMat, 36f, Loc.Get("gift_your_code"), layout);
            if (layout) TopStretch(caption.rectTransform, top: 600f, height: 50f, margin: 60f);

            // Code box
            var box = FindChild(panel, "CodeBox") ?? NewUI("CodeBox", panel).transform;
            var boxRt = (RectTransform)box;
            var boxImg = Ensure<Image>(box.gameObject);
            CopyImage(gateInput, boxImg);
            if (layout)
            {
                boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 1f);
                boxRt.pivot = new Vector2(0.5f, 1f);
                boxRt.sizeDelta = new Vector2(520f, 140f);
                boxRt.anchoredPosition = new Vector2(0f, -660f);
            }

            var code = Text(box, "CodeText", titleFont, titleMat, 84f, "-----", layout);
            if (layout)
            {
                Stretch(code.rectTransform);
                code.characterSpacing = 12f;
                // The code sits on the light input plate — white on white is unreadable.
                code.color = new Color32(0x3D, 0x1E, 0x73, 0xFF);
            }

            // Copy button
            var copy = FindChild(panel, "BtnCopy") ?? NewUI("BtnCopy", panel).transform;
            var copyRt = (RectTransform)copy;
            var copyImg = Ensure<Image>(copy.gameObject);
            CopyImage(promoApply, copyImg);
            Ensure<Button>(copy.gameObject);
            Ensure<ButtonScaleEffect>(copy.gameObject);
            if (layout)
            {
                copyRt.anchorMin = copyRt.anchorMax = new Vector2(0.5f, 1f);
                copyRt.pivot = new Vector2(0.5f, 1f);
                copyRt.sizeDelta = new Vector2(460f, 130f);
                copyRt.anchoredPosition = new Vector2(0f, -830f);
            }

            var copyLabel = Text(copy, "Label", titleFont, titleMat, 46f, Loc.Get("copy"), layout);
            if (layout) Stretch(copyLabel.rectTransform);

            var so = new SerializedObject(popup);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("txtTitle").objectReferenceValue = title;
            so.FindProperty("txtInstruction").objectReferenceValue = instruction;
            so.FindProperty("txtCodeCaption").objectReferenceValue = caption;
            so.FindProperty("txtCode").objectReferenceValue = code;
            so.FindProperty("btnCopy").objectReferenceValue = copy.GetComponent<Button>();
            so.FindProperty("txtCopyLabel").objectReferenceValue = copyLabel;
            so.FindProperty("btnClose").objectReferenceValue = close.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        // ── "Промокод" caption under the paywall's promo icon ──
        /// <summary>
        /// The gift instruction tells the friend to tap a button called "Промокод", but the
        /// paywall shows an unlabelled icon. Adds the caption so the two agree; the text is
        /// the same Loc key the instruction substitutes, in every language.
        /// </summary>
        private static void SetupPromoButtonLabel()
        {
            var payment = Object.FindAnyObjectByType<PaymentScreen>(FindObjectsInactive.Include);
            if (payment == null) return;

            var so = new SerializedObject(payment);
            var btn = so.FindProperty("btnPromo").objectReferenceValue as Button;
            if (btn == null) return;

            var fresh = FindChild(btn.transform, "Label") == null;
            var donor = FindByPath(payment.transform, "cont/GameObject (1)/GameObject/BtnPrivacy (1)");
            var label = Text(btn.transform, "Label", FontOf(donor), MaterialOf(donor),
                26f, Loc.Get("promo_code"), fresh);

            if (!fresh) return; // placed once; a hand-moved caption stays where it was put

            // Just under the icon, same width — the icon sits in the top-right corner.
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(200f, 36f);
            rt.anchoredPosition = new Vector2(0f, -6f);
        }

        // ── Gift badge inside the TaleCard prefab ─────────
        private static void SetupCardBadge()
        {
            var contents = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                var card = contents.GetComponent<TaleCard>();
                if (card == null)
                {
                    Debug.LogError("[GiftUISetup] TaleCard component missing on the prefab.");
                    return;
                }

                var badge = FindChild(contents.transform, "GiftBadge");
                var layout = badge == null; // same rule as the popup: place it once, never re-place
                if (badge == null) badge = NewUI("GiftBadge", contents.transform).transform;

                var badgeRt = (RectTransform)badge;
                var badgeImg = Ensure<Image>(badge.gameObject);
                if (badgeImg.sprite == null) badgeImg.color = new Color32(0xF5, 0xA6, 0x23, 0xFF);
                if (layout)
                {
                    badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0.5f, 1f);
                    badgeRt.pivot = new Vector2(0.5f, 1f);
                    badgeRt.sizeDelta = new Vector2(300f, 72f);
                    badgeRt.anchoredPosition = new Vector2(0f, -16f);
                }

                // Font donor: the coming-soon label, the badge this one sits next to.
                var soonLabel = FindByPath(contents.transform, "ComingSoon/Label")?.GetComponent<TMP_Text>();
                var label = Text(badge, "Label", soonLabel != null ? soonLabel.font : null,
                    soonLabel != null ? soonLabel.fontSharedMaterial : null, 30f, Loc.Get("gift_badge"), layout);
                if (layout) Stretch(label.rectTransform);

                badge.gameObject.SetActive(false); // TaleCard turns it on for gift tales

                var so = new SerializedObject(card);
                so.FindProperty("giftBadge").objectReferenceValue = badge.gameObject;
                so.FindProperty("giftBadgeText").objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, CardPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ── Helpers ───────────────────────────────────────
        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child;
            return null;
        }

        /// <summary>Transform.Find, but walking our own children so inactive screens resolve too.</summary>
        private static Transform FindByPath(Transform root, string path)
        {
            var current = root;
            foreach (var part in path.Split('/'))
            {
                current = FindChild(current, part);
                if (current == null) return null;
            }
            return current;
        }

        private static TMP_FontAsset FontOf(Transform t)
        {
            var text = t != null ? t.GetComponent<TMP_Text>() : null;
            return text != null ? text.font : null;
        }

        private static Material MaterialOf(Transform t)
        {
            var text = t != null ? t.GetComponent<TMP_Text>() : null;
            return text != null ? text.fontSharedMaterial : null;
        }

        /// <summary>Copies the donor's sprite/colour, but never overwrites art already assigned.</summary>
        private static void CopyImage(Transform donor, Image target)
        {
            if (target.sprite != null) return;
            var src = donor != null ? donor.GetComponent<Image>() : null;
            if (src == null) return;

            target.sprite = src.sprite;
            target.color = src.color;
            target.type = src.type;
            target.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
        }

        /// <summary>
        /// Finds or creates a label. <paramref name="style"/> false updates only the string,
        /// leaving a hand-tuned font, size and colour alone.
        /// </summary>
        private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font,
            Material material, float size, string content, bool style = true)
        {
            var t = FindChild(parent, name);
            var go = t != null ? t.gameObject : NewUI(name, parent);
            var text = Ensure<TextMeshProUGUI>(go);

            if (style)
            {
                if (font != null) text.font = font;
                if (material != null) text.fontSharedMaterial = material;
                text.fontSize = size;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.Normal;
            }

            text.text = content;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }

        /// <summary>Full-width row hanging <paramref name="top"/> px below the panel's top edge.</summary>
        private static void TopStretch(RectTransform rt, float top, float height, float margin)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(margin, -(top + height));
            rt.offsetMax = new Vector2(-margin, -top);
        }
    }
}
