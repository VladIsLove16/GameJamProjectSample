using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RoadOfLife.Tests
{
    public sealed class CoreRoadGameTests
    {
        [Test]
        public void CurrentCardData_ParsesAllTwentyCards()
        {
            var database = LoadCurrentDatabase();

            Assert.That(database.Cards, Has.Count.EqualTo(20));
            Assert.That(database.Cards.Select(card => card.Id), Is.Unique);
        }

        [Test]
        public void CurrentCardData_BuildsValidEighteenCardDeck_ForOneThousandSeeds()
        {
            var database = LoadCurrentDatabase();
            var requests = CreateSessionRequests();

            for (var seed = 0; seed < 1000; seed++)
            {
                var deck = database.BuildDeck(requests, new SystemRandomSource(seed));

                Assert.That(deck, Has.Count.EqualTo(RoadGameSession.TotalChoices), $"seed {seed}");
                Assert.That(deck.Select(card => card.Id), Is.Unique, $"seed {seed}");
                for (var i = 0; i < deck.Count; i++)
                {
                    Assert.That(
                        deck[i].IsAvailableFor(requests[i].Phase, requests[i].TripNumber),
                        Is.True,
                        $"seed {seed}, slot {i}, card {deck[i].Id}");
                }
            }
        }

        [TestCase(RoadStat.Tempo, -50, FailureReason.TempoLow)]
        [TestCase(RoadStat.Tempo, 50, FailureReason.TempoHigh)]
        [TestCase(RoadStat.Engine, -50, FailureReason.EngineLow)]
        [TestCase(RoadStat.Engine, 50, FailureReason.EngineHigh)]
        [TestCase(RoadStat.Visibility, -50, FailureReason.VisibilityLow)]
        [TestCase(RoadStat.Visibility, 50, FailureReason.VisibilityHigh)]
        [TestCase(RoadStat.Load, -50, FailureReason.LoadLow)]
        [TestCase(RoadStat.Load, 50, FailureReason.LoadHigh)]
        public void Stats_FailAtBothEndsOfEveryScale(
            RoadStat stat,
            int amount,
            FailureReason expectedFailure)
        {
            var stats = new RoadStats();

            stats.Apply(DeltaFor(stat, amount));

            Assert.That(stats.GetFailureReason(), Is.EqualTo(expectedFailure));
        }

        [Test]
        public void Session_UsesThreeTrips_ResetsStats_AndAppliesUpgrade()
        {
            var database = new RoadCardDatabase(CreateControlledSessionCards());
            var session = new RoadGameSession(database, new SystemRandomSource(7));
            session.Start();

            for (var i = 0; i < RoadGameSession.CardsPerTrip; i++)
                session.Choose(ChoiceSide.Left);

            Assert.That(session.Stage, Is.EqualTo(RoadSessionStage.ChoosingUpgrade));
            Assert.That(session.TripNumber, Is.EqualTo(1));
            Assert.That(session.CompletedTrips, Is.EqualTo(1));

            session.ChooseUpgrade(RoadUpgrade.RoadMarkers);

            Assert.That(session.TripNumber, Is.EqualTo(2));
            Assert.That(session.Stats.Snapshot, Is.EqualTo(new StatSnapshot(50, 50, 50, 50)));

            var resolution = session.Choose(ChoiceSide.Left);

            Assert.That(resolution.AfterChoice.Visibility, Is.EqualTo(60));
            Assert.That(resolution.AfterUpgrades.Visibility, Is.EqualTo(52));
            Assert.That(resolution.TriggeredUpgrade, Is.EqualTo(RoadUpgrade.RoadMarkers));
        }

        [Test]
        public void Session_RejectsSelectingTheSameUpgradeTwice()
        {
            var database = new RoadCardDatabase(CreateControlledSessionCards());
            var session = new RoadGameSession(database, new SystemRandomSource(1));
            session.Start();

            CompleteTrip(session);
            session.ChooseUpgrade(RoadUpgrade.RoadMarkers);
            CompleteTrip(session);

            Assert.That(
                () => session.ChooseUpgrade(RoadUpgrade.RoadMarkers),
                Throws.InvalidOperationException);
        }

        private static RoadCardDatabase LoadCurrentDatabase()
        {
            var path = Path.Combine(Application.dataPath, "Game", "Data", "Cards.tsv.txt");
            return RoadCardDatabase.FromTsv(File.ReadAllText(path));
        }

        private static IReadOnlyList<CardDrawRequest> CreateSessionRequests()
        {
            var requests = new List<CardDrawRequest>(RoadGameSession.TotalChoices);
            for (var trip = 1; trip <= RoadGameSession.TotalTrips; trip++)
            {
                for (var i = 0; i < RoadGameSession.CardsPerLeg; i++)
                    requests.Add(new CardDrawRequest(JourneyPhase.ToCity, trip));
                for (var i = 0; i < RoadGameSession.CardsPerLeg; i++)
                    requests.Add(new CardDrawRequest(JourneyPhase.FromCity, trip));
            }

            return requests;
        }

        private static IEnumerable<RoadCard> CreateControlledSessionCards()
        {
            for (var trip = 1; trip <= RoadGameSession.TotalTrips; trip++)
            {
                foreach (var phase in new[] { JourneyPhase.ToCity, JourneyPhase.FromCity })
                {
                    for (var i = 0; i < RoadGameSession.CardsPerLeg; i++)
                    {
                        var isSecondTrip = trip == 2;
                        var delta = isSecondTrip ? new StatDelta(0, 0, 10, 0) : default;
                        var tag = isSecondTrip ? CardTag.Visibility : CardTag.Alarm;
                        yield return new RoadCard(
                            $"trip_{trip}_{phase}_{i}",
                            phase == JourneyPhase.ToCity ? RoadCardPhase.ToCity : RoadCardPhase.FromCity,
                            new[] { trip },
                            tag,
                            1,
                            "Event",
                            new CardChoice("Left", "Left result", delta),
                            new CardChoice("Right", "Right result", delta));
                    }
                }
            }
        }

        private static void CompleteTrip(RoadGameSession session)
        {
            for (var i = 0; i < RoadGameSession.CardsPerTrip; i++)
                session.Choose(ChoiceSide.Left);
        }

        private static StatDelta DeltaFor(RoadStat stat, int amount) => stat switch
        {
            RoadStat.Tempo => new StatDelta(amount, 0, 0, 0),
            RoadStat.Engine => new StatDelta(0, amount, 0, 0),
            RoadStat.Visibility => new StatDelta(0, 0, amount, 0),
            RoadStat.Load => new StatDelta(0, 0, 0, amount),
            _ => default,
        };
    }
}
