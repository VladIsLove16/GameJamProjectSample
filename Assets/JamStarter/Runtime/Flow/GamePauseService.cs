using UnityEngine;
using Zenject;

namespace JamStarter
{
    /// <summary>
    /// Owns the game's time-scale based pause state. Calls are idempotent and the
    /// time scale that existed before pausing is restored on resume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePauseService : MonoBehaviour
    {
        private const float DefaultTimeScale = 1f;

        private float timeScaleBeforePause = DefaultTimeScale;

        private SignalBus signalBus;

        public bool IsPaused { get; private set; }
        public float TimeScaleBeforePause => timeScaleBeforePause;

        [Inject]
        private void Construct(SignalBus events)
        {
            signalBus = events;
        }

        /// <summary>Pauses scaled gameplay time. Returns false if already paused.</summary>
        public bool Pause()
        {
            if (IsPaused)
            {
                return false;
            }

            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            IsPaused = true;
            signalBus?.Fire(new PauseChangedSignal(true));
            return true;
        }

        /// <summary>Restores the time scale captured by <see cref="Pause"/>.</summary>
        public bool Resume()
        {
            return ResumeInternal(true);
        }

        public bool Toggle()
        {
            return IsPaused ? Resume() : Pause();
        }

        public bool SetPaused(bool paused)
        {
            return paused ? Pause() : Resume();
        }

        /// <summary>
        /// Clears pause before a Single scene transition and normalizes game time.
        /// A new scene should never inherit a paused or slow-motion state by accident.
        /// </summary>
        public void ResetForSceneTransition()
        {
            bool wasPaused = IsPaused;
            ResumeInternal(false);
            timeScaleBeforePause = DefaultTimeScale;
            Time.timeScale = DefaultTimeScale;
            if (wasPaused)
            {
                signalBus?.Fire(new PauseChangedSignal(false));
            }
        }

        private bool ResumeInternal(bool notify)
        {
            if (!IsPaused)
            {
                return false;
            }

            Time.timeScale = timeScaleBeforePause;
            IsPaused = false;

            if (notify)
            {
                signalBus?.Fire(new PauseChangedSignal(false));
            }

            return true;
        }

        private void OnDestroy()
        {
            if (IsPaused)
            {
                ResumeInternal(false);
            }
        }
    }
}
