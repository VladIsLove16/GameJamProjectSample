using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RoadOfLife
{
    /// <summary>
    /// Presents a card as a physical swipeable Canvas object. The view only reports
    /// a choice after the mandatory off-screen animation has completed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardSwipeView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Scene references")]
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private CanvasGroup cardCanvasGroup;

        [Header("Gesture")]
        [SerializeField, Min(1f)] private float commitThreshold = 150f;
        [SerializeField, Min(0f)] private float previewThreshold = 35f;
        [SerializeField, Min(0f)] private float keyboardPreviewOffset = 70f;
        [SerializeField, Range(0f, 45f)] private float maximumRotation = 13f;
        [SerializeField, Range(0f, 1f)] private float verticalDragInfluence = 0.12f;

        [Header("Animation")]
        [SerializeField, Min(100f)] private float offscreenDistance = 1500f;
        [SerializeField, Min(0.01f)] private float swipeDuration = 0.24f;
        [SerializeField, Min(0.01f)] private float snapBackDuration = 0.16f;

        private Vector2 restingPosition;
        private Quaternion restingRotation;
        private Vector2 dragOffset;
        private Coroutine activeAnimation;
        private ChoiceSide previewSide;
        private bool stateCaptured;
        private bool dragging;
        private bool interactable = true;

        public event Action<ChoiceSide> ChoiceCommitted;
        public event Action<ChoiceSide> PreviewChanged;

        public ChoiceSide CurrentPreview => previewSide;
        public bool IsAnimating => activeAnimation != null;

        private void Awake()
        {
            CaptureRestingState();
            ApplyInteractionState();
        }

        /// <summary>
        /// Assigns the serialized Canvas objects. Intended for the editor scene builder.
        /// </summary>
        public void Configure(RectTransform targetCard, Canvas canvas, CanvasGroup canvasGroup)
        {
            cardRect = targetCard;
            rootCanvas = canvas;
            cardCanvasGroup = canvasGroup;
            stateCaptured = false;
            CaptureRestingState();
            ApplyInteractionState();
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            dragging = false;
            ApplyInteractionState();
        }

        /// <summary>
        /// Immediately places the serialized card object back at its authored position.
        /// </summary>
        public void ResetToCenter()
        {
            StopActiveAnimation();
            CaptureRestingState();
            dragging = false;
            dragOffset = Vector2.zero;
            ApplyPose(restingPosition, restingRotation);
            SetPreview(ChoiceSide.None);
        }

        /// <summary>
        /// Shows controller/keyboard selection without committing it.
        /// </summary>
        public void PreviewChoice(ChoiceSide side)
        {
            if (!interactable || dragging || activeAnimation != null)
            {
                return;
            }

            CaptureRestingState();
            if (side == ChoiceSide.None)
            {
                ApplyPose(restingPosition, restingRotation);
                SetPreview(ChoiceSide.None);
                return;
            }

            float direction = side == ChoiceSide.Left ? -1f : 1f;
            Vector2 position = restingPosition + Vector2.right * keyboardPreviewOffset * direction;
            Quaternion rotation = restingRotation * Quaternion.Euler(0f, 0f, -maximumRotation * 0.45f * direction);
            ApplyPose(position, rotation);
            SetPreview(side);
        }

        /// <summary>
        /// Animates the selected side off-screen, then emits ChoiceCommitted.
        /// Mouse, touch, keyboard and controller all finish through this same path.
        /// </summary>
        public void AnimateChoice(ChoiceSide side)
        {
            if (!interactable || side == ChoiceSide.None || cardRect == null)
            {
                return;
            }

            CaptureRestingState();
            StopActiveAnimation();
            dragging = false;
            interactable = false;
            ApplyInteractionState();
            SetPreview(side);
            activeAnimation = StartCoroutine(AnimateOffscreen(side));
        }

        public void CommitChoice(ChoiceSide side)
        {
            AnimateChoice(side);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanHandle(eventData))
            {
                return;
            }

            StopActiveAnimation();
            CaptureRestingState();
            ApplyPose(restingPosition, restingRotation);
            dragOffset = Vector2.zero;
            dragging = true;
            SetPreview(ChoiceSide.None);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || !interactable || cardRect == null)
            {
                return;
            }

            float canvasScale = rootCanvas != null ? Mathf.Max(0.01f, rootCanvas.scaleFactor) : 1f;
            dragOffset += eventData.delta / canvasScale;

            Vector2 visualOffset = new Vector2(dragOffset.x, dragOffset.y * verticalDragInfluence);
            float normalized = Mathf.Clamp(dragOffset.x / commitThreshold, -1f, 1f);
            Quaternion rotation = restingRotation * Quaternion.Euler(0f, 0f, -normalized * maximumRotation);
            ApplyPose(restingPosition + visualOffset, rotation);
            UpdatePreviewForOffset(dragOffset.x);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            if (Mathf.Abs(dragOffset.x) >= commitThreshold)
            {
                AnimateChoice(dragOffset.x < 0f ? ChoiceSide.Left : ChoiceSide.Right);
                return;
            }

            StopActiveAnimation();
            activeAnimation = StartCoroutine(AnimateSnapBack());
        }

        private bool CanHandle(PointerEventData eventData)
        {
            return interactable && activeAnimation == null && cardRect != null &&
                   eventData != null && eventData.button == PointerEventData.InputButton.Left;
        }

        private IEnumerator AnimateOffscreen(ChoiceSide side)
        {
            Vector2 startPosition = cardRect.anchoredPosition;
            Quaternion startRotation = cardRect.localRotation;
            float direction = side == ChoiceSide.Left ? -1f : 1f;
            Vector2 targetPosition = restingPosition + new Vector2(offscreenDistance * direction, dragOffset.y * verticalDragInfluence);
            Quaternion targetRotation = restingRotation * Quaternion.Euler(0f, 0f, -maximumRotation * 1.55f * direction);

            float elapsed = 0f;
            while (elapsed < swipeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / swipeDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                ApplyPose(Vector2.LerpUnclamped(startPosition, targetPosition, eased),
                    Quaternion.SlerpUnclamped(startRotation, targetRotation, eased));
                yield return null;
            }

            ApplyPose(targetPosition, targetRotation);
            activeAnimation = null;
            ChoiceCommitted?.Invoke(side);
        }

        private IEnumerator AnimateSnapBack()
        {
            Vector2 startPosition = cardRect.anchoredPosition;
            Quaternion startRotation = cardRect.localRotation;
            float elapsed = 0f;

            while (elapsed < snapBackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / snapBackDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                ApplyPose(Vector2.LerpUnclamped(startPosition, restingPosition, eased),
                    Quaternion.SlerpUnclamped(startRotation, restingRotation, eased));
                yield return null;
            }

            ApplyPose(restingPosition, restingRotation);
            dragOffset = Vector2.zero;
            activeAnimation = null;
            SetPreview(ChoiceSide.None);
        }

        private void UpdatePreviewForOffset(float horizontalOffset)
        {
            if (Mathf.Abs(horizontalOffset) < previewThreshold)
            {
                SetPreview(ChoiceSide.None);
            }
            else
            {
                SetPreview(horizontalOffset < 0f ? ChoiceSide.Left : ChoiceSide.Right);
            }
        }

        private void SetPreview(ChoiceSide side)
        {
            if (previewSide == side)
            {
                return;
            }

            previewSide = side;
            PreviewChanged?.Invoke(side);
        }

        private void CaptureRestingState()
        {
            if (stateCaptured)
            {
                return;
            }

            if (cardRect == null)
            {
                cardRect = transform as RectTransform;
            }

            if (cardRect == null)
            {
                return;
            }

            restingPosition = cardRect.anchoredPosition;
            restingRotation = cardRect.localRotation;
            stateCaptured = true;
        }

        private void ApplyPose(Vector2 position, Quaternion rotation)
        {
            if (cardRect == null)
            {
                return;
            }

            cardRect.anchoredPosition = position;
            cardRect.localRotation = rotation;
        }

        private void ApplyInteractionState()
        {
            if (cardCanvasGroup == null)
            {
                return;
            }

            cardCanvasGroup.interactable = interactable;
            cardCanvasGroup.blocksRaycasts = interactable;
        }

        private void StopActiveAnimation()
        {
            if (activeAnimation == null)
            {
                return;
            }

            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }

        private void OnDisable()
        {
            StopActiveAnimation();
            dragging = false;
        }
    }
}
