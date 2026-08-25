using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class SettingsPanel : MonoBehaviour, IAppServicesConsumer
    {
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Slider musicVolume;
        [SerializeField] private Slider sfxVolume;
        [SerializeField] private Slider uiVolume;
        [SerializeField] private Toggle fullscreen;
        [SerializeField] private TMP_Dropdown quality;

        private SettingsService settings;
        private bool suppressCallbacks;
        private bool listenersBound;

        private void Awake()
        {
            BindListeners();
        }

        public void Initialize(AppServices services)
        {
            if (settings == services.Settings)
            {
                Refresh();
                return;
            }

            if (settings != null)
            {
                settings.Changed -= OnSettingsChanged;
            }

            settings = services.Settings;
            settings.Changed += OnSettingsChanged;
            PopulateQualityOptions();
            Refresh();
        }

        public void Commit()
        {
            settings?.Save();
        }

        public void ResetToDefaults()
        {
            settings?.ResetToDefaults();
        }

        private void BindListeners()
        {
            if (listenersBound)
            {
                return;
            }

            ValidateReferences();
            masterVolume.onValueChanged.AddListener(OnMasterVolumeChanged);
            musicVolume.onValueChanged.AddListener(OnMusicVolumeChanged);
            sfxVolume.onValueChanged.AddListener(OnSfxVolumeChanged);
            uiVolume.onValueChanged.AddListener(OnUiVolumeChanged);
            fullscreen.onValueChanged.AddListener(OnFullscreenChanged);
            quality.onValueChanged.AddListener(OnQualityChanged);
            listenersBound = true;
        }

        private void PopulateQualityOptions()
        {
            suppressCallbacks = true;
            quality.ClearOptions();
            quality.AddOptions(new List<string>(settings.QualityNames));
            suppressCallbacks = false;
        }

        private void Refresh()
        {
            if (settings == null)
            {
                return;
            }

            GameSettingsSnapshot snapshot = settings.Current;
            suppressCallbacks = true;
            masterVolume.SetValueWithoutNotify(snapshot.MasterVolume);
            musicVolume.SetValueWithoutNotify(snapshot.MusicVolume);
            sfxVolume.SetValueWithoutNotify(snapshot.SfxVolume);
            uiVolume.SetValueWithoutNotify(snapshot.UiVolume);
            fullscreen.SetIsOnWithoutNotify(snapshot.Fullscreen);
            quality.SetValueWithoutNotify(snapshot.QualityLevel);
            quality.RefreshShownValue();
            suppressCallbacks = false;
        }

        private void OnSettingsChanged(GameSettingsSnapshot snapshot)
        {
            Refresh();
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (!suppressCallbacks) settings?.SetMasterVolume(value);
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (!suppressCallbacks) settings?.SetMusicVolume(value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (!suppressCallbacks) settings?.SetSfxVolume(value);
        }

        private void OnUiVolumeChanged(float value)
        {
            if (!suppressCallbacks) settings?.SetUiVolume(value);
        }

        private void OnFullscreenChanged(bool value)
        {
            if (!suppressCallbacks) settings?.SetFullscreen(value);
        }

        private void OnQualityChanged(int value)
        {
            if (!suppressCallbacks) settings?.SetQualityLevel(value);
        }

        private void ValidateReferences()
        {
            if (masterVolume == null || musicVolume == null || sfxVolume == null || uiVolume == null ||
                fullscreen == null || quality == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(SettingsPanel)} on '{name}' requires references to all controls.");
            }
        }

        private void OnDestroy()
        {
            if (settings != null)
            {
                settings.Changed -= OnSettingsChanged;
            }
        }
    }
}
