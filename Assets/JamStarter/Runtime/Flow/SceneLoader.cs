using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamStarter
{
    /// <summary>
    /// Serializes Single-scene transitions behind an optional unscaled fade overlay.
    /// It deliberately has no global accessor; inject its reference where navigation is needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private CanvasGroup loadingOverlay;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;

        [Header("Services")]
        [SerializeField] private GamePauseService pauseService;

        private Coroutine loadRoutine;

        public event Action<bool> LoadingChanged;
        public event Action<float> LoadingProgressChanged;
        public event Action<string> SceneLoadStarted;
        public event Action<string> SceneLoadCompleted;

        public bool IsLoading => loadRoutine != null;

        private void Awake()
        {
            SetOverlay(0f, false);
        }

        /// <summary>UnityEvent-friendly scene navigation entry point.</summary>
        public void LoadScene(string sceneName)
        {
            TryLoadScene(sceneName);
        }

        /// <summary>Starts a transition if no other transition is running.</summary>
        public bool TryLoadScene(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning(
                    $"Ignoring request to load '{sceneName}' because a scene transition is already running.",
                    this);
                return false;
            }

            if (!isActiveAndEnabled)
            {
                Debug.LogWarning(
                    $"Cannot load '{sceneName}' because {nameof(SceneLoader)} is disabled.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("Cannot load a scene with an empty name.", this);
                return false;
            }

            string trimmedSceneName = sceneName.Trim();
            if (!Application.CanStreamedLevelBeLoaded(trimmedSceneName))
            {
                Debug.LogError(
                    $"Scene '{trimmedSceneName}' is not available. Add it to Build Settings first.", this);
                return false;
            }

            loadRoutine = StartCoroutine(LoadSceneRoutine(trimmedSceneName));
            return true;
        }

        /// <summary>UnityEvent-friendly active scene reload.</summary>
        public void ReloadActiveScene()
        {
            TryReloadActiveScene();
        }

        public bool TryReloadActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.name))
            {
                Debug.LogError("There is no valid active scene to reload.", this);
                return false;
            }

            return TryLoadScene(activeScene.name);
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            LoadingChanged?.Invoke(true);
            SceneLoadStarted?.Invoke(sceneName);
            LoadingProgressChanged?.Invoke(0f);
            SetOverlay(loadingOverlay != null ? loadingOverlay.alpha : 0f, true);

            yield return FadeOverlay(1f);

            PrepareTimeForSingleLoad();

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                FinishFailedLoad();
                yield break;
            }

            if (operation == null)
            {
                Debug.LogError($"Unity could not start loading scene '{sceneName}'.", this);
                FinishFailedLoad();
                yield break;
            }

            while (!operation.isDone)
            {
                // Unity reports scene loading from 0 to 0.9 until activation completes.
                LoadingProgressChanged?.Invoke(Mathf.Clamp01(operation.progress / 0.9f));
                yield return null;
            }

            LoadingProgressChanged?.Invoke(1f);
            SceneLoadCompleted?.Invoke(sceneName);
            yield return FadeOverlay(0f);

            SetOverlay(0f, false);
            loadRoutine = null;
            LoadingChanged?.Invoke(false);
        }

        private IEnumerator FadeOverlay(float targetAlpha)
        {
            if (loadingOverlay == null)
            {
                yield break;
            }

            float startAlpha = loadingOverlay.alpha;
            if (fadeDuration <= 0f || Mathf.Approximately(startAlpha, targetAlpha))
            {
                loadingOverlay.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                loadingOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                yield return null;
            }

            loadingOverlay.alpha = targetAlpha;
        }

        private void PrepareTimeForSingleLoad()
        {
            if (pauseService != null)
            {
                pauseService.ResetForSceneTransition();
                return;
            }

            // A loader can be used without the optional pause service in tiny prototypes.
            // In that case, still prevent a paused time scale leaking into the next scene.
            Time.timeScale = 1f;
        }

        private void FinishFailedLoad()
        {
            SetOverlay(0f, false);
            loadRoutine = null;
            LoadingChanged?.Invoke(false);
        }

        private void SetOverlay(float alpha, bool blocksInput)
        {
            if (loadingOverlay == null)
            {
                return;
            }

            loadingOverlay.alpha = alpha;
            loadingOverlay.interactable = blocksInput;
            loadingOverlay.blocksRaycasts = blocksInput;
        }
    }
}
