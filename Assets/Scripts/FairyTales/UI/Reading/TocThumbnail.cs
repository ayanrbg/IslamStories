using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FairyTales.UI.Reading
{
    public class TocThumbnail : MonoBehaviour
    {
        [SerializeField] private Image cover;
        [SerializeField] private Image border;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button button;

        public Image Cover => cover;
        public Image Border => border;
        public TMP_Text Label => label;
        public Button Button => button;
    }
}
