using System;

namespace JamStarter
{
    /// <summary>
    /// Immutable application-level dependency bundle. It deliberately has no static
    /// accessor: scene code receives it from the composition root.
    /// </summary>
    public sealed class AppServices
    {
        public AppServices(
            InputReader input,
            GamePauseService pause,
            SceneLoader scenes,
            AudioService audio,
            SettingsService settings)
        {
            Input = input != null ? input : throw new ArgumentNullException(nameof(input));
            Pause = pause != null ? pause : throw new ArgumentNullException(nameof(pause));
            Scenes = scenes != null ? scenes : throw new ArgumentNullException(nameof(scenes));
            Audio = audio != null ? audio : throw new ArgumentNullException(nameof(audio));
            Settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

        public InputReader Input { get; }
        public GamePauseService Pause { get; }
        public SceneLoader Scenes { get; }
        public AudioService Audio { get; }
        public SettingsService Settings { get; }
    }
}
