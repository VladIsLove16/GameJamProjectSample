using System;
using UnityEngine;
using UnityEngine.UI;

namespace RoadOfLife
{
    [DisallowMultipleComponent]
    public sealed class RoadLayeredBackground : MonoBehaviour
    {
        [SerializeField] private Layer[] layers = Array.Empty<Layer>();
        [SerializeField] private bool animateLayers = true;

        public void SetSprite(int index, Sprite sprite)
        {
            if (layers == null || index < 0 || index >= layers.Length || layers[index] == null)
            {
                return;
            }

            layers[index].SetSprite(sprite);
        }

        private void Update()
        {
            if (!animateLayers || layers == null)
            {
                return;
            }

            foreach (Layer layer in layers)
            {
                layer?.Tick(Time.unscaledDeltaTime);
            }
        }

        [Serializable]
        public sealed class Layer
        {
            [SerializeField] private Image image;
            [SerializeField] private RectTransform rectTransform;
            [SerializeField] private float horizontalSpeed;
            [SerializeField] private float verticalSpeed;
            [SerializeField] private bool wrapHorizontal = true;
            [SerializeField] private bool wrapVertical;
            [SerializeField] private float wrapDistance = 1920f;

            public void SetSprite(Sprite sprite)
            {
                if (image != null)
                {
                    image.sprite = sprite;
                    image.preserveAspect = true;
                }
            }

            public void Tick(float deltaTime)
            {
                if (rectTransform == null || (Mathf.Approximately(horizontalSpeed, 0f) &&
                    Mathf.Approximately(verticalSpeed, 0f)))
                {
                    return;
                }

                Vector2 position = rectTransform.anchoredPosition;
                position.x += horizontalSpeed * deltaTime;
                position.y += verticalSpeed * deltaTime;
                if (wrapHorizontal && Mathf.Abs(position.x) > wrapDistance)
                {
                    position.x = 0f;
                }

                if (wrapVertical && Mathf.Abs(position.y) > wrapDistance)
                {
                    position.y = 0f;
                }

                rectTransform.anchoredPosition = position;
            }
        }
    }
}
