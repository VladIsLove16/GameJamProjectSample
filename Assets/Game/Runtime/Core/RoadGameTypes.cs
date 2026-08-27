using System;

namespace RoadOfLife
{
    public enum ChoiceSide
    {
        None = 0,
        Left = -1,
        Right = 1,
    }

    public enum JourneyPhase
    {
        ToCity,
        FromCity,
    }

    public enum CardTag
    {
        Snow,
        Engine,
        Visibility,
        Load,
        Ice,
        Alarm,
    }

    public enum RoadStat
    {
        Tempo,
        Engine,
        Visibility,
        Load,
    }

    public enum RoadUpgrade
    {
        RoadMarkers,
        WarmingPoint,
        PreparedDetour,
        LoadingPost,
    }

    public enum FailureReason
    {
        None,
        TempoLow,
        TempoHigh,
        EngineLow,
        EngineHigh,
        VisibilityLow,
        VisibilityHigh,
        LoadLow,
        LoadHigh,
    }

    public enum RoadSessionStage
    {
        NotStarted,
        Driving,
        ChoosingUpgrade,
        Won,
        Lost,
    }

    [Serializable]
    public readonly struct StatDelta : IEquatable<StatDelta>
    {
        public StatDelta(int tempo, int engine, int visibility, int load)
        {
            Tempo = tempo;
            Engine = engine;
            Visibility = visibility;
            Load = load;
        }

        public int Tempo { get; }
        public int Engine { get; }
        public int Visibility { get; }
        public int Load { get; }

        public int this[RoadStat stat] => stat switch
        {
            RoadStat.Tempo => Tempo,
            RoadStat.Engine => Engine,
            RoadStat.Visibility => Visibility,
            RoadStat.Load => Load,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
        };

        public bool Equals(StatDelta other) =>
            Tempo == other.Tempo && Engine == other.Engine &&
            Visibility == other.Visibility && Load == other.Load;

        public override bool Equals(object obj) => obj is StatDelta other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Tempo, Engine, Visibility, Load);
    }

    [Serializable]
    public readonly struct StatSnapshot : IEquatable<StatSnapshot>
    {
        public StatSnapshot(int tempo, int engine, int visibility, int load)
        {
            Tempo = tempo;
            Engine = engine;
            Visibility = visibility;
            Load = load;
        }

        public int Tempo { get; }
        public int Engine { get; }
        public int Visibility { get; }
        public int Load { get; }

        public int this[RoadStat stat] => stat switch
        {
            RoadStat.Tempo => Tempo,
            RoadStat.Engine => Engine,
            RoadStat.Visibility => Visibility,
            RoadStat.Load => Load,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
        };

        public bool Equals(StatSnapshot other) =>
            Tempo == other.Tempo && Engine == other.Engine &&
            Visibility == other.Visibility && Load == other.Load;

        public override bool Equals(object obj) => obj is StatSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Tempo, Engine, Visibility, Load);
    }
}
