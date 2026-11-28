using TMPro;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>
    /// A solid cartoon shadow under a title: this text repeats <see cref="source"/> (its words
    /// and auto-sized font size), a few pixels down-right, in a dark colour. A font underlay
    /// can't do it — the font atlas has too little padding for a shadow this thick.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextDropShadow : MonoBehaviour
    {
        public TMP_Text source;
        private TMP_Text _self;

        private void LateUpdate()
        {
            if (source == null) return;
            if (_self == null) _self = GetComponent<TMP_Text>();
            if (_self.text != source.text) _self.text = source.text;
            if (!Mathf.Approximately(_self.fontSize, source.fontSize))
            {
                _self.enableAutoSizing = false;
                _self.fontSize = source.fontSize;
            }
        }
    }
}
