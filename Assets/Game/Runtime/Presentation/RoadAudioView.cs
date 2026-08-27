using JamStarter;
using UnityEngine;
using Zenject;

namespace RoadOfLife
{
    [DisallowMultipleComponent]
    public sealed class RoadAudioView : MonoBehaviour
    {
        [SerializeField] private AudioClip cardChoiceSound;
        [SerializeField] private AudioClip consequenceSound;
        [SerializeField] private AudioClip upgradeSound;
        [SerializeField] private AudioClip failureSound;
        [SerializeField] private AudioClip victorySound;

        private AudioService audioService;

        [Inject]
        private void Construct(AudioService audioService)
        {
            this.audioService = audioService;
        }

        public void PlayCardChoice() => audioService?.PlaySfx(cardChoiceSound);
        public void PlayConsequence() => audioService?.PlaySfx(consequenceSound);
        public void PlayUpgrade() => audioService?.PlaySfx(upgradeSound);
        public void PlayFailure() => audioService?.PlaySfx(failureSound);
        public void PlayVictory() => audioService?.PlaySfx(victorySound);
    }
}
