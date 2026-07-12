using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.UI.Core
{
    public class UnlockPopup : MonoBehaviour
    {
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnUnlock;
        private CanvasGroup _cg;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

            if (btnClose) btnClose.onClick.AddListener(Hide);
            if (btnUnlock) btnUnlock.onClick.AddListener(OnUnlock);
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _cg.alpha = 0f;
            _cg.DOFade(1f, 0.25f);
        }

        public void Hide()
        {
            _cg.DOFade(0f, 0.2f)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void OnUnlock()
        {
            Toast.Show(Loc.Get("unlock_coming_soon"));
            Hide();
        }
    }
}
