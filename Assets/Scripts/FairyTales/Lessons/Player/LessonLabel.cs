using TMPro;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>
    /// A fixed caption of the lesson player (button label, header). The player fills
    /// every label from <see cref="LessonStrings"/> when it opens, so the prefab carries
    /// keys, not Russian text.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LessonLabel : MonoBehaviour
    {
        public string key;

        public void Apply()
        {
            if (!string.IsNullOrEmpty(key)) GetComponent<TMP_Text>().text = LessonStrings.T(key);
        }
    }
}
