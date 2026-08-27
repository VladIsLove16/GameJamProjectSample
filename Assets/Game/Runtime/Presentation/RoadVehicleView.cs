using UnityEngine;

namespace RoadOfLife
{
    [DisallowMultipleComponent]
    public sealed class RoadVehicleView : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private string tempoParameter = "Tempo";
        [SerializeField] private string engineParameter = "Engine";
        [SerializeField] private string visibilityParameter = "Visibility";
        [SerializeField] private string loadParameter = "Load";
        [SerializeField] private string tripTrigger = "Trip";
        [SerializeField] private string eventTrigger = "Event";

        public void SetStats(StatSnapshot stats)
        {
            SetFloat(tempoParameter, stats.Tempo / 100f);
            SetFloat(engineParameter, stats.Engine / 100f);
            SetFloat(visibilityParameter, stats.Visibility / 100f);
            SetFloat(loadParameter, stats.Load / 100f);
        }

        public void PlayTripAnimation()
        {
            if (animator != null && !string.IsNullOrWhiteSpace(tripTrigger))
            {
                animator.SetTrigger(tripTrigger);
            }
        }

        public void PlayEventAnimation()
        {
            if (animator != null && !string.IsNullOrWhiteSpace(eventTrigger))
            {
                animator.SetTrigger(eventTrigger);
            }
        }

        public void SetModelVisible(bool visible)
        {
            if (modelRoot != null)
            {
                modelRoot.gameObject.SetActive(visible);
            }
        }

        private void SetFloat(string parameter, float value)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(parameter))
            {
                animator.SetFloat(parameter, Mathf.Clamp01(value));
            }
        }
    }
}
