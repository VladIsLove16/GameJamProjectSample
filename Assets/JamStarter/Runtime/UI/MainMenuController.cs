using UnityEngine;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour, IAppServicesConsumer
    {
        [SerializeField] private UIScreen mainScreen;
        [SerializeField] private UIScreen settingsScreen;
        [SerializeField] private SettingsPanel settingsPanel;

        private AppServices services;

        private void Start()
        {
            settingsScreen.Hide();
            mainScreen.Show();
        }

        public void Initialize(AppServices value)
        {
            services = value;
            services.Pause.Resume();
            services.Input.UseUI();
        }

        public void StartGame()
        {
            services?.Scenes.LoadScene(SceneNames.Sandbox);
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
