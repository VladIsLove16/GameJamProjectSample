using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

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

        private Coroutine loadRoutine;
        private GamePauseService pauseService;
        private SignalBus signalBus;

        public bool IsLoading => loadRoutine != null;

        private void Awake()
        {
            SetOverlay(0f, false);
        }

        [Inject]
        private void Construct(GamePauseService pause, SignalBus events)
        {
            pauseService = pause;
            signalBus = events;
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
            signalBus?.Fire(new SceneLoadStartedSignal(sceneName));
            signalBus?.Fire(new SceneLoadProgressSignal(sceneName, 0f));
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
                FinishFailedLoad(sceneName);
                yield break;
            }

            if (operation == null)
            {
                Debug.LogError($"Unity could not start loading scene '{sceneName}'.", this);
                FinishFailedLoad(sceneName);
                yield break;
            }

            while (!operation.isDone)
            {
                // Unity reports scene loading from 0 to 0.9 until activation completes.
                signalBus?.Fire(new SceneLoadProgressSignal(
                    sceneName,
                    Mathf.Clamp01(operation.progress / 0.9f)));
                yield return null;
            }

            signalBus?.Fire(new SceneLoadProgressSignal(sceneName, 1f));
            signalBus?.Fire(new SceneLoadCompletedSignal(sceneName));
            yield return FadeOverlay(0f);

            SetOverlay(0f, false);
            loadRoutine = null;
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

        private void FinishFailedLoad(string sceneName)
        {
            SetOverlay(0f, false);
            loadRoutine = null;
            signalBus?.Fire(new SceneLoadFailedSignal(sceneName));
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
