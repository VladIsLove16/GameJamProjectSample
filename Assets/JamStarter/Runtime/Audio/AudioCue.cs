using System;
using UnityEngine;

namespace JamStarter
{
    public readonly struct AudioCueSample
    {
        public AudioCueSample(AudioClip clip, float volume, float pitch, float spatialBlend)
        {
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
            SpatialBlend = spatialBlend;
        }

        public AudioClip Clip { get; }
        public float Volume { get; }
        public float Pitch { get; }
        public float SpatialBlend { get; }
    }

    [CreateAssetMenu(fileName = "AudioCue", menuName = "Jam Starter/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [SerializeField] private Vector2 volumeRange = Vector2.one;
        [SerializeField] private Vector2 pitchRange = new(0.97f, 1.03f);
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField] private bool avoidImmediateRepeat = true;

        public bool TrySample(SeededRandom random, ref int previousIndex, out AudioCueSample sample)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            int validCount = CountValidClips();
            if (validCount == 0)
            {
                sample = default;
                return false;
            }

            int selection = random.Range(0, validCount);
            int selectedIndex = GetValidClipIndex(selection);

            if (avoidImmediateRepeat && validCount > 1 && selectedIndex == previousIndex)
            {
                selection = (selection + 1 + random.Range(0, validCount - 1)) % validCount;
                selectedIndex = GetValidClipIndex(selection);
            }

            previousIndex = selectedIndex;
            sample = new AudioCueSample(
                clips[selectedIndex],
                random.Range(volumeRange.x, volumeRange.y),
                random.Range(pitchRange.x, pitchRange.y),
                spatialBlend);
            return true;
        }

        private int CountValidClips()
        {
            int count = 0;
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetValidClipIndex(int validIndex)
        {
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null)
                {
                    continue;
                }

                if (validIndex == 0)
                {
                    return index;
                }

                validIndex--;
            }

            throw new InvalidOperationException("Audio cue contains no clip at the requested index.");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            volumeRange.x = Mathf.Clamp01(volumeRange.x);
            volumeRange.y = Mathf.Clamp01(volumeRange.y);
            if (volumeRange.x > volumeRange.y)
            {
                (volumeRange.x, volumeRange.y) = (volumeRange.y, volumeRange.x);
            }

            pitchRange.x = Mathf.Clamp(pitchRange.x, -3f, 3f);
            pitchRange.y = Mathf.Clamp(pitchRange.y, -3f, 3f);
            if (pitchRange.x > pitchRange.y)
            {
                (pitchRange.x, pitchRange.y) = (pitchRange.y, pitchRange.x);
            }

            if (Mathf.Approximately(pitchRange.x, 0f) && Mathf.Approximately(pitchRange.y, 0f))
            {
                pitchRange = Vector2.one;
            }
        }
#endif
    }
}
