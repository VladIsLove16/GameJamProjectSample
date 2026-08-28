using UnityEngine;

namespace RoadOfLife
{
    [CreateAssetMenu(fileName = "RoadAudioLibrary", menuName = "Road of Life/Audio Library")]
    public sealed class RoadAudioLibrary : ScriptableObject
    {
        [Header("SFX")]
        [SerializeField] private AudioClip cardChoiceSound;
        [SerializeField] private AudioClip consequenceSound;
        [SerializeField] private AudioClip upgradeSound;
        [SerializeField] private AudioClip failureSound;
        [SerializeField] private AudioClip victorySound;
        [SerializeField] private AudioClip startEngineSound;

        [Header("Music and ambience")]
        [SerializeField] private AudioClip drivingMusic;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.45f;

        public AudioClip CardChoiceSound => cardChoiceSound;
        public AudioClip ConsequenceSound => consequenceSound;
        public AudioClip UpgradeSound => upgradeSound;
        public AudioClip FailureSound => failureSound;
        public AudioClip VictorySound => victorySound;
        public AudioClip StartEngineSound => startEngineSound;
        public AudioClip DrivingMusic => drivingMusic;
        public float SfxVolume => sfxVolume;
        public float MusicVolume => musicVolume;
    }
}
