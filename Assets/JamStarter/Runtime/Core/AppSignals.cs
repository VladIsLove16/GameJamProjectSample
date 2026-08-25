using Zenject;

namespace JamStarter
{
    public sealed class AppSignalInstaller : Installer<AppSignalInstaller>
    {
        public override void InstallBindings()
        {
            Container.DeclareSignal<AppReadySignal>();
            Container.DeclareSignal<SceneLoadStartedSignal>();
            Container.DeclareSignal<SceneLoadProgressSignal>();
            Container.DeclareSignal<SceneLoadCompletedSignal>();
            Container.DeclareSignal<SceneLoadFailedSignal>();
            Container.DeclareSignal<PauseChangedSignal>();
            Container.DeclareSignal<SettingsChangedSignal>();
        }
    }

    public readonly struct AppReadySignal
    {
        public AppReadySignal(string initialScene)
        {
            InitialScene = initialScene;
        }

        public string InitialScene { get; }
    }

    public readonly struct SceneLoadStartedSignal
    {
        public SceneLoadStartedSignal(string sceneName)
        {
            SceneName = sceneName;
        }

        public string SceneName { get; }
    }

    public readonly struct SceneLoadProgressSignal
    {
        public SceneLoadProgressSignal(string sceneName, float progress)
        {
            SceneName = sceneName;
            Progress = progress;
        }

        public string SceneName { get; }
        public float Progress { get; }
    }

    public readonly struct SceneLoadCompletedSignal
    {
        public SceneLoadCompletedSignal(string sceneName)
        {
            SceneName = sceneName;
        }

        public string SceneName { get; }
    }

    public readonly struct SceneLoadFailedSignal
    {
        public SceneLoadFailedSignal(string sceneName)
        {
            SceneName = sceneName;
        }

        public string SceneName { get; }
    }

    public readonly struct PauseChangedSignal
    {
        public PauseChangedSignal(bool isPaused)
        {
            IsPaused = isPaused;
        }

        public bool IsPaused { get; }
    }

    public readonly struct SettingsChangedSignal
    {
        public SettingsChangedSignal(GameSettingsSnapshot settings)
        {
            Settings = settings;
        }

        public GameSettingsSnapshot Settings { get; }
    }
}
