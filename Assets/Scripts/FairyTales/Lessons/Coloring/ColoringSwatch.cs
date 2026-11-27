using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>A crayon in the coloring palette; the chosen one is bigger, with a ring.</summary>
    public class ColoringSwatch : MonoBehaviour
    {
        public Button button;
        public Color32 color;
        public GameObject ring;

        public void SetSelected(bool on)
        {
            if (ring != null) ring.SetActive(on);
            transform.DOKill();
            transform.DOScale(on ? 1.15f : 1f, 0.18f).SetEase(on ? Ease.OutBack : Ease.OutQuad);
        }
    }
}
