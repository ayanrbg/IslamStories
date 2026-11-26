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
        /// <summary>Bottom of the chin (local to this view) after the last layout.</summary>
        public Vector2 Chin { get; private set; }
        /// <summary>Width of the face, cheek to cheek, in local units after the last layout.</summary>
        public float FaceWidth { get; private set; }
        /// <summary>The head image (its sprite and rect are current after a layout).</summary>
        public Image HeadImage => _head;
        /// <summary>Called after every layout (the talking jaw follows the head).</summary>
        public event System.Action LaidOut;

        // A figure that isn't one of the family (the friend in the repeat step).
        private AvatarArt.Body _figureBody;
        private Sprite _figureHead;
        private Vector2 _figureChin;
        private float _figureFace;

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

        /// <summary>
        /// Shows a figure instead of the family member: <paramref name="body"/> with
        /// <paramref name="head"/> (chin normalized, y from the top). <paramref name="faceWidth"/> —
        /// the face's share of the head width for a photo head (sized like the child's photo),
        /// or 0 for a drawn head (the body's drawn-head width). A null head shows the body alone.
        /// </summary>
        public void ShowFigure(AvatarArt.Body body, Sprite head, Vector2 chin, float faceWidth)
        {
            _figureBody = body;
            _figureHead = head;
            _figureChin = chin;
            _figureFace = faceWidth;
            Refresh();
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
            bool figure = _figureBody?.sprite != null;
            var record = figure ? new AvatarRecord() : AvatarStore.Get(person);
            var body = figure ? _figureBody : art.BodyFor(person, record);
            var box = ((RectTransform)transform).rect.size;
            if (box.x <= 0 || box.y <= 0 || body?.sprite == null) return;

            bool hasHead;
            Sprite headSprite;
            Vector2 chin;
            float headW;
            float faceShare; // face width as a share of the head width
            if (figure)
            {
                hasHead = _figureHead != null;
                headSprite = _figureHead;
                chin = _figureChin;
                headW = _figureFace > 0 ? body.faceWidth / _figureFace : body.headWidth;
                faceShare = _figureFace > 0 ? _figureFace : DrawnFaceShare;
            }
            else
            {
                hasHead = ResolveHead(art, body, record, out headSprite, out chin, out headW);
                faceShare = record.kind == AvatarStore.KindPhoto ? Mathf.Max(0.05f, record.faceWidth) : DrawnFaceShare;
            }
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
                Chin = ToLocal(hx0 + chin.x * hw, hy0 + chin.y * hh);
                FaceWidth = faceShare * hw * k;
            }
            else
            {
                _head.enabled = false;
                Chin = ToLocal(body.neck.x, body.neck.y * bodyH);
                FaceWidth = 0;
            }
            if (body.front != null) Place(_front, body.front, ToLocal(0.5f, bodyH / 2f), new Vector2(k, bodyH * k));
            else _front.enabled = false;
            LaidOut?.Invoke();
        }

        /// <summary>A drawn head's face is about this share of its width (hair and scarf around it).</summary>
        private const float DrawnFaceShare = 0.62f;

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
