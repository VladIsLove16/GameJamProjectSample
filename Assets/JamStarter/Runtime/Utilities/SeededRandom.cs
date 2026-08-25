using System;
using System.Collections.Generic;

namespace JamStarter
{
    /// <summary>
    /// Small deterministic pseudo-random generator whose sequence does not depend on
    /// UnityEngine.Random's global state.
    /// </summary>
    public sealed class SeededRandom
    {
        private readonly uint _initialState;
        private uint _state;

        public SeededRandom(int seed)
        {
            Seed = seed;
            _initialState = Scramble(unchecked((uint)seed));
            _state = _initialState;
        }

        public int Seed { get; }

        public float Value => (NextUInt() >> 8) * (1f / 16777216f);

        public void Reset()
        {
            _state = _initialState;
        }

        public uint NextUInt()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Maximum must be greater than minimum.");
            }

            var range = (uint)((long)maxExclusive - minInclusive);
            var threshold = unchecked(0u - range) % range;
            uint sample;

            do
            {
                sample = NextUInt();
            }
            while (sample < threshold);

            return (int)((long)minInclusive + sample % range);
        }

        public float Range(float minInclusive, float maxExclusive)
        {
            EnsureFinite(minInclusive, nameof(minInclusive));
            EnsureFinite(maxExclusive, nameof(maxExclusive));

            if (minInclusive > maxExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Maximum must be greater than or equal to minimum.");
            }

            return minInclusive + (maxExclusive - minInclusive) * Value;
        }

        public bool Chance(float probability)
        {
            if (float.IsNaN(probability) || probability < 0f || probability > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(probability), probability, "Probability must be between zero and one.");
            }

            return probability >= 1f || probability > 0f && Value < probability;
        }

        public void Shuffle<T>(IList<T> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            for (var index = values.Count - 1; index > 0; index--)
            {
                var swapIndex = Range(0, index + 1);
                if (swapIndex == index)
                {
                    continue;
                }

                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }

        private static uint Scramble(uint seed)
        {
            seed += 0x9E3779B9u;
            seed = (seed ^ (seed >> 16)) * 0x85EBCA6Bu;
            seed = (seed ^ (seed >> 13)) * 0xC2B2AE35u;
            seed ^= seed >> 16;
            return seed == 0u ? 0xA341316Cu : seed;
        }

        private static void EnsureFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite.");
            }
        }
    }
}
