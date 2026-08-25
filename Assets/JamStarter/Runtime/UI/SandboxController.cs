using TMPro;
using UnityEngine;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class SandboxController : MonoBehaviour, IAppServicesConsumer
    {
        private enum OverlayState
        {
            None,
            Pause,
            Settings,
            Result,
        }

        [SerializeField] private UIScreen hudScreen;
        [SerializeField] private UIScreen pauseScreen;
        [SerializeField] private UIScreen settingsScreen;
        [SerializeField] private UIScreen resultScreen;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private TMP_Text resultMessage;

        private AppServices services;
        private OverlayState overlayState;

        public void Initialize(AppServices value)
        {
            if (services != null)
            {
                services.Input.PausePressed -= OnPauseRequested;
                services.Input.CancelPressed -= OnCancelRequested;
            }

            services = value;
            services.Input.PausePressed += OnPauseRequested;
            services.Input.CancelPressed += OnCancelRequested;
            services.Pause.Resume();
            services.Input.UseGameplay();

            pauseScreen.Hide();
            settingsScreen.Hide();
            resultScreen.Hide();
            hudScreen.Show(false);
            overlayState = OverlayState.None;
        }

        public void TogglePause()
        {
            if (overlayState == OverlayState.None)
            {
                Pause();
            }
            else if (overlayState == OverlayState.Pause)
            {
                Resume();
            }
        }

        public void Pause()
        {
            if (services == null || overlayState != OverlayState.None)
            {
                return;
            }

            services.Pause.Pause();
            services.Input.UseUI();
            pauseScreen.Show();
            overlayState = OverlayState.Pause;
        }

        public void Resume()
        {
            if (services == null || overlayState == OverlayState.Result)
            {
                return;
            }

            settingsPanel.Commit();
            settingsScreen.Hide();
            pauseScreen.Hide();
            services.Pause.Resume();
            services.Input.UseGameplay();
            overlayState = OverlayState.None;
        }

        public void OpenSettings()
        {
            if (overlayState != OverlayState.Pause)
            {
                return;
            }

            pauseScreen.Hide();
            settingsScreen.Show();
            overlayState = OverlayState.Settings;
        }

        public void CloseSettings()
        {
            if (overlayState != OverlayState.Settings)
            {
                return;
            }

            settingsPanel.Commit();
            settingsScreen.Hide();
            pauseScreen.Show();
            overlayState = OverlayState.Pause;
        }

        public void CompleteSandboxFlow()
        {
            if (services == null)
            {
                return;
            }

            services.Pause.Pause();
            services.Input.UseUI();
            pauseScreen.Hide();
            settingsScreen.Hide();
            resultMessage.text = "Flow complete\nReplace Sandbox with your game scene";
            resultScreen.Show();
            overlayState = OverlayState.Result;
        }

        public void Restart()
        {
            settingsPanel.Commit();
            services?.Scenes.ReloadActiveScene();
        }

        public void ReturnToMainMenu()
        {
            settingsPanel.Commit();
            services?.Scenes.LoadScene(SceneNames.MainMenu);
        }

        private void OnPauseRequested()
        {
            TogglePause();
        }

        private void OnCancelRequested()
        {
            if (overlayState == OverlayState.Settings)
            {
                CloseSettings();
            }
            else if (overlayState == OverlayState.Pause)
            {
                Resume();
            }
        }

        private void OnDestroy()
        {
            if (services == null)
            {
                return;
            }

            services.Input.PausePressed -= OnPauseRequested;
            services.Input.CancelPressed -= OnCancelRequested;
            services.Pause.Resume();
        }
    }
}
