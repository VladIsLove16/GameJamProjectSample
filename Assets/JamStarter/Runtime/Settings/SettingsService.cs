using System;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class SettingsService : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;
        public const string PlayerPrefsKey = "JamStarter.Settings.v1";

        private const float MutedDecibels = -80f;

        [Header("Defaults")]
        [SerializeField] private GameSettings defaults = new();

        [Header("Audio mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParameter = "MasterVolume";
        [SerializeField] private string musicVolumeParameter = "MusicVolume";
        [SerializeField] private string sfxVolumeParameter = "SfxVolume";
        [SerializeField] private string uiVolumeParameter = "UiVolume";

        private GameSettings current;

        private SignalBus signalBus;

        public bool IsInitialized { get; private set; }
        public GameSettingsSnapshot Current => new(current ?? defaults);
        public string[] QualityNames => QualitySettings.names;

        [Inject]
        private void Construct(SignalBus events)
        {
            signalBus = events;
        }

        private void Awake()
        {
            current = Sanitize(defaults.Clone());
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            current = LoadOrDefault();
            IsInitialized = true;
            ApplyAll();
            PublishChanged();
        }

        public void SetMasterVolume(float value) => SetVolume(ref current.masterVolume, value, masterVolumeParameter);
        public void SetMusicVolume(float value) => SetVolume(ref current.musicVolume, value, musicVolumeParameter);
        public void SetSfxVolume(float value) => SetVolume(ref current.sfxVolume, value, sfxVolumeParameter);
        public void SetUiVolume(float value) => SetVolume(ref current.uiVolume, value, uiVolumeParameter);

        public void SetFullscreen(bool value)
        {
            EnsureInitialized();
            if (current.fullscreen == value)
            {
                return;
            }

            current.fullscreen = value;
#if !UNITY_WEBGL
            Screen.fullScreen = value;
#endif
            PublishChanged();
        }

        public void SetQualityLevel(int value)
        {
            EnsureInitialized();
            int clamped = ClampQualityLevel(value);
            if (current.qualityLevel == clamped)
            {
                return;
            }

            current.qualityLevel = clamped;
            QualitySettings.SetQualityLevel(clamped, true);
            PublishChanged();
        }

        public void Save()
        {
            EnsureInitialized();
            current.schemaVersion = CurrentSchemaVersion;
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(current));
            PlayerPrefs.Save();
        }

        public void ResetToDefaults()
        {
            EnsureInitialized();
            current = Sanitize(defaults.Clone());
            ApplyAll();
            Save();
            PublishChanged();
        }

        public void ClearSavedSettings()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
            current = Sanitize(defaults.Clone());

            if (IsInitialized)
            {
                ApplyAll();
                PublishChanged();
            }
        }

        private GameSettings LoadOrDefault()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return Sanitize(defaults.Clone());
            }

            try
            {
                string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                GameSettings loaded = JsonUtility.FromJson<GameSettings>(json);
                if (loaded == null || loaded.schemaVersion != CurrentSchemaVersion)
                {
                    return Sanitize(defaults.Clone());
                }

                return Sanitize(loaded);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read saved settings. Defaults will be used. {exception.Message}", this);
                return Sanitize(defaults.Clone());
            }
        }

        private void SetVolume(ref float destination, float value, string parameter)
        {
            EnsureInitialized();
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(destination, clamped))
            {
                return;
            }

            destination = clamped;
            ApplyMixerVolume(parameter, clamped);
            PublishChanged();
        }

        private void ApplyAll()
        {
            ApplyMixerVolume(masterVolumeParameter, current.masterVolume);
            ApplyMixerVolume(musicVolumeParameter, current.musicVolume);
            ApplyMixerVolume(sfxVolumeParameter, current.sfxVolume);
            ApplyMixerVolume(uiVolumeParameter, current.uiVolume);

#if !UNITY_WEBGL
            Screen.fullScreen = current.fullscreen;
#endif
            QualitySettings.SetQualityLevel(current.qualityLevel, true);
        }

        private void PublishChanged()
        {
            signalBus?.Fire(new SettingsChangedSignal(Current));
        }

        private void ApplyMixerVolume(string parameter, float linearVolume)
        {
            if (audioMixer == null || string.IsNullOrWhiteSpace(parameter))
            {
                return;
            }

            float decibels = linearVolume <= 0.0001f
                ? MutedDecibels
                : Mathf.Log10(linearVolume) * 20f;

            if (!audioMixer.SetFloat(parameter, decibels))
            {
                Debug.LogWarning($"AudioMixer parameter '{parameter}' is not exposed.", audioMixer);
            }
        }

        private GameSettings Sanitize(GameSettings settings)
        {
            settings.schemaVersion = CurrentSchemaVersion;
            settings.masterVolume = Mathf.Clamp01(settings.masterVolume);
            settings.musicVolume = Mathf.Clamp01(settings.musicVolume);
            settings.sfxVolume = Mathf.Clamp01(settings.sfxVolume);
            settings.uiVolume = Mathf.Clamp01(settings.uiVolume);
            settings.qualityLevel = settings.qualityLevel < 0
                ? QualitySettings.GetQualityLevel()
                : ClampQualityLevel(settings.qualityLevel);
            return settings;
        }

        private static int ClampQualityLevel(int value)
        {
            return Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                Initialize();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && IsInitialized)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            if (IsInitialized)
            {
                Save();
            }
        }
    }
}
