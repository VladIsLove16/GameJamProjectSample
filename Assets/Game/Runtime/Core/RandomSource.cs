using System;

namespace RoadOfLife
{
    public interface IRandomSource
    {
        int Next(int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource() : this(Environment.TickCount)
        {
        }

        public SystemRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            return _random.Next(maxExclusive);
        }
    }

    public sealed class UnityRandomSource : IRandomSource
    {
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            return UnityEngine.Random.Range(0, maxExclusive);
        }
    }
}
