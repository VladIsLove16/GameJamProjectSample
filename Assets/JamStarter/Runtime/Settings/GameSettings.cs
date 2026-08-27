using System;

namespace JamStarter
{
    [Serializable]
    public sealed class GameSettings
    {
        public int schemaVersion = SettingsService.CurrentSchemaVersion;
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float uiVolume = 1f;
        public bool fullscreen = true;
        public bool showExactStats;
        public bool showChoiceHints;
        public bool introSeen;
        public int qualityLevel = -1;

        public GameSettings Clone()
        {
            return new GameSettings
            {
                schemaVersion = schemaVersion,
                masterVolume = masterVolume,
                musicVolume = musicVolume,
                sfxVolume = sfxVolume,
                uiVolume = uiVolume,
                fullscreen = fullscreen,
                showExactStats = showExactStats,
                showChoiceHints = showChoiceHints,
                introSeen = introSeen,
                qualityLevel = qualityLevel,
            };
        }
    }

    public readonly struct GameSettingsSnapshot
    {
        public GameSettingsSnapshot(GameSettings source)
        {
            MasterVolume = source.masterVolume;
            MusicVolume = source.musicVolume;
            SfxVolume = source.sfxVolume;
            UiVolume = source.uiVolume;
            Fullscreen = source.fullscreen;
            ShowExactStats = source.showExactStats;
            ShowChoiceHints = source.showChoiceHints;
            IntroSeen = source.introSeen;
            QualityLevel = source.qualityLevel;
        }

        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
        public float UiVolume { get; }
        public bool Fullscreen { get; }
        public bool ShowExactStats { get; }
        public bool ShowChoiceHints { get; }
        public bool IntroSeen { get; }
        public int QualityLevel { get; }
    }
}
