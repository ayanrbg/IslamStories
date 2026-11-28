using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FairyTales.Lessons
{
    /// <summary>Hands the finger over to a lesson step that has no MonoBehaviour of its own («Обведи»).</summary>
    public class TracePad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public event Action<PointerEventData> Down, Drag, Up;

        public void OnPointerDown(PointerEventData e) => Down?.Invoke(e);
        public void OnDrag(PointerEventData e) => Drag?.Invoke(e);
        public void OnPointerUp(PointerEventData e) => Up?.Invoke(e);
    }
}
