using System.Collections.Generic;
using NUnit.Framework;

namespace JamStarter.Tests
{
    public sealed class SeededRandomTests
    {
        [Test]
        public void EqualSeeds_ProduceEqualSequences()
        {
            var left = new SeededRandom(42);
            var right = new SeededRandom(42);

            for (int index = 0; index < 128; index++)
            {
                Assert.That(left.NextUInt(), Is.EqualTo(right.NextUInt()));
            }
        }

        [Test]
        public void Reset_ReplaysSequence()
        {
            var random = new SeededRandom(1701);
            uint first = random.NextUInt();
            random.NextUInt();

            random.Reset();

            Assert.That(random.NextUInt(), Is.EqualTo(first));
        }

        [Test]
        public void IntegerRange_StaysInsideBounds()
        {
            var random = new SeededRandom(-12);
            for (int index = 0; index < 1000; index++)
            {
                Assert.That(random.Range(-4, 7), Is.InRange(-4, 6));
            }
        }

        [Test]
        public void Shuffle_IsDeterministic()
        {
            var left = new List<int> { 1, 2, 3, 4, 5, 6 };
            var right = new List<int>(left);
            new SeededRandom(99).Shuffle(left);
            new SeededRandom(99).Shuffle(right);

            Assert.That(left, Is.EqualTo(right));
        }
    }
}
