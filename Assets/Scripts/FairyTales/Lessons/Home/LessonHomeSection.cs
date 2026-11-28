using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.Lessons
{
    /// <summary>A row of the lessons home: a title and cards that scroll sideways (one per topic).</summary>
    public class LessonHomeSection : MonoBehaviour
    {
        public TMP_Text title;
        public ScrollRect scroll;
        public RectTransform row;
        /// <summary>Inactive in the topic template; one clone per lesson.</summary>
        public LessonTileView tileTemplate;
    }
}
