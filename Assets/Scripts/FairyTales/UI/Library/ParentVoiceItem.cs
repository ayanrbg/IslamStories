using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FairyTales.Cache;
using FairyTales.UI.Core;

namespace FairyTales.UI.Library
{
    /// <summary>One row of the parent voices list: who read it, for which child, and a delete button.</summary>
    public class ParentVoiceItem : MonoBehaviour
    {
        [SerializeField] private Button btnPlay;
        [SerializeField] private Button btnDelete;
        [SerializeField] private Image childIcon;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text childText;
        [SerializeField] private Sprite boySprite;
        [SerializeField] private Sprite girlSprite;

        public void Bind(ParentVoice voice, Action onPlay, Action onDelete)
        {
            if (speakerText)
                speakerText.text = string.IsNullOrEmpty(voice.speaker) ? Loc.Get("voice_parent") : voice.speaker;
            if (childText) childText.text = voice.childName ?? "";
            if (childIcon) childIcon.sprite = voice.childGender == "female" ? girlSprite : boySprite;

            btnPlay.onClick.RemoveAllListeners();
            btnPlay.onClick.AddListener(() => onPlay?.Invoke());
            btnDelete.onClick.RemoveAllListeners();
            btnDelete.onClick.AddListener(() => onDelete?.Invoke());
        }
    }
}
