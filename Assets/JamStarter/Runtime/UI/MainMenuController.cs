using UnityEngine;
using Zenject;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private UIScreen mainScreen;
        [SerializeField] private UIScreen settingsScreen;
        [SerializeField] private SettingsPanel settingsPanel;

        private InputReader input;
        private GamePauseService pause;
        private SceneLoader scenes;

        private void Start()
        {
            settingsScreen.Hide();
            mainScreen.Show();
        }

        [Inject]
        private void Construct(InputReader inputReader, GamePauseService pauseService, SceneLoader sceneLoader)
        {
            input = inputReader;
            pause = pauseService;
            scenes = sceneLoader;
            pause.Resume();
            input.UseUI();
        }

        public void StartGame()
        {
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
    }
}
