using UnityEngine;
using UnityEngine.UI;

namespace FairyTales.UI.Core
{
    /// <summary>
    /// Twinkling & rotating stars using CanvasRenderer (avoids Canvas mesh rebuild).
    /// Runs on an isolated Canvas so updates never dirty the main UI Canvas.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class StarBackground : MonoBehaviour
    {
        [Header("Stars")]
        [SerializeField] int starCount = 40;
        [SerializeField] Sprite starSprite;
        [SerializeField] Color starColor = new Color(1f, 1f, 0.85f, 0.8f);
        [SerializeField] float minSize = 8f;
        [SerializeField] float maxSize = 28f;

        [Header("Twinkle")]
        [SerializeField] float minAlpha = 0.15f;
        [SerializeField] float maxAlpha = 0.9f;
        [SerializeField] float twinkleSpeedMin = 0.3f;
        [SerializeField] float twinkleSpeedMax = 0.8f;

        [Header("Rotation")]
        [SerializeField] float rotationRange = 25f;
        [SerializeField] float rotationSpeedMin = 0.15f;
        [SerializeField] float rotationSpeedMax = 0.4f;

        // Per-star data (struct of arrays for cache friendliness)
        CanvasRenderer[] _renderers;
        RectTransform[] _rects;
        float[] _twinkleSpeed;
        float[] _twinklePhase;
        float[] _rotSpeed;
        float[] _rotPhase;
        float[] _baseRotation;

        // Throttle: update half the stars each frame (alternating)
        int _batchOffset;

        void Awake()
        {
            // Nested Canvas isolates rebuilds from the main UI Canvas.
            // Sorting order is determined by hierarchy position (sibling order).

            var rect = GetComponent<RectTransform>();
            var w = rect.rect.width;
            var h = rect.rect.height;

            _renderers = new CanvasRenderer[starCount];
            _rects = new RectTransform[starCount];
            _twinkleSpeed = new float[starCount];
            _twinklePhase = new float[starCount];
            _rotSpeed = new float[starCount];
            _rotPhase = new float[starCount];
            _baseRotation = new float[starCount];

            for (int i = 0; i < starCount; i++)
            {
                var go = new GameObject($"S{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var size = Random.Range(minSize, maxSize);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(
                    Random.Range(-w * 0.5f, w * 0.5f),
                    Random.Range(-h * 0.5f, h * 0.5f)
                );

                var baseRot = Random.Range(0f, 360f);
                rt.localRotation = Quaternion.Euler(0, 0, baseRot);

                var img = go.GetComponent<Image>();
                img.sprite = starSprite;
                img.raycastTarget = false;
                img.color = starColor;

                _renderers[i] = go.GetComponent<CanvasRenderer>();
                _rects[i] = rt;
                _twinkleSpeed[i] = Random.Range(twinkleSpeedMin, twinkleSpeedMax);
                _twinklePhase[i] = Random.Range(0f, Mathf.PI * 2f);
                _rotSpeed[i] = Random.Range(rotationSpeedMin, rotationSpeedMax);
                _rotPhase[i] = Random.Range(0f, Mathf.PI * 2f);
                _baseRotation[i] = baseRot;
            }
        }

        void Update()
        {
            var t = Time.time;
            var mid = (minAlpha + maxAlpha) * 0.5f;
            var amp = (maxAlpha - minAlpha) * 0.5f;

            // Update half the stars each frame (alternating batches)
            int start = _batchOffset;
            int end = Mathf.Min(start + (starCount + 1) / 2, starCount);

            for (int i = start; i < end; i++)
            {
                // Twinkle via CanvasRenderer.SetAlpha — does NOT dirty Canvas geometry
                var alpha = mid + amp * Mathf.Sin(t * _twinkleSpeed[i] + _twinklePhase[i]);
                _renderers[i].SetAlpha(alpha);

                // Rotation: sin wave oscillation around base angle
                var angle = _baseRotation[i]
                    + rotationRange * Mathf.Sin(t * _rotSpeed[i] + _rotPhase[i]);
                _rects[i].localRotation = Quaternion.Euler(0, 0, angle);
            }

            _batchOffset = end >= starCount ? 0 : end;
        }
    }
}
