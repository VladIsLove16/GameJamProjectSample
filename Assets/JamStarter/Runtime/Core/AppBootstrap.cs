using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace JamStarter
{
    /// <summary>
    /// ProjectContext installer for application-lifetime services and signals.
    /// Scene objects are injected by their SceneContext; this class only describes
    /// the object graph and contains no runtime lookup or global accessor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppBootstrap : MonoInstaller
    {
        [Header("Persistent services")]
        [SerializeField] private InputReader input;
        [SerializeField] private GamePauseService pause;
        [SerializeField] private SceneLoader scenes;
        [SerializeField] private AudioService audioService;
        [SerializeField] private SettingsService settings;

        [Header("Startup")]
        [SerializeField] private string firstScene = SceneNames.MainMenu;

        public override void InstallBindings()
        {
            ValidateReferences();
            SignalBusInstaller.Install(Container);
            AppSignalInstaller.Install(Container);

            Container.BindInstance(input);
            Container.BindInstance(pause);
            Container.BindInstance(scenes);
            Container.BindInstance(audioService);
            Container.BindInstance(settings);
            Container.BindInstance(new AppStartupSettings(firstScene));
            Container.BindInterfacesTo<AppStartup>().AsSingle();
        }

        private void ValidateReferences()
        {
            if (input == null || pause == null || scenes == null || audioService == null || settings == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(AppBootstrap)} on '{name}' requires references to all persistent services.");
            }
        }

    }

    internal sealed class AppStartupSettings
    {
        public AppStartupSettings(string firstScene)
        {
            FirstScene = firstScene;
        }

        public string FirstScene { get; }
    }

    internal sealed class AppStartup : IInitializable
    {
        private readonly InputReader input;
        private readonly SceneLoader scenes;
        private readonly SettingsService settings;
        private readonly SignalBus signalBus;
        private readonly AppStartupSettings startupSettings;

        public AppStartup(
            InputReader input,
            SceneLoader scenes,
            SettingsService settings,
            SignalBus signalBus,
            AppStartupSettings startupSettings)
        {
            this.input = input;
            this.scenes = scenes;
            this.settings = settings;
            this.signalBus = signalBus;
            this.startupSettings = startupSettings;
        }

        public void Initialize()
        {
            settings.Initialize();
            input.UseUI();

            Scene activeScene = SceneManager.GetActiveScene();
            signalBus.Fire(new AppReadySignal(activeScene.name));

            if (activeScene.name == SceneNames.Bootstrap &&
                !string.IsNullOrWhiteSpace(startupSettings.FirstScene))
            {
                scenes.LoadScene(startupSettings.FirstScene);
            }
        }
    }
}
