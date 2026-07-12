using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FairyTales.Api;
using FairyTales.Audio;
using FairyTales.UI.Core;
using FairyTales.UI.Library;
using FairyTales.UI.Onboarding;
using FairyTales.UI.Narration;
using FairyTales.UI.Payment;
using FairyTales.UI.Reading;

namespace FairyTales.Editor
{
    public static class SceneSetup
    {
        [MenuItem("FairyTales/Setup Scene")]
        public static void Setup()
        {
            CreateServices();
            CreateAudio();
            CreateUICanvas();
            OnboardingSetup.Setup();

            Debug.Log("[SceneSetup] Done! Don't forget to save the scene.");
        }

        // ── [Services] — ApiClient ─────────────────────────
        private static void CreateServices()
        {
            if (Object.FindAnyObjectByType<ApiClient>() != null) return;

            var go = new GameObject("[Services]");
            go.AddComponent<ApiClient>();
            Undo.RegisterCreatedObjectUndo(go, "Create Services");
        }

        // ── [Audio] — MicRecorder, NarrationPlayer, BGM ────
        private static void CreateAudio()
        {
            if (Object.FindAnyObjectByType<BackgroundMusicManager>() == null)
            {
                var bgm = new GameObject("[BackgroundMusic]");
                bgm.AddComponent<BackgroundMusicManager>();
                Undo.RegisterCreatedObjectUndo(bgm, "Create BGM");
            }

            if (Object.FindAnyObjectByType<MicRecorder>() != null) return;

            var go = new GameObject("[Audio]");
            go.AddComponent<MicRecorder>();
            go.AddComponent<NarrationPlayer>();
            Undo.RegisterCreatedObjectUndo(go, "Create Audio");
        }

        // ── UI Canvas + ScreenManager + screen stubs ───────
        private static void CreateUICanvas()
        {
            if (Object.FindAnyObjectByType<ScreenManager>() != null) return;

            // EventSystem
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            // Canvas
            var canvas = new GameObject("[UICanvas]");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 0;

            var scaler = canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvas.AddComponent<GraphicRaycaster>();

            var sm = canvas.AddComponent<ScreenManager>();
            Undo.RegisterCreatedObjectUndo(canvas, "Create UICanvas");

            // Shared background (always visible behind all screens)
            var bgGo = new GameObject("SharedBackground", typeof(RectTransform));
            bgGo.transform.SetParent(canvas.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.08f, 0.18f);
            bgImg.preserveAspect = false;

            var bgArf = bgGo.AddComponent<AspectRatioFitter>();
            bgArf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bgArf.aspectRatio = 9f / 16f; // default portrait

            // Screens with real components (onboarding)
            CreateScreen<LanguageSelectScreen>(canvas.transform);
            CreateScreen<PersonalizationScreen>(canvas.transform);
            CreateScreen<LoadingScreen>(canvas.transform);

            // Library screens (Phase 5)
            CreateScreen<LibraryScreen>(canvas.transform);
            CreateScreen<TaleDetailScreen>(canvas.transform);

            // Reading (Phase 6)
            CreateScreen<ReadingScreen>(canvas.transform);

            // Payment
            CreateScreen<PaymentScreen>(canvas.transform);

            // Narration (Phase 7)
            CreateScreen<NarrationSetupScreen>(canvas.transform);
            CreateScreen<VoiceRecordingScreen>(canvas.transform);
            CreateScreen<NarrationProgressScreen>(canvas.transform);

            // SafeAreaFitter — wraps all screens
            var safeArea = new GameObject("SafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(canvas.transform, false);
            var safeRt = safeArea.GetComponent<RectTransform>();
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
            safeArea.AddComponent<SafeAreaFitter>();

            // Toast
            CreateToast(canvas.transform);

            // Set initial screen, onboarded screen, shared background
            var so = new SerializedObject(sm);
            so.FindProperty("initialScreen").objectReferenceValue =
                canvas.GetComponentInChildren<LanguageSelectScreen>(true);
            so.FindProperty("onboardedScreen").objectReferenceValue =
                canvas.GetComponentInChildren<LibraryScreen>(true);
            so.FindProperty("sharedBackground").objectReferenceValue = bgImg;
            so.ApplyModifiedProperties();
        }

        private static void CreateToast(Transform parent)
        {
            var go = new GameObject("Toast", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.05f);
            rt.anchorMax = new Vector2(0.9f, 0.12f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            var cg = go.AddComponent<CanvasGroup>();

            // Label
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(16, 4);
            labelRt.offsetMax = new Vector2(-16, -4);

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 28;
            label.color = Color.white;

            // Wire Toast references
            var toast = go.AddComponent<Toast>();
            var so = new SerializedObject(toast);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.ApplyModifiedProperties();

            go.SetActive(false);
        }

        private static void CreateScreen<T>(Transform parent)
            where T : BaseScreen
        {
            var name = typeof(T).Name;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<CanvasGroup>();
            go.AddComponent<T>();
            go.SetActive(false);
        }
    }
}
