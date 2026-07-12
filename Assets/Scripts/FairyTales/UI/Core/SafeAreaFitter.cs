using UnityEngine;

namespace FairyTales.UI.Core
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        private void Update()
        {
            var safeArea = Screen.safeArea;
            if (safeArea == _lastSafeArea) return;
            _lastSafeArea = safeArea;
            Apply(safeArea);
        }

        private void Apply(Rect safeArea)
        {
            var screenW = Screen.width;
            var screenH = Screen.height;
            if (screenW <= 0 || screenH <= 0) return;

            _rect.anchorMin = new Vector2(safeArea.x / screenW, safeArea.y / screenH);
            _rect.anchorMax = new Vector2(
                (safeArea.x + safeArea.width) / screenW,
                (safeArea.y + safeArea.height) / screenH);
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
