using System;

namespace JamStarter
{
    public enum CountdownTimerState
    {
        Idle,
        Running,
        Paused,
        Completed,
        Cancelled,
    }

    /// <summary>
    /// A time-source-agnostic countdown. The owner decides whether to pass scaled or
    /// unscaled delta time to <see cref="Tick"/>.
    /// </summary>
    public sealed class CountdownTimer
    {
        public event Action<float> RemainingTimeChanged;
        public event Action<CountdownTimerState> StateChanged;
        public event Action Completed;
        public event Action Cancelled;

        public CountdownTimerState State { get; private set; } = CountdownTimerState.Idle;

        public float RemainingSeconds { get; private set; }

        public bool IsRunning => State == CountdownTimerState.Running;

        public bool IsFinished => State == CountdownTimerState.Completed || State == CountdownTimerState.Cancelled;

        /// <summary>
        /// Starts a new countdown, replacing any previous run.
        /// </summary>
        public void Start(float durationSeconds)
        {
            EnsureFiniteAndNonNegative(durationSeconds, nameof(durationSeconds));

            RemainingSeconds = durationSeconds;
            SetState(CountdownTimerState.Running);
            RemainingTimeChanged?.Invoke(RemainingSeconds);

            if (RemainingSeconds <= 0f)
            {
                Complete();
            }
        }

        /// <summary>
        /// Advances the countdown. Pass Time.deltaTime for a scaled clock or
        /// Time.unscaledDeltaTime for a clock that continues while the game is paused.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            EnsureFiniteAndNonNegative(deltaSeconds, nameof(deltaSeconds));

            if (State != CountdownTimerState.Running || deltaSeconds <= 0f)
            {
                return;
            }

            var nextRemaining = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (nextRemaining.Equals(RemainingSeconds))
            {
                return;
            }

            RemainingSeconds = nextRemaining;
            RemainingTimeChanged?.Invoke(RemainingSeconds);

            if (RemainingSeconds <= 0f)
            {
                Complete();
            }
        }

        /// <summary>
        /// Adds time to an active or paused countdown.
        /// </summary>
        public void AddTime(float seconds)
        {
            EnsureFiniteAndNonNegative(seconds, nameof(seconds));

            if (State != CountdownTimerState.Running && State != CountdownTimerState.Paused)
            {
                throw new InvalidOperationException("Time can only be added to a running or paused countdown.");
            }

            if (seconds <= 0f)
            {
                return;
            }

            var expandedTime = (double)RemainingSeconds + seconds;
            if (expandedTime > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "The resulting duration exceeds the supported range.");
            }

            RemainingSeconds = (float)expandedTime;
            RemainingTimeChanged?.Invoke(RemainingSeconds);
        }

        public bool Pause()
        {
            if (State != CountdownTimerState.Running)
            {
                return false;
            }

            SetState(CountdownTimerState.Paused);
            return true;
        }

        public bool Resume()
        {
            if (State != CountdownTimerState.Paused)
            {
                return false;
            }

            SetState(CountdownTimerState.Running);
            return true;
        }

        public bool Cancel()
        {
            if (State != CountdownTimerState.Running && State != CountdownTimerState.Paused)
            {
                return false;
            }

            SetState(CountdownTimerState.Cancelled);
            Cancelled?.Invoke();
            return true;
        }

        private void Complete()
        {
            if (State != CountdownTimerState.Running)
            {
                return;
            }

            RemainingSeconds = 0f;
            SetState(CountdownTimerState.Completed);
            Completed?.Invoke();
        }

        private void SetState(CountdownTimerState nextState)
        {
            if (State == nextState)
            {
                return;
            }

            State = nextState;
            StateChanged?.Invoke(State);
        }

        private static void EnsureFiniteAndNonNegative(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and non-negative.");
            }
        }
    }
}
