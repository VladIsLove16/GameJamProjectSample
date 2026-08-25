using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamStarter
{
    /// <summary>
    /// Persistent composition root. It owns application services and injects them
    /// into scene components after each load, avoiding global service access.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class AppBootstrap : MonoBehaviour
    {
        [Header("Persistent services")]
        [SerializeField] private InputReader input;
        [SerializeField] private GamePauseService pause;
        [SerializeField] private SceneLoader scenes;
        [SerializeField] private AudioService audioService;
        [SerializeField] private SettingsService settings;

        [Header("Startup")]
        [SerializeField] private string firstScene = SceneNames.MainMenu;

        private static AppBootstrap activeInstance;

        private AppServices services;

        private void Awake()
        {
            if (activeInstance != null && activeInstance != this)
            {
                Debug.LogWarning("Duplicate application root detected. The duplicate will be destroyed.", this);
                Destroy(gameObject);
                return;
            }

            ValidateReferences();
            activeInstance = this;
            DontDestroyOnLoad(gameObject);
            services = new AppServices(input, pause, scenes, audioService, settings);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            settings.Initialize();
            input.UseUI();
            Inject(SceneManager.GetActiveScene());

            if (SceneManager.GetActiveScene().name == SceneNames.Bootstrap &&
                !string.IsNullOrWhiteSpace(firstScene))
            {
                scenes.LoadScene(firstScene);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Inject(scene);
        }

        private void Inject(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || services == null)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    if (behaviours[behaviourIndex] is IAppServicesConsumer consumer)
                    {
                        consumer.Initialize(services);
                    }
                }
            }
        }

        private void ValidateReferences()
        {
            if (input == null || pause == null || scenes == null || audioService == null || settings == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(AppBootstrap)} on '{name}' requires references to all persistent services.");
            }
        }

        private void OnDestroy()
        {
            if (activeInstance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            activeInstance = null;
        }
    }
}
