using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// Keeps a whole UI subtree's layout in agreement with the text that is actually on
    /// screen, whoever changed that text and whenever they changed it.
    ///
    /// It exists because of a layout that cannot converge in a single pass: a label with a
    /// ContentSizeFitter inside a layout group whose "Control Child Size" is off. The group
    /// places the children from the label's CURRENT width, and only afterwards does the
    /// fitter resize the label to the new text — and nothing asks for the corrective second
    /// pass, because Graphic.OnRectTransformDimensionsChange deliberately skips
    /// SetLayoutDirty while a rebuild is in progress ("prevent double dirtying"). The
    /// siblings keep the offsets that belonged to the previous translation, and keep them
    /// for good. That is the gap the paywall's star rows showed when a short Russian line
    /// was replaced by a longer English one.
    ///
    /// The paywall's own rows no longer rely on that combination — the groups control the
    /// child widths themselves now, which converges in one pass. This guard stays as the
    /// backstop for what the scene cannot promise: UI built from code at runtime, a screen
    /// re-authored later without this trap in mind, and any layout whose text arrives after
    /// it was last measured. It rebuilds TWICE so a fitter nested in a group converges
    /// either way, on every occasion the text can have changed: when the subtree becomes
    /// visible, when the language changes, when the screen is resized, and — as the
    /// catch-all — whenever a watched label's string differs from what was last laid out.
    /// Localized strings arrive asynchronously, so that last trigger is what makes this
    /// reliable rather than well-timed.
    /// </summary>
    [DisallowMultipleComponent]
    public class LayoutGuard : MonoBehaviour
    {
        /// <summary>Rebuild passes per refresh. Two is the minimum that converges a
        /// ContentSizeFitter nested inside a layout group; the third absorbs one more
        /// level of nesting for free, since these subtrees are tiny.</summary>
        private const int Passes = 3;

        /// <summary>Frames to keep refreshing after a trigger. One follow-up is enough: each
        /// rebuild already runs <see cref="Passes"/> passes, and a localized string that
        /// lands later is caught by the per-frame text check. Four frames of forced
        /// rebuilds used to run through the paywall's slide-in.</summary>
        private const int SettleFrames = 1;

        private RectTransform _self;
        private Canvas _canvas;

        private RectTransform[] _roots = System.Array.Empty<RectTransform>();
        private TMP_Text[] _texts = System.Array.Empty<TMP_Text>();
        private string[] _laidOut = System.Array.Empty<string>();

        private int _settle;
        private bool _rebuilding;
        private bool _wasVisible;

        /// <summary>
        /// Adds the guard to <paramref name="go"/> (or returns the one already there), so a
        /// screen can protect itself from code without anyone having to remember a
        /// component in the scene.
        /// </summary>
        public static LayoutGuard Attach(GameObject go)
        {
            if (go == null) return null;
            var guard = go.GetComponent<LayoutGuard>();
            return guard != null ? guard : go.AddComponent<LayoutGuard>();
        }

        private void Awake()
        {
            _self = GetComponent<RectTransform>();
            _canvas = GetComponent<Canvas>();
            Collect();
        }

        private void OnEnable()
        {
            Loc.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.OnLanguageChanged -= Refresh;
        }

        /// <summary>Re-reads the subtree. Call after building UI at runtime.</summary>
        public void Rescan()
        {
            Collect();
            Refresh();
        }

        /// <summary>Lays the subtree out again now, and keeps watching it for a few
        /// frames. Safe to call as often as you like.</summary>
        public void Refresh()
        {
            _settle = SettleFrames;
            if (isActiveAndEnabled && IsVisible) RebuildNow();
        }

        /// <summary>A hidden screen here is one whose Canvas is switched off — the
        /// GameObject stays active — and a switched-off canvas is not worth laying out.
        /// </summary>
        private bool IsVisible => _canvas == null || _canvas.enabled;

        private void LateUpdate()
        {
            var visible = IsVisible;
            if (visible != _wasVisible)
            {
                _wasVisible = visible;
                // Text that changed while hidden lands in one rebuild on the way in. Text
                // that didn't needs none: the screen's own Rescan already laid it out.
                if (visible && TextChanged()) _settle = SettleFrames;
            }
            if (!visible) return;

            if (TextChanged()) _settle = SettleFrames;
            if (_settle <= 0) return;

            _settle--;
            RebuildNow();
        }

        /// <summary>The screen was resized (rotation, safe area): every width the fitters
        /// computed is now suspect.</summary>
        private void OnRectTransformDimensionsChange()
        {
            if (_settle < SettleFrames) _settle = SettleFrames;
        }

        /// <summary>True when any watched label carries a string other than the one the
        /// last rebuild was laid out for.</summary>
        private bool TextChanged()
        {
            for (var i = 0; i < _texts.Length; i++)
            {
                var t = _texts[i];
                if (t == null) continue;
                if (!string.Equals(t.text, _laidOut[i], System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void RebuildNow()
        {
            if (_rebuilding) return;
            _rebuilding = true;

            for (var i = 0; i < _texts.Length; i++)
            {
                var t = _texts[i];
                if (t == null) continue;
                // TMP caches its preferred width and only recomputes it once told the text
                // it measured is no longer the text it holds. Without this the fitters
                // would faithfully re-apply the previous translation's width.
                t.SetLayoutDirty();
                _laidOut[i] = t.text;
            }

            for (var pass = 0; pass < Passes; pass++)
            {
                for (var i = 0; i < _roots.Length; i++)
                {
                    var root = _roots[i];
                    if (root != null) LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                }
            }

            _rebuilding = false;
        }

        private void Collect()
        {
            // Input fields are left out on purpose: their text changes on every keystroke,
            // and a rebuild per keystroke would be work nobody asked for.
            var texts = new List<TMP_Text>();
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                if (text.GetComponentInParent<TMP_InputField>(true) == null)
                    texts.Add(text);
            _texts = texts.ToArray();
            _laidOut = new string[_texts.Length];

            // Every layout group and every fitter, reduced to the rects a rebuild has to
            // start from: ForceRebuildLayoutImmediate walks down from the rect it is given
            // but stops the moment a rect has no layout component on it, so it has to be
            // handed the top of each layout island rather than the screen root.
            var roots = new List<RectTransform>();
            foreach (var group in GetComponentsInChildren<LayoutGroup>(true))
                AddRoot(roots, group.transform as RectTransform);
            foreach (var fitter in GetComponentsInChildren<ContentSizeFitter>(true))
                AddRoot(roots, fitter.transform as RectTransform);
            _roots = roots.ToArray();
        }

        private void AddRoot(List<RectTransform> roots, RectTransform rect)
        {
            if (rect == null) return;
            var root = TopmostLayoutRect(rect);
            if (!roots.Contains(root)) roots.Add(root);
        }

        /// <summary>
        /// Climbs to the outermost rect still driven by a layout group, the same way
        /// LayoutRebuilder.MarkLayoutForRebuild does — a nested group must be rebuilt from
        /// its parent, otherwise the parent hands it a size it never sees. Never climbs
        /// past this guard's own rect.
        /// </summary>
        private RectTransform TopmostLayoutRect(RectTransform rect)
        {
            var root = rect;
            while (root != _self)
            {
                var parent = root.parent as RectTransform;
                if (parent == null || parent.GetComponent<LayoutGroup>() == null) break;
                root = parent;
            }
            return root;
        }
    }
}
