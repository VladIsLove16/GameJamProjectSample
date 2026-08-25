using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace JamStarter
{
    /// <summary>
    /// Persistent audio facade with separate Music, SFX and UI routing. One-shot
    /// emitters are pooled and the oldest voice is stolen when the configured limit
    /// is reached, keeping memory and AudioSource counts bounded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        [Header("Mixer routing")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;

        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private Transform oneShotRoot;
        [SerializeField, Min(1)] private int maxConcurrentVoices = 24;
        [SerializeField] private int randomSeed = 7319;

        private readonly Dictionary<AudioCue, int> previousCueIndices = new();
        private readonly Dictionary<AudioSource, int> voiceVersions = new();
        private readonly List<AudioSource> activeVoices = new();

        private ComponentPool<AudioSource> voicePool;
        private SeededRandom random;
        private Coroutine musicRoutine;
        private int nextVoiceVersion;

        public AudioMixer Mixer => audioMixer;
        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

        private void Awake()
        {
            if (musicSource == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(AudioService)} on '{name}' requires a music AudioSource reference.");
            }

            random = new SeededRandom(randomSeed);
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.outputAudioMixerGroup = musicGroup;

            voicePool = new ComponentPool<AudioSource>(
                CreateVoice,
                oneShotRoot != null ? oneShotRoot : transform,
                ResetVoiceForUse,
                ResetVoiceForPool,
                defaultCapacity: Mathf.Min(8, maxConcurrentVoices),
                maxSize: maxConcurrentVoices);
        }

        public void PlayMusic(AudioClip clip)
        {
            PlayMusic(clip, true, 0.25f);
        }

        public void PlayMusic(AudioClip clip, bool loop, float fadeSeconds)
        {
            if (clip == null)
            {
                return;
            }

            if (musicRoutine != null)
            {
                StopCoroutine(musicRoutine);
            }

            musicRoutine = StartCoroutine(ChangeMusicRoutine(clip, loop, Mathf.Max(0f, fadeSeconds)));
        }

        public void StopMusic(float fadeSeconds = 0.2f)
        {
            if (musicRoutine != null)
            {
                StopCoroutine(musicRoutine);
            }

            musicRoutine = StartCoroutine(StopMusicRoutine(Mathf.Max(0f, fadeSeconds)));
        }

        public bool PlaySfx(AudioCue cue)
        {
            return PlayCue(cue, sfxGroup, null);
        }

        public bool PlaySfx(AudioCue cue, Vector3 position)
        {
            return PlayCue(cue, sfxGroup, position);
        }

        public bool PlayUi(AudioCue cue)
        {
            return PlayCue(cue, uiGroup, null);
        }

        public bool PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            return PlayOneShot(clip, sfxGroup, null, volume, pitch, 0f);
        }

        public bool PlaySfxAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            return PlayOneShot(clip, sfxGroup, position, volume, pitch, 1f);
        }

        public bool PlayUi(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            return PlayOneShot(clip, uiGroup, null, volume, pitch, 0f);
        }

        public void StopAllOneShots()
        {
            while (activeVoices.Count > 0)
            {
                ReleaseVoice(activeVoices[0]);
            }
        }

        private bool PlayCue(AudioCue cue, AudioMixerGroup group, Vector3? position)
        {
            if (cue == null)
            {
                return false;
            }

            previousCueIndices.TryGetValue(cue, out int previousIndex);
            if (!cue.TrySample(random, ref previousIndex, out AudioCueSample sample))
            {
                Debug.LogWarning($"Audio cue '{cue.name}' contains no playable clips.", cue);
                return false;
            }

            previousCueIndices[cue] = previousIndex;
            return PlayOneShot(
                sample.Clip,
                group,
                position,
                sample.Volume,
                sample.Pitch,
                position.HasValue ? Mathf.Max(sample.SpatialBlend, 0.01f) : 0f);
        }

        private bool PlayOneShot(
            AudioClip clip,
            AudioMixerGroup group,
            Vector3? position,
            float volume,
            float pitch,
            float spatialBlend)
        {
            if (clip == null || voicePool == null)
            {
                return false;
            }

            if (activeVoices.Count >= maxConcurrentVoices)
            {
                ReleaseVoice(activeVoices[0]);
            }

            AudioSource source = voicePool.Get();
            source.outputAudioMixerGroup = group;
            source.transform.position = position ?? transform.position;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, -3f, 3f);
            if (Mathf.Approximately(source.pitch, 0f))
            {
                source.pitch = 1f;
            }

            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.clip = clip;
            source.Play();

            int version = ++nextVoiceVersion;
            voiceVersions[source] = version;
            activeVoices.Add(source);

            float duration = clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            StartCoroutine(ReleaseVoiceAfter(source, version, duration + 0.05f));
            return true;
        }

        private AudioSource CreateVoice()
        {
            var voiceObject = new GameObject("OneShot Voice");
            var source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            return source;
        }

        private static void ResetVoiceForUse(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
        }

        private static void ResetVoiceForPool(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.outputAudioMixerGroup = null;
            source.volume = 1f;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.transform.localPosition = Vector3.zero;
        }

        private IEnumerator ReleaseVoiceAfter(AudioSource source, int version, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!voiceVersions.TryGetValue(source, out int currentVersion) || currentVersion != version)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (voiceVersions.TryGetValue(source, out int finalVersion) && finalVersion == version)
            {
                ReleaseVoice(source);
            }
        }

        private void ReleaseVoice(AudioSource source)
        {
            if (source == null || !voiceVersions.Remove(source))
            {
                return;
            }

            source.Stop();
            activeVoices.Remove(source);
            voicePool.Release(source);
        }

        private IEnumerator ChangeMusicRoutine(AudioClip clip, bool loop, float fadeSeconds)
        {
            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                musicSource.loop = loop;
                yield return FadeMusicVolume(1f, fadeSeconds);
                musicRoutine = null;
                yield break;
            }

            if (musicSource.isPlaying)
            {
                yield return FadeMusicVolume(0f, fadeSeconds * 0.5f);
            }

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = 0f;
            musicSource.Play();
            yield return FadeMusicVolume(1f, fadeSeconds * 0.5f);
            musicRoutine = null;
        }

        private IEnumerator StopMusicRoutine(float fadeSeconds)
        {
            yield return FadeMusicVolume(0f, fadeSeconds);
            musicSource.Stop();
            musicSource.clip = null;
            musicSource.volume = 1f;
            musicRoutine = null;
        }

        private IEnumerator FadeMusicVolume(float target, float duration)
        {
            float start = musicSource.volume;
            if (duration <= 0f)
            {
                musicSource.volume = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            musicSource.volume = target;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            if (voicePool != null)
            {
                StopAllOneShots();
                voicePool.Dispose();
                voicePool = null;
            }
        }
    }
}
