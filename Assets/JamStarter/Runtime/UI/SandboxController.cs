using TMPro;
using UnityEngine;
using Zenject;

namespace JamStarter
{
    [DisallowMultipleComponent]
    public sealed class SandboxController : MonoBehaviour
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

        private InputReader input;
        private GamePauseService pauseService;
        private SceneLoader scenes;
        private OverlayState overlayState;

        [Inject]
        private void Construct(InputReader inputReader, GamePauseService pause, SceneLoader sceneLoader)
        {
            if (input != null)
            {
                input.PausePressed -= OnPauseRequested;
                input.CancelPressed -= OnCancelRequested;
            }

            input = inputReader;
            pauseService = pause;
            scenes = sceneLoader;
            input.PausePressed += OnPauseRequested;
            input.CancelPressed += OnCancelRequested;
            pauseService.Resume();
            input.UseGameplay();

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
            if (input == null || overlayState != OverlayState.None)
            {
                return;
            }

            pauseService.Pause();
            input.UseUI();
            pauseScreen.Show();
            overlayState = OverlayState.Pause;
        }

        public void Resume()
        {
            if (input == null || overlayState == OverlayState.Result)
            {
                return;
            }

            settingsPanel.Commit();
            settingsScreen.Hide();
            pauseScreen.Hide();
            pauseService.Resume();
            input.UseGameplay();
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
            ShowResult("Flow complete\nReplace Sandbox with your game scene");
        }

        /// <summary>
        /// Finishes the active game flow while keeping pause/input state synchronized
        /// with the starter's existing result overlay.
        /// </summary>
        public void ShowResult(string message)
        {
            if (input == null)
            {
                return;
            }

            pauseService.Pause();
            input.UseUI();
            pauseScreen.Hide();
            settingsScreen.Hide();
            resultMessage.text = string.IsNullOrWhiteSpace(message) ? "Рейс завершён" : message;
            resultScreen.Show();
            overlayState = OverlayState.Result;
        }

        public void Restart()
        {
            settingsPanel.Commit();
            scenes?.ReloadActiveScene();
        }

        public void ReturnToMainMenu()
        {
            settingsPanel.Commit();
            scenes?.LoadScene(SceneNames.MainMenu);
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
            if (input == null)
            {
                return;
            }

            input.PausePressed -= OnPauseRequested;
            input.CancelPressed -= OnCancelRequested;
            pauseService.Resume();
        }
    }
}
