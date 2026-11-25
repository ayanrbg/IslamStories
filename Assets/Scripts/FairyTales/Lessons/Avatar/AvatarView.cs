using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Draws a family member's avatar inside its RectTransform and keeps it current
    /// (<see cref="AvatarStore.Changed"/>, the child's gender).
    ///
    /// Full: a drawn body without a head, the head — a ready drawn one or the child's own,
    /// cut out of a photo — standing with its chin on the neck, then the body's front layer.
    /// The whole figure is fitted into the box. Bust: a circle with just the head (the
    /// circle is the Mask on this object; the builders set it up).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AvatarView : MonoBehaviour
    {
        public AvatarPerson person;
        public bool bust;

        private Image _body;
        private Image _head;
        private Image _front;
        private bool _subscribed;

        /// <summary>Head rectangle (local to this view) after the last layout — for dragging it.</summary>
        public Rect HeadRect { get; private set; }
        /// <summary>Width of the drawn body in local units, after the last layout.</summary>
        public float BodyWidth { get; private set; }

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
            if (_body == null) _body = Part("Body");
            if (_head == null) _head = Part("Head");
            if (_front == null) _front = Part("Front");
            Layout();
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
            t.SetAsLastSibling(); // Body, Head, Front in this order
            var img = t.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = false;
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        /// <summary>The head to draw: its sprite, chin (normalized, y from the top) and width in body widths.</summary>
        private bool ResolveHead(AvatarArt art, AvatarArt.Body body, AvatarRecord r,
            out Sprite sprite, out Vector2 chin, out float width)
        {
            if (r.kind == AvatarStore.KindPhoto && AvatarStore.PhotoSprite(person) is { } photo)
            {
                sprite = photo;
                chin = new Vector2(r.chinX, r.chinY);
                // Scaled by the face so every child's face comes out the same size; hair follows.
                width = body.faceWidth / Mathf.Max(0.05f, r.faceWidth) * r.scale;
                return true;
            }
            var head = art.HeadFor(person, r.kind == AvatarStore.KindFace ? r.face : art.DefaultHead(person));
            sprite = head?.sprite;
            chin = head?.chin ?? new Vector2(0.5f, 1f);
            width = body.headWidth;
            return sprite != null;
        }

        private void Layout()
        {
            var art = AvatarArt.Instance;
            if (art == null || _body == null) return;
            var record = AvatarStore.Get(person);
            var body = art.BodyFor(person, record);
            var box = ((RectTransform)transform).rect.size;
            if (box.x <= 0 || box.y <= 0 || body?.sprite == null) return;

            bool hasHead = ResolveHead(art, body, record, out var headSprite, out var chin, out float headW);
            float headAspect = hasHead ? headSprite.rect.height / headSprite.rect.width : 1f;
            var offset = record.kind == AvatarStore.KindPhoto ? new Vector2(record.dx, record.dy) : Vector2.zero;

            if (bust) { LayoutBust(box, hasHead, headSprite, chin, headAspect, record); return; }

            // Figure in body-width units, y DOWN from the top of the body sprite.
            float bodyH = body.sprite.rect.height / body.sprite.rect.width;
            float hw = headW, hh = headW * headAspect;
            float hx0 = body.neck.x - chin.x * hw + offset.x * hw;
            float hy0 = body.neck.y * bodyH - chin.y * hh + offset.y * hh;
            float minX = Mathf.Min(0, hasHead ? hx0 : 0), maxX = Mathf.Max(1, hasHead ? hx0 + hw : 1);
            float minY = Mathf.Min(0, hasHead ? hy0 : 0), maxY = Mathf.Max(bodyH, hasHead ? hy0 + hh : bodyH);
            float figW = maxX - minX, figH = maxY - minY;
            float k = Mathf.Min(box.x / figW, box.y / figH);   // units → local
            // Centre the figure; local coords have y UP from the box centre.
            Vector2 ToLocal(float x, float y) => new((x - minX - figW / 2f) * k, (figH / 2f - (y - minY)) * k);

            BodyWidth = k;
            Place(_body, body.sprite, ToLocal(0.5f, bodyH / 2f), new Vector2(k, bodyH * k));
            if (hasHead)
            {
                var c = ToLocal(hx0 + hw / 2f, hy0 + hh / 2f);
                var size = new Vector2(hw * k, hh * k);
                Place(_head, headSprite, c, size);
                HeadRect = new Rect(c - size / 2f, size);
            }
            else _head.enabled = false;
            if (body.front != null) Place(_front, body.front, ToLocal(0.5f, bodyH / 2f), new Vector2(k, bodyH * k));
            else _front.enabled = false;
        }

        private void LayoutBust(Vector2 box, bool hasHead, Sprite headSprite, Vector2 chin, float headAspect, AvatarRecord r)
        {
            _body.enabled = false;
            _front.enabled = false;
            if (!hasHead) { _head.enabled = false; return; }
            float d = Mathf.Min(box.x, box.y);
            float w;
            Vector2 centre;
            if (r.kind == AvatarStore.KindPhoto)
            {
                // The face fills about 60% of the circle; centred a little above the chin.
                w = d * 0.62f / Mathf.Max(0.05f, r.faceWidth);
                float faceH = r.faceWidth * 1.25f; // in head widths, a face is a bit taller than wide
                var faceCentre = new Vector2(chin.x, chin.y * headAspect - faceH * 0.5f); // in head widths
                centre = new Vector2((0.5f - faceCentre.x) * w, (faceCentre.y - headAspect / 2f) * w);
            }
            else
            {
                w = d * 1.02f;
                centre = new Vector2(0, -d * 0.04f);
            }
            Place(_head, headSprite, centre, new Vector2(w, w * headAspect));
            HeadRect = new Rect(centre - new Vector2(w, w * headAspect) / 2f, new Vector2(w, w * headAspect));
        }

        private static void Place(Image img, Sprite sprite, Vector2 pos, Vector2 size)
        {
            img.enabled = true;
            img.sprite = sprite;
            var rt = img.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
