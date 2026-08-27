using System;

namespace RoadOfLife
{
    public sealed class RoadStats
    {
        public const int Minimum = 0;
        public const int Maximum = 100;
        public const int Neutral = 50;

        private int _tempo;
        private int _engine;
        private int _visibility;
        private int _load;

        public RoadStats()
        {
            Reset();
        }

        public StatSnapshot Snapshot => new StatSnapshot(_tempo, _engine, _visibility, _load);

        public void Reset()
        {
            _tempo = Neutral;
            _engine = Neutral;
            _visibility = Neutral;
            _load = Neutral;
        }

        public StatSnapshot Apply(StatDelta delta)
        {
            _tempo += delta.Tempo;
            _engine += delta.Engine;
            _visibility += delta.Visibility;
            _load += delta.Load;
            return Snapshot;
        }

        public StatSnapshot MoveTowardNeutral(RoadStat stat, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");

            Set(stat, MoveTowards(Get(stat), Neutral, amount));
            return Snapshot;
        }

        public int Get(RoadStat stat) => stat switch
        {
            RoadStat.Tempo => _tempo,
            RoadStat.Engine => _engine,
            RoadStat.Visibility => _visibility,
            RoadStat.Load => _load,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
        };

        public FailureReason GetFailureReason()
        {
            if (_tempo <= Minimum) return FailureReason.TempoLow;
            if (_tempo >= Maximum) return FailureReason.TempoHigh;
            if (_engine <= Minimum) return FailureReason.EngineLow;
            if (_engine >= Maximum) return FailureReason.EngineHigh;
            if (_visibility <= Minimum) return FailureReason.VisibilityLow;
            if (_visibility >= Maximum) return FailureReason.VisibilityHigh;
            if (_load <= Minimum) return FailureReason.LoadLow;
            if (_load >= Maximum) return FailureReason.LoadHigh;
            return FailureReason.None;
        }

        private void Set(RoadStat stat, int value)
        {
            switch (stat)
            {
                case RoadStat.Tempo:
                    _tempo = value;
                    break;
                case RoadStat.Engine:
                    _engine = value;
                    break;
                case RoadStat.Visibility:
                    _visibility = value;
                    break;
                case RoadStat.Load:
                    _load = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
            }
        }

        private static int MoveTowards(int current, int target, int maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;

            return current + Math.Sign(target - current) * maxDelta;
        }

    }
}
