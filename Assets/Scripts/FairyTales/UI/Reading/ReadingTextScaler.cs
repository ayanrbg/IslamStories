using UnityEngine;
using TMPro;

namespace FairyTales.UI.Reading
{
    /// <summary>
    /// Auto-scales reading text font size based on screen size.
    /// Attach to ReadingScreen; references are set by ReadingSetup.
    /// </summary>
    public class ReadingTextScaler : MonoBehaviour
    {
        [SerializeField] private TMP_Text pageText;

        [Header("Font")]
        [SerializeField] private float baseFontSize = 22f;
        [SerializeField] private float minFontSize = 18f;
        [SerializeField] private float maxFontSize = 34f;

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            float diagonal = GetScreenDiagonalInches();
            float t = Mathf.InverseLerp(4f, 11f, diagonal);

            float fontSize = Mathf.Lerp(maxFontSize, baseFontSize, t);
            fontSize = Mathf.Clamp(fontSize, minFontSize, maxFontSize);

            if (pageText)
            {
                pageText.fontSize = fontSize;
                pageText.enableAutoSizing = false;
            }
        }

        private static float GetScreenDiagonalInches()
        {
            float dpi = Screen.dpi;
            if (dpi <= 0) dpi = 160f;

            float wInch = Screen.width / dpi;
            float hInch = Screen.height / dpi;
            return Mathf.Sqrt(wInch * wInch + hInch * hInch);
        }
    }
}
