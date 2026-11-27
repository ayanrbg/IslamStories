using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>A page in the coloring gallery: the child's picture (if any) under the line art.</summary>
    public class ColoringCard : MonoBehaviour
    {
        public Button button;
        public RawImage picture;
        public RawImage lines;
        public TMP_Text title;
    }
}
