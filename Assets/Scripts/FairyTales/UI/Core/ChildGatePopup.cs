using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.UI.Core
{
    public class ChildGatePopup : MonoBehaviour
    {
        private static ChildGatePopup _instance;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text problemText;
        [SerializeField] private TMP_Text inputText;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private Button btnClose;
        [SerializeField] private Button[] numButtons; // 0-9
        [SerializeField] private Button btnBackspace;

        private string _answer;
        private string _input = "";
        private Action _onSuccess;
        private Action _onCancel;

        private static string[] DigitWords => new[]
        {
            Loc.Get("digit_0"), Loc.Get("digit_1"), Loc.Get("digit_2"), Loc.Get("digit_3"), Loc.Get("digit_4"),
            Loc.Get("digit_5"), Loc.Get("digit_6"), Loc.Get("digit_7"), Loc.Get("digit_8"), Loc.Get("digit_9")
        };

        private void Awake()
        {
            _instance = this;

            if (btnClose) btnClose.onClick.AddListener(OnCancelButton);
            if (btnBackspace) btnBackspace.onClick.AddListener(OnBackspace);

            for (int i = 0; i < numButtons.Length; i++)
            {
                int digit = i;
                if (numButtons[i]) numButtons[i].onClick.AddListener(() => OnDigit(digit));
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        public static void Show(Action onSuccess, Action onCancel = null)
        {
            if (_instance == null)
            {
                // RELEASE: Debug.LogWarning("[ChildGate] No instance — bypassing");
                onSuccess?.Invoke();
                return;
            }
            _instance.Present(onSuccess, onCancel);
        }

        private void Present(Action onSuccess, Action onCancel)
        {
            _onSuccess = onSuccess;
            _onCancel = onCancel;
            _input = "";
            GenerateProblem();
            UpdateDisplay();

            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        private void GenerateProblem()
        {
            int d1 = UnityEngine.Random.Range(0, 10);
            int d2 = UnityEngine.Random.Range(0, 10);
            int d3 = UnityEngine.Random.Range(0, 10);
            _answer = $"{d1}{d2}{d3}";
            if (problemText)
                problemText.text = $"{DigitWords[d1]}, {DigitWords[d2]}, {DigitWords[d3]}";
        }

        private void OnDigit(int digit)
        {
            if (_input.Length >= 3) return;
            _input += digit.ToString();
            UpdateDisplay();
            CheckAnswer();
        }

        private void OnBackspace()
        {
            if (_input.Length == 0) return;
            _input = _input.Substring(0, _input.Length - 1);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (inputText) inputText.text = _input;
            if (placeholderText)
                placeholderText.gameObject.SetActive(_input.Length == 0);
        }

        private void CheckAnswer()
        {
            if (_input != _answer) return;

            var cb = _onSuccess;
            _onSuccess = null;
            _onCancel = null;
            Close(() => cb?.Invoke());
        }

        private void Close(Action onComplete = null)
        {
            canvasGroup.interactable = false;
            canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        private void Close() => Close(null);

        private void OnCancelButton()
        {
            var cb = _onCancel;
            _onCancel = null;
            _onSuccess = null;
            Close(() => cb?.Invoke());
        }
    }
}
