using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// A sticker tile of the lessons home (built by LessonHomeBuilder): a picture, a
    /// label in a white pill that fits its text, and one badge on the corner.
    /// </summary>
    public class LessonTileView : MonoBehaviour
    {
        public enum Badge { None, Play, Lock, Done }

        public Button button;
        public Image picture;
        public AspectRatioFitter fit;
        public TMP_Text label;
        public RectTransform labelPill;
        public GameObject play;
        public GameObject locked;
        public GameObject done;

        private const float PillPadding = 50f;
        private float _fontSize;

        public void SetPicture(Sprite sprite)
        {
            if (sprite == null || picture == null) return;
            picture.sprite = sprite;
            if (fit != null) fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        public void SetLabel(string text)
        {
            if (label == null) return;
            label.text = text;
            labelPill.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (string.IsNullOrEmpty(text)) return;

            // The pill hugs the text, never wider than the tile leaves room for.
            var parent = (RectTransform)labelPill.parent;
            float max = parent.rect.width - 150f; // room for the corner badge
            // A long title shrinks to fit instead of ending in an ellipsis.
            if (_fontSize <= 0) _fontSize = label.fontSize;
            label.enableAutoSizing = false;
            label.fontSize = _fontSize;
            float want = label.GetPreferredValues(text).x + PillPadding;
            float w = Mathf.Min(want, Mathf.Max(200f, max));
            if (want > w)
            {
                label.enableAutoSizing = true;
                label.fontSizeMax = _fontSize;
                label.fontSizeMin = _fontSize * 0.7f;
            }
            var left = labelPill.anchoredPosition.x - labelPill.sizeDelta.x / 2;
            labelPill.sizeDelta = new Vector2(w, labelPill.sizeDelta.y);
            labelPill.anchoredPosition = new Vector2(left + w / 2, labelPill.anchoredPosition.y);
            label.rectTransform.sizeDelta = new Vector2(w - 20, label.rectTransform.sizeDelta.y);
        }

        public void SetBadge(Badge badge)
        {
            if (play) play.SetActive(badge == Badge.Play);
            if (locked) locked.SetActive(badge == Badge.Lock);
            if (done) done.SetActive(badge == Badge.Done);
        }
    }

}
