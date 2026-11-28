using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// A sideways row inside a page that scrolls up and down (the home's carousels): a drag
    /// that is mostly vertical goes to the page, a sideways one moves the row.
    /// </summary>
    public class NestedScrollRect : ScrollRect
    {
        private ScrollRect _parent;
        private bool _toParent;

        protected override void Awake()
        {
            base.Awake();
            if (transform.parent != null) _parent = transform.parent.GetComponentInParent<ScrollRect>();
        }

        public override void OnInitializePotentialDrag(PointerEventData e)
        {
            if (_parent != null) _parent.OnInitializePotentialDrag(e);
            base.OnInitializePotentialDrag(e);
        }

        public override void OnBeginDrag(PointerEventData e)
        {
            _toParent = _parent != null && Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x);
            if (_toParent) _parent.OnBeginDrag(e);
            else base.OnBeginDrag(e);
        }

        public override void OnDrag(PointerEventData e)
        {
            if (_toParent) _parent.OnDrag(e);
            else base.OnDrag(e);
        }

        public override void OnEndDrag(PointerEventData e)
        {
            if (_toParent) _parent.OnEndDrag(e);
            else base.OnEndDrag(e);
            _toParent = false;
        }
    }
}
