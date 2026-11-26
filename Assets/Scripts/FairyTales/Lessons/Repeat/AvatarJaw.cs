using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Makes an avatar talk in the «Персонаж» mode (spec §4: "рот двигается от громкости"):
    /// the paper-doll trick — a copy of the head, clipped to the jaw, slides down over a dark
    /// mouth. Works the same on a photo head and on a drawn one, without any mouth art.
    /// At <see cref="Open"/> = 0 the copy sits exactly on the head, so nothing shows.
    /// </summary>
    [RequireComponent(typeof(AvatarView))]
    public class AvatarJaw : MonoBehaviour
    {
        /// <summary>0 = closed, 1 = wide open. Set every frame from the mic level.</summary>
        [Range(0, 1)] public float Open;
        /// <summary>Widest opening, in face widths.</summary>
        public float MaxDrop = 0.12f;
        public Color mouthColor = new(0.36f, 0.12f, 0.16f);

        private AvatarView _view;
        private RectTransform _jaw;
        private Image _jawHead;
        private Image _mouth;
        private float _shown = -1;

        private void Awake()
        {
            _view = GetComponent<AvatarView>();
        }

        private void OnEnable()
        {
            _view.LaidOut += Place;
            Place();
        }

        private void OnDisable()
        {
            _view.LaidOut -= Place;
            if (_jaw != null) _jaw.gameObject.SetActive(false);
            if (_mouth != null) _mouth.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(_shown, Open)) Place();
        }

        private void Ensure()
        {
            if (_jaw != null) return;
            var mouthGo = new GameObject("Mouth", typeof(RectTransform), typeof(Image));
            mouthGo.transform.SetParent(transform, false);
            _mouth = mouthGo.GetComponent<Image>();
            _mouth.raycastTarget = false;
            _mouth.sprite = AvatarArt.Instance != null ? AvatarArt.Instance.circle : null;
            _mouth.color = mouthColor;

            var jawGo = new GameObject("Jaw", typeof(RectTransform), typeof(RectMask2D));
            jawGo.transform.SetParent(transform, false);
            _jaw = (RectTransform)jawGo.transform;
            var headGo = new GameObject("JawHead", typeof(RectTransform), typeof(Image));
            headGo.transform.SetParent(_jaw, false);
            _jawHead = headGo.GetComponent<Image>();
            _jawHead.raycastTarget = false;
            foreach (var rt in new[] { (RectTransform)mouthGo.transform, _jaw, (RectTransform)headGo.transform })
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private void Place()
        {
            if (_view == null || !isActiveAndEnabled) return;
            var head = _view.HeadImage;
            float face = _view.FaceWidth;
            if (head == null || !head.enabled || head.sprite == null || face <= 1f)
            {
                if (_jaw != null) { _jaw.gameObject.SetActive(false); _mouth.gameObject.SetActive(false); }
                return;
            }
            Ensure();
            _shown = Open;
            // Above the head image, below the body's front layer.
            int at = head.transform.GetSiblingIndex() + 1;
            _mouth.transform.SetSiblingIndex(at);
            _jaw.SetSiblingIndex(at + 1);

            var chin = _view.Chin;
            float mouthY = chin.y + face * 0.30f;                 // a face is ~1.25 widths tall
            float jawBottom = chin.y - face * 0.06f;
            float drop = Mathf.Clamp01(Open) * MaxDrop * face;
            float jawW = face * 0.5f;     // narrower than the face: the cheeks stay put

            _jaw.gameObject.SetActive(true);
            _jaw.anchoredPosition = new Vector2(chin.x, (mouthY + jawBottom) / 2f - drop);
            _jaw.sizeDelta = new Vector2(jawW, mouthY - jawBottom);
            _jawHead.sprite = head.sprite;
            var headRt = head.rectTransform;
            _jawHead.rectTransform.sizeDelta = headRt.sizeDelta;
            // In the jaw's space: where the head is, minus the jaw's own (dropped) centre.
            _jawHead.rectTransform.anchoredPosition = headRt.anchoredPosition - _jaw.anchoredPosition - new Vector2(0, drop);

            _mouth.gameObject.SetActive(drop > 0.5f);
            _mouth.rectTransform.anchoredPosition = new Vector2(chin.x, mouthY - drop * 0.5f);
            _mouth.rectTransform.sizeDelta = new Vector2(jawW * 0.7f, drop + face * 0.04f);
        }
    }
}
