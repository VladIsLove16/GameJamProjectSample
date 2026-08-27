using UnityEngine;
using Zenject;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string ShowIntroNextLaunchKey = "RoadOfLife.ShowIntroNextLaunch";
        [SerializeField] private UIScreen mainScreen;
        [SerializeField] private UIScreen settingsScreen;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private IntroSequenceView introSequence;
    #if UNITY_EDITOR
        [Header("Editor testing")]
        [SerializeField] private bool alwaysShowIntroInEditor;
    #endif

        private InputReader input;
        private GamePauseService pause;
        private SceneLoader scenes;
        private SettingsService settings;

        private void Start()
        {
            settingsScreen.Hide();
            mainScreen.Show();
            if (introSequence != null)
            {
                introSequence.Completed += OnIntroCompleted;
                bool showIntro = !settings.Current.IntroSeen;
                if (PlayerPrefs.GetInt(ShowIntroNextLaunchKey, 0) == 1)
                {
                    PlayerPrefs.DeleteKey(ShowIntroNextLaunchKey);
                    PlayerPrefs.Save();
                    showIntro = true;
                }

                if (showIntro
#if UNITY_EDITOR
                          || alwaysShowIntroInEditor
#endif
                         )
                {
                    OpenIntro();
                }
            }
        }

        [Inject]
        private void Construct(
            InputReader inputReader,
            GamePauseService pauseService,
            SceneLoader sceneLoader,
            SettingsService settingsService)
        {
            input = inputReader;
            pause = pauseService;
            scenes = sceneLoader;
            settings = settingsService;
            pause.Resume();
            input.UseUI();
        }

        public void StartGame()
        {
            settings?.MarkIntroSeen();
            scenes?.LoadScene(SceneNames.Sandbox);
        }

        public void OpenIntro()
        {
            mainScreen.Hide();
            settingsScreen.Hide();
            introSequence?.Show();
        }

        private void OnIntroCompleted()
        {
            settings?.MarkIntroSeen();
            scenes?.LoadScene(SceneNames.Sandbox);
        }

        public void OpenSettings()
        {
            mainScreen.Hide();
            settingsScreen.Show();
        }

        public void CloseSettings()
        {
            settingsPanel.Commit();
            settingsScreen.Hide();
            mainScreen.Show();
        }

        public void Quit()
        {
            settingsPanel.Commit();
            Application.Quit();
        }

        private void OnDestroy()
        {
            if (introSequence != null)
            {
                introSequence.Completed -= OnIntroCompleted;
            }
        }
    }
}
