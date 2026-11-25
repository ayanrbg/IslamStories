using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Fitting a face into the avatar oval: the photo sits under a frame with an oval
    /// hole; a finger drags it, two fingers (or the mouse wheel) zoom it, and the oval
    /// always stays covered. <see cref="Crop"/> cuts the oval out with soft edges.
    ///
    /// Also shows the live front camera before the shot (<see cref="ShowCamera"/>).
    /// </summary>
    public class PhotoCropper : MonoBehaviour, IDragHandler, IScrollHandler
    {
        /// <summary>The visible area (this object's rect when unset).</summary>
        public RectTransform viewport;
        public RawImage photo;
        /// <summary>The oval's box inside the viewport — what gets cut out.</summary>
        public RectTransform oval;
        public float maxZoom = 4f;

        private Texture2D _texture;
        private Vector2 _size;      // photo size in viewport units at zoom 1 (rotation applied)
        private float _zoom = 1f;
        private Vector2 _pan;
        private float _lastPinch;
        private bool _live;

        public Texture2D Texture => _texture;

        private RectTransform View => viewport != null ? viewport : (RectTransform)transform;

        /// <summary>A still photo to fit. The cropper does not own it: the caller destroys it.</summary>
        public void SetPhoto(Texture2D texture)
        {
            _live = false;
            _texture = texture;
            photo.texture = texture;
            photo.uvRect = new Rect(0, 0, 1, 1);
            photo.rectTransform.localEulerAngles = Vector3.zero;
            Fit(texture.width, texture.height);
        }

        /// <summary>
        /// Live preview of a camera: turned upright by <paramref name="angle"/> and mirrored
        /// like a mirror when <paramref name="mirror"/>. No dragging while it is live.
        /// </summary>
        public void ShowCamera(Texture cam, int angle, bool mirror, bool flipY)
        {
            _live = true;
            _texture = null;
            photo.texture = cam;
            var (flipU, flipV) = Flips(angle, mirror, flipY);
            photo.uvRect = new Rect(flipU ? 1 : 0, flipV ? 1 : 0, flipU ? -1 : 1, flipV ? -1 : 1);
            photo.rectTransform.localEulerAngles = new Vector3(0, 0, -angle);
            bool sideways = angle % 180 != 0;
            Fit(sideways ? cam.height : cam.width, sideways ? cam.width : cam.height);
            // The RawImage's own rect is in the camera's orientation.
            if (sideways) photo.rectTransform.sizeDelta = new Vector2(_size.y, _size.x);
        }

        /// <summary>
        /// Texture-space flips that make the turned picture a mirror image on screen: when the
        /// texture is shown sideways, a left-right mirror on screen is an up-down flip of the texture.
        /// </summary>
        public static (bool u, bool v) Flips(int angle, bool mirror, bool flipY)
        {
            bool sideways = (((angle % 360) + 360) % 360) % 180 != 0;
            return (mirror && !sideways, flipY ^ (mirror && sideways));
        }

        private void Fit(float w, float h)
        {
            var view = View.rect.size;
            float baseScale = Mathf.Max(view.x / w, view.y / h);
            _size = new Vector2(w, h) * baseScale;
            _zoom = 1f;
            _pan = Vector2.zero;
            photo.rectTransform.anchorMin = photo.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            photo.rectTransform.sizeDelta = _size;
            Apply();
        }

        public void OnDrag(PointerEventData e)
        {
            if (_live || _texture == null || Input.touchCount >= 2) return;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            _pan += e.delta / scale;
            Apply();
        }

        public void OnScroll(PointerEventData e)
        {
            if (_live || _texture == null) return;
            _zoom *= 1f + e.scrollDelta.y * 0.08f;
            Apply();
        }

        private void Update()
        {
            if (_live || _texture == null || Input.touchCount < 2) { _lastPinch = 0; return; }
            float d = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            if (_lastPinch > 0) { _zoom *= d / _lastPinch; Apply(); }
            _lastPinch = d;
        }

        private void Apply()
        {
            _zoom = Mathf.Clamp(_zoom, 1f, maxZoom);
            // Keep the oval covered: the photo edge may not come inside it.
            var half = _size * _zoom / 2f;
            var ov = OvalRect();
            float minX = ov.xMax - half.x, maxX = ov.xMin + half.x;
            float minY = ov.yMax - half.y, maxY = ov.yMin + half.y;
            _pan.x = minX <= maxX ? Mathf.Clamp(_pan.x, minX, maxX) : (ov.center.x);
            _pan.y = minY <= maxY ? Mathf.Clamp(_pan.y, minY, maxY) : (ov.center.y);
            photo.rectTransform.anchoredPosition = _pan;
            photo.rectTransform.localScale = Vector3.one * _zoom;
        }

        /// <summary>The oval's box in viewport-centre coordinates.</summary>
        private Rect OvalRect()
        {
            if (oval == null) { var r = View.rect; return new Rect(-r.width / 2, -r.height / 2, r.width, r.height); }
            var size = oval.rect.size;
            Vector2 c = View.InverseTransformPoint(oval.TransformPoint(oval.rect.center));
            return new Rect(c - size / 2, size);
        }

        /// <summary>
        /// The face inside the oval as a new readable texture of <paramref name="w"/>×<paramref name="h"/>,
        /// transparent outside the oval with a soft edge. Null without a still photo.
        /// </summary>
        public Texture2D Crop(int w = AvatarStore.PhotoWidth, int h = AvatarStore.PhotoHeight)
        {
            if (_texture == null || !_texture.isReadable) return null;
            var ov = OvalRect();
            var shown = _size * _zoom;
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            const float feather = 0.06f;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f) { pixels[y * w + x] = default; continue; }
                    // Point in the viewport → in the photo's uv.
                    var p = new Vector2(ov.xMin + u * ov.width, ov.yMin + v * ov.height);
                    float pu = (p.x - _pan.x) / shown.x + 0.5f;
                    float pv = (p.y - _pan.y) / shown.y + 0.5f;
                    var c = _texture.GetPixelBilinear(pu, pv);
                    c.a = Mathf.Clamp01((1f - r) / feather);
                    pixels[y * w + x] = c;
                }
            }
            result.SetPixels32(pixels);
            result.Apply();
            return result;
        }
    }
}
