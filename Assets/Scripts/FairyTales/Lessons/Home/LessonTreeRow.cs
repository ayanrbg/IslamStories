using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>
    /// A row of the Tree screen (built by LessonHomeBuilder): a leaf, a caption and on
    /// the right either a note («новый!», «вчера») or the parent's «Отметить» button.
    /// </summary>
    public class LessonTreeRow : MonoBehaviour
    {
        public Image icon;
        public TMP_Text title;
        public TMP_Text note;
        public Button button;
    }
}
