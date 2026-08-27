using UnityEngine;

namespace RoadOfLife
{
    [DisallowMultipleComponent]
    public sealed class RoadTutorialTestController : MonoBehaviour
    {
        [SerializeField] private JamStarter.IntroSequenceView tutorial;

        private void Start()
        {
            ShowTutorial();
        }

        public void ShowTutorial()
        {
            tutorial?.Show();
        }
    }
}
