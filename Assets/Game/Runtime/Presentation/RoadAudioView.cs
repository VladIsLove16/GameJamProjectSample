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
        [SerializeField] private AudioClip startEngineSound;
        [SerializeField] private AudioClip drivingAmbience;
        [SerializeField] private AudioClip radioMusic;
        [SerializeField] private bool playAudioOnStart = true;

        private AudioService audioService;
        private bool started;

        [Inject]
        private void Construct(AudioService audioService)
        {
            this.audioService = audioService;
        }

        private void Start()
        {
            if (!playAudioOnStart || started)
            {
                return;
            }

            started = true;
            audioService?.PlaySfx(startEngineSound);

            AudioClip background = drivingAmbience != null ? drivingAmbience : radioMusic;
            if (background != null)
            {
                audioService?.PlayMusic(background, true, 0.2f);
            }
        }

        public void PlayCardChoice() => audioService?.PlaySfx(cardChoiceSound);
        public void PlayConsequence() => audioService?.PlaySfx(consequenceSound);
        public void PlayUpgrade() => audioService?.PlaySfx(upgradeSound);
        public void PlayFailure() => audioService?.PlaySfx(failureSound);
        public void PlayVictory() => audioService?.PlaySfx(victorySound);

#if UNITY_EDITOR
        private void OnValidate()
        {
            cardChoiceSound = LoadDefault(cardChoiceSound, "Assets/Sprites/Game/click.wav");
            consequenceSound = LoadDefault(consequenceSound, "Assets/Sprites/Game/smena-volna.ogg");
            upgradeSound = LoadDefault(upgradeSound, "Assets/Sprites/Game/radio-rech3.ogg");
            failureSound = LoadDefault(failureSound, "Assets/Sprites/Game/smena-volna.ogg");
            victorySound = LoadDefault(victorySound, "Assets/Sprites/Game/music-radio-3.ogg");
            startEngineSound = LoadDefault(startEngineSound, "Assets/Sprites/Game/zapuck-engine.ogg");
            drivingAmbience = LoadDefault(drivingAmbience, "Assets/Sprites/Game/engine-edet.ogg");
            radioMusic = LoadDefault(radioMusic, "Assets/Sprites/Game/music-radio-2.ogg");
        }

        private static AudioClip LoadDefault(AudioClip current, string path)
        {
            return current != null ? current : UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
#endif
    }
}
