using UnityEngine;
using UnityEngine.EventSystems;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Lets the parent nudge a photo head on the avatar preview with a finger (and resize it
    /// with two fingers or the mouse wheel). Sits on the preview's hit area; only acts while
    /// the person's avatar is a photo.
    /// </summary>
    public class AvatarFitter : MonoBehaviour, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public AvatarView view;
        private float _lastPinch;

        private bool Active => view != null && AvatarStore.Get(view.person).kind == AvatarStore.KindPhoto;

        public void OnDrag(PointerEventData e)
        {
            if (!Active || Input.touchCount >= 2) return;
            var r = AvatarStore.Get(view.person);
            var head = view.HeadRect.size;
            if (head.x <= 0 || head.y <= 0) return;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            var d = e.delta / scale;
            // Offsets are in head sizes; local y is up, the record's y is down.
            AvatarStore.SetFit(view.person, r.scale, new Vector2(r.dx + d.x / head.x, r.dy - d.y / head.y), save: false);
        }

        public void OnEndDrag(PointerEventData e) => Commit();

        public void OnScroll(PointerEventData e)
        {
            if (!Active) return;
            Resize(1f + e.scrollDelta.y * 0.05f);
            Commit();
        }

        public void Resize(float factor)
        {
            if (!Active) return;
            var r = AvatarStore.Get(view.person);
            AvatarStore.SetFit(view.person, r.scale * factor, new Vector2(r.dx, r.dy), save: false);
        }

        public void Commit()
        {
            if (!Active) return;
            var r = AvatarStore.Get(view.person);
            AvatarStore.SetFit(view.person, r.scale, new Vector2(r.dx, r.dy));
        }

        private void Update()
        {
            if (!Active || Input.touchCount < 2) { if (_lastPinch > 0) Commit(); _lastPinch = 0; return; }
            float d = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            if (_lastPinch > 0) Resize(d / _lastPinch);
            _lastPinch = d;
        }
    }
}
