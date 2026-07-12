using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FairyTales.UI.Core
{
    public class ButtonScaleEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float pressScale = 0.9f;
        [SerializeField] private float duration = 0.1f;

        private Vector3 _originalScale;

        private void Awake() => _originalScale = transform.localScale;

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_originalScale * pressScale, duration)
                .SetEase(Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_originalScale, duration)
                .SetEase(Ease.OutBack);
        }
    }
}
