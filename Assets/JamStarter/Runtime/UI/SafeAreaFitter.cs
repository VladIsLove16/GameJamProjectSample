using UnityEngine;

namespace JamStarter
{
    /// <summary>Fits a referenced RectTransform to the current device safe area.</summary>
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform target;

        private Rect lastSafeArea;
        private Vector2Int lastResolution;

        private void OnEnable()
        {
            Apply(true);
        }

        private void Update()
        {
            Apply(false);
        }

        public void Refresh()
        {
            Apply(true);
        }

        private void Apply(bool force)
        {
            if (target == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == lastSafeArea && resolution == lastResolution)
            {
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastResolution = resolution;
        }
    }
}
