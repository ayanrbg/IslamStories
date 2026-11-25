using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Draws a family member's avatar inside its RectTransform and keeps it current
    /// (<see cref="AvatarStore.Changed"/>, the child's gender).
    ///
    /// Full: the drawn body, fitted in the box; a photo goes into the face oval, a ready
    /// face covers the drawn head. Bust: a circle with just the face — the photo, the
    /// ready face, or the body zoomed to its head. The circle is the Mask on this object
    /// (its Image is the circle sprite; LessonPlayerBuilder / AvatarScreenBuilder set it up).
    ///
    /// Composed from UI images rather than rendered to a texture: every place an avatar
    /// shows up is uGUI, and the images follow any size without a re-render.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AvatarView : MonoBehaviour
    {
        public AvatarPerson person;
        public bool bust;

        private Image _body;
        private Image _face;
        private bool _subscribed;

        private void OnEnable()
        {
            if (!_subscribed) { AvatarStore.Changed += Refresh; _subscribed = true; }
            Refresh();
        }

        private void OnDisable()
        {
            if (_subscribed) { AvatarStore.Changed -= Refresh; _subscribed = false; }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled) Layout();
        }

        public void Refresh()
        {
            if (this == null) return;
            EnsureParts();
            Layout();
        }

        private void EnsureParts()
        {
            if (_body == null) _body = Part("Body");
            if (_face == null) _face = Part("Face");
        }

        private Image Part(string name)
        {
            var t = transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            var img = t.GetComponent<Image>();
            img.raycastTarget = false;
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        private void Layout()
        {
            var art = AvatarArt.Instance;
            if (art == null || _body == null) return;
            var body = art.BodyFor(person);
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0 || size.y <= 0 || body.sprite == null) return;

            var photo = AvatarStore.Get(person).kind == AvatarStore.KindPhoto ? AvatarStore.PhotoSprite(person) : null;
            var drawnFace = person == AvatarPerson.Child ? art.Face(AvatarStore.FaceIndex(person)) : null;

            if (bust) LayoutBust(body, size, photo, drawnFace);
            else LayoutFull(body, size, photo, drawnFace);
        }

        private void LayoutFull(AvatarArt.Body body, Vector2 box, Sprite photo, Sprite drawnFace)
        {
            // The body fitted into the box (preserve aspect, centred).
            float aspect = body.sprite.rect.width / body.sprite.rect.height;
            var drawn = box.x / box.y > aspect ? new Vector2(box.y * aspect, box.y) : new Vector2(box.x, box.x / aspect);
            Place(_body, body.sprite, Vector2.zero, drawn);

            if (photo != null)
            {
                float h = body.face.height * drawn.y;
                Place(_face, photo, Point(body.face, drawn), new Vector2(h * AvatarStore.PhotoAspect, h));
            }
            else if (drawnFace != null)
            {
                float w = body.head.width * drawn.x;
                float fa = drawnFace.rect.width / drawnFace.rect.height;
                Place(_face, drawnFace, Point(body.head, drawn), new Vector2(w, w / fa));
            }
            else _face.enabled = false;
        }

        private void LayoutBust(AvatarArt.Body body, Vector2 box, Sprite photo, Sprite drawnFace)
        {
            float d = Mathf.Min(box.x, box.y);
            if (photo != null)
            {
                // The oval a little larger than the circle, so no edge shows.
                _body.enabled = false;
                Place(_face, photo, new Vector2(0, -d * 0.04f), new Vector2(d * 1.12f, d * 1.12f / AvatarStore.PhotoAspect));
                return;
            }
            if (drawnFace != null)
            {
                _body.enabled = false;
                float fa = drawnFace.rect.width / drawnFace.rect.height;
                Place(_face, drawnFace, new Vector2(0, -d * 0.06f), new Vector2(d * 1.05f, d * 1.05f / fa));
                return;
            }
            // The drawn body zoomed so its head fills the circle.
            _face.enabled = false;
            float aspect = body.sprite.rect.width / body.sprite.rect.height;
            float w = d * 1.1f / body.head.width;
            var drawn = new Vector2(w, w / aspect);
            var head = Point(body.head, drawn);
            Place(_body, body.sprite, -head + new Vector2(0, -d * 0.05f), drawn);
        }

        /// <summary>Centre of a normalized rect (y from the top) relative to the drawn body's centre.</summary>
        private static Vector2 Point(Rect r, Vector2 drawn)
            => new((r.x - 0.5f) * drawn.x, (0.5f - r.y) * drawn.y);

        private static void Place(Image img, Sprite sprite, Vector2 pos, Vector2 size)
        {
            img.enabled = true;
            img.sprite = sprite;
            img.preserveAspect = false;
            var rt = img.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
