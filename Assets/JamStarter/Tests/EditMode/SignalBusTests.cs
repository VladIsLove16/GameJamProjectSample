using NUnit.Framework;
using Zenject;

namespace JamStarter.Tests
{
    public sealed class SignalBusTests
    {
        private DiContainer container;
        private SignalBus signalBus;

        [SetUp]
        public void SetUp()
        {
            container = new DiContainer(StaticContext.Container);
            SignalBusInstaller.Install(container);
            AppSignalInstaller.Install(container);
            signalBus = container.Resolve<SignalBus>();
        }

        [TearDown]
        public void TearDown()
        {
            StaticContext.Clear();
        }

        [Test]
        public void TypedSignal_DeliversPayload()
        {
            SceneLoadProgressSignal received = default;
            signalBus.Subscribe<SceneLoadProgressSignal>(signal => received = signal);

            signalBus.Fire(new SceneLoadProgressSignal("Arena", 0.75f));

            Assert.That(received.SceneName, Is.EqualTo("Arena"));
            Assert.That(received.Progress, Is.EqualTo(0.75f));
        }

        [Test]
        public void TryUnsubscribe_StopsDeliveryAndIsIdempotent()
        {
            int calls = 0;
            void OnPaused(PauseChangedSignal signal) => calls++;

            signalBus.Subscribe<PauseChangedSignal>(OnPaused);
            signalBus.Fire(new PauseChangedSignal(true));
            signalBus.TryUnsubscribe<PauseChangedSignal>(OnPaused);
            signalBus.TryUnsubscribe<PauseChangedSignal>(OnPaused);
            signalBus.Fire(new PauseChangedSignal(false));

            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
