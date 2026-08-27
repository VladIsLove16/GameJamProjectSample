using System;
using NUnit.Framework;
using UnityEngine;

namespace JamStarter.Tests
{
    public sealed class CountdownTimerTests
    {
        [Test]
        public void Tick_CompletesExactlyOnce()
        {
            var timer = new CountdownTimer();
            int completions = 0;
            timer.Completed += () => completions++;

            timer.Start(1f);
            timer.Tick(0.4f);
            timer.Tick(0.6f);
            timer.Tick(10f);

            Assert.That(timer.State, Is.EqualTo(CountdownTimerState.Completed));
            Assert.That(timer.RemainingSeconds, Is.Zero);
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void Pause_PreventsTimeFromAdvancing()
        {
            var timer = new CountdownTimer();
            timer.Start(5f);

            Assert.That(timer.Pause(), Is.True);
            timer.Tick(3f);

            Assert.That(timer.RemainingSeconds, Is.EqualTo(5f));
            Assert.That(timer.Resume(), Is.True);
            timer.Tick(2f);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(3f));
        }

        [Test]
        public void AddTime_IsOnlyValidForActiveTimer()
        {
            var timer = new CountdownTimer();
            Assert.Throws<InvalidOperationException>(() => timer.AddTime(1f));

            timer.Start(2f);
            timer.AddTime(3f);
            Assert.That(timer.RemainingSeconds, Is.EqualTo(5f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Start_RejectsInvalidDuration(float value)
        {
            var timer = new CountdownTimer();
            Assert.Throws<ArgumentOutOfRangeException>(() => timer.Start(value));
        }
    }

    public sealed class TutorialLaunchRequestTests
    {
        [SetUp]
        [TearDown]
        public void ClearRequest()
        {
            PlayerPrefs.DeleteKey(SettingsService.ShowTutorialNextPlayKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void RequestedTutorial_IsConsumedExactlyOnce()
        {
            SettingsService.RequestTutorialOnNextPlay();

            Assert.That(SettingsService.ConsumeTutorialOnNextPlayRequest(), Is.True);
            Assert.That(SettingsService.ConsumeTutorialOnNextPlayRequest(), Is.False);
        }
    }
}
