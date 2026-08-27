using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoadOfLife
{
    /// <summary>
    /// Renders a 0..100 resource where either edge is dangerous.
    /// All visual elements are authored and wired on the Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BipolarStatView : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private TMP_Text statLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private RectTransform valueMarker;
        [SerializeField] private Image fillImage;
        [SerializeField] private Graphic markerGraphic;
        [SerializeField] private Graphic lowDangerGraphic;
        [SerializeField] private Graphic highDangerGraphic;

        [Header("Presentation")]
        [SerializeField] private bool showExactValue = true;
        [SerializeField, Range(1, 49)] private int dangerEdge = 15;
        [SerializeField, Min(0.01f)] private float changeDuration = 0.28f;
        [SerializeField] private Color normalColor = new Color(0.82f, 0.87f, 0.82f, 1f);
        [SerializeField] private Color dangerColor = new Color(0.83f, 0.22f, 0.16f, 1f);
        [SerializeField] private Color dangerZoneIdleColor = new Color(0.48f, 0.12f, 0.09f, 0.28f);

        private Coroutine valueAnimation;
        private float displayedValue = 50f;
        private int targetValue = 50;

        public int Value => targetValue;

        public void SetExactValueVisible(bool visible)
        {
            showExactValue = visible;
            if (valueLabel != null)
            {
                valueLabel.gameObject.SetActive(visible);
            }
        }

        private void Awake()
        {
            RenderValue(displayedValue);
        }

        /// <summary>
        /// Assigns every serialized visual used by the component. Intended for the
        /// editor scene builder after it creates real Canvas objects.
        /// </summary>
        public void Configure(
            string displayName,
            TMP_Text label,
            TMP_Text number,
            RectTransform marker,
            Image fill,
            Graphic markerTint,
            Graphic lowDangerZone,
            Graphic highDangerZone)
        {
            statLabel = label;
            valueLabel = number;
            valueMarker = marker;
            fillImage = fill;
            markerGraphic = markerTint;
            lowDangerGraphic = lowDangerZone;
            highDangerGraphic = highDangerZone;

            if (statLabel != null)
            {
                statLabel.text = displayName ?? string.Empty;
            }

            RenderValue(displayedValue);
        }

        public void SetDisplayName(string displayName)
        {
            if (statLabel != null)
            {
                statLabel.text = displayName ?? string.Empty;
            }
        }

        public void SetValue(int value, bool animate = true)
        {
            targetValue = Mathf.Clamp(value, 0, 100);
            StopValueAnimation();

            if (!animate || !isActiveAndEnabled || changeDuration <= 0.01f)
            {
                displayedValue = targetValue;
                RenderValue(displayedValue);
                return;
            }

            valueAnimation = StartCoroutine(AnimateValue(displayedValue, targetValue));
        }

        public void SetDangerEdge(int edge)
        {
            dangerEdge = Mathf.Clamp(edge, 1, 49);
            RenderValue(displayedValue);
        }

        private IEnumerator AnimateValue(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < changeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / changeDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                displayedValue = Mathf.LerpUnclamped(from, to, eased);
                RenderValue(displayedValue);
                yield return null;
            }

            displayedValue = to;
            RenderValue(displayedValue);
            valueAnimation = null;
        }

        private void RenderValue(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, 100f);
            float normalized = clamped / 100f;

            if (valueLabel != null)
            {
                valueLabel.text = Mathf.RoundToInt(clamped).ToString();
                valueLabel.gameObject.SetActive(showExactValue);
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = normalized;
            }

            if (valueMarker != null)
            {
                Vector2 anchorMin = valueMarker.anchorMin;
                Vector2 anchorMax = valueMarker.anchorMax;
                anchorMin.x = normalized;
                anchorMax.x = normalized;
                valueMarker.anchorMin = anchorMin;
                valueMarker.anchorMax = anchorMax;

                Vector2 anchoredPosition = valueMarker.anchoredPosition;
                anchoredPosition.x = 0f;
                valueMarker.anchoredPosition = anchoredPosition;
            }

            float lowDanger = clamped < dangerEdge ? 1f - clamped / dangerEdge : 0f;
            float highDanger = clamped > 100f - dangerEdge
                ? (clamped - (100f - dangerEdge)) / dangerEdge
                : 0f;
            float danger = Mathf.Max(lowDanger, highDanger);

            if (markerGraphic != null)
            {
                markerGraphic.color = Color.Lerp(normalColor, dangerColor, danger);
            }

            SetDangerZoneColor(lowDangerGraphic, lowDanger);
            SetDangerZoneColor(highDangerGraphic, highDanger);
        }

        private void SetDangerZoneColor(Graphic graphic, float intensity)
        {
            if (graphic == null)
            {
                return;
            }

            Color color = Color.Lerp(dangerZoneIdleColor, dangerColor, Mathf.Clamp01(intensity));
            graphic.color = color;
        }

        private void StopValueAnimation()
        {
            if (valueAnimation == null)
            {
                return;
            }

            StopCoroutine(valueAnimation);
            valueAnimation = null;
        }

        private void OnDisable()
        {
            StopValueAnimation();
        }
    }
}
