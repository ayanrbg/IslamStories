using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FairyTales.Diagnostics
{
    /// <summary>
    /// On-screen debug overlay for diagnosing the LIVE App Store build (where the
    /// device console / Xcode isn't available). Captures Unity's log stream
    /// (<see cref="Application.logMessageReceived"/>) and mirrors it onto a TMP text
    /// element. The text auto-clears (fades out) a few seconds after the last message,
    /// so it doesn't stay on screen forever.
    ///
    /// Setup: drop this component on a GameObject that has (or references) a
    /// TextMeshProUGUI on a screen-space overlay Canvas, and assign <c>logText</c>.
    /// It picks up all the existing [IAP-DBG] / [Promo] / [REMOTE-LOG] purchase logs
    /// automatically — no other code changes required.
    /// </summary>
    public class OnScreenLog : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("TMP text where logs are printed. Put it on a full-screen overlay Canvas.")]
        [SerializeField] private TextMeshProUGUI logText;

        [Header("Behaviour")]
        [Tooltip("Seconds of silence (no new log) before the text starts fading out.")]
        [SerializeField] private float clearDelay = 2f;
        [Tooltip("How long the fade-out takes, in seconds.")]
        [SerializeField] private float fadeDuration = 0.4f;
        [Tooltip("Max lines kept on screen (oldest dropped).")]
        [SerializeField] private int maxLines = 18;
        [Tooltip("Show only purchase-related logs (IAP/Promo/RemoteLog) + all warnings/errors. Off = show everything.")]
        [SerializeField] private bool onlyPaymentLogs = true;

        public static OnScreenLog Instance { get; private set; }

        private readonly List<string> _lines = new List<string>();
        private float _lastLogTime;
        private bool _dirty;
        private CanvasGroup _group;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (logText)
            {
                logText.text = "";
                // CanvasGroup fades everything reliably, including inline <color> lines.
                _group = logText.GetComponent<CanvasGroup>();
                if (_group == null) _group = logText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            if (Instance == this) Instance = null;
        }

        /// <summary>Push a custom line onto the overlay (same as a Debug.Log would).</summary>
        public static void Push(string message)
        {
            if (Instance != null) Instance.Append(message, LogType.Log);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (onlyPaymentLogs && !IsRelevant(condition, type)) return;
            Append(condition, type);
        }

        private static bool IsRelevant(string condition, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
                return true;
            if (string.IsNullOrEmpty(condition)) return false;
            return condition.Contains("[IAP") ||
                   condition.Contains("[Promo]") ||
                   condition.Contains("[REMOTE-LOG]");
        }

        private void Append(string message, LogType type)
        {
            if (string.IsNullOrEmpty(message)) return;

            // Keep it to one line per entry so the overlay stays readable.
            var line = message.Replace("\n", " ").Replace("\r", " ");
            if (line.Length > 220) line = line.Substring(0, 220) + "…";

            if (type == LogType.Error || type == LogType.Exception)
                line = $"<color=#ff5555>{line}</color>";
            else if (type == LogType.Warning)
                line = $"<color=#ffcc44>{line}</color>";

            _lines.Add(line);
            if (_lines.Count > maxLines) _lines.RemoveRange(0, _lines.Count - maxLines);

            _lastLogTime = Time.unscaledTime;
            _dirty = true;
        }

        private void Update()
        {
            if (!logText) return;

            if (_dirty)
            {
                logText.text = string.Join("\n", _lines);
                _dirty = false;
            }

            if (_lines.Count == 0) return;

            float elapsed = Time.unscaledTime - _lastLogTime;
            if (elapsed <= clearDelay)
            {
                SetAlpha(1f);
                return;
            }

            float t = fadeDuration > 0f ? (elapsed - clearDelay) / fadeDuration : 1f;
            if (t >= 1f)
            {
                _lines.Clear();
                logText.text = "";
                SetAlpha(1f); // reset for the next burst
            }
            else
            {
                SetAlpha(1f - t);
            }
        }

        private void SetAlpha(float a)
        {
            if (_group) _group.alpha = a;
        }
    }
}
