using System;
using System.Collections.Generic;
using System.Linq;

namespace RoadOfLife
{
    public sealed class ChoiceResolution
    {
        public ChoiceResolution(
            RoadCard card,
            ChoiceSide side,
            CardChoice choice,
            StatSnapshot before,
            StatSnapshot afterChoice,
            StatSnapshot afterUpgrades,
            RoadUpgrade? triggeredUpgrade,
            FailureReason failure,
            bool tripCompleted,
            bool sessionWon)
        {
            Card = card;
            Side = side;
            Choice = choice;
            Before = before;
            AfterChoice = afterChoice;
            AfterUpgrades = afterUpgrades;
            TriggeredUpgrade = triggeredUpgrade;
            Failure = failure;
            TripCompleted = tripCompleted;
            SessionWon = sessionWon;
        }

        public RoadCard Card { get; }
        public ChoiceSide Side { get; }
        public CardChoice Choice { get; }
        public StatSnapshot Before { get; }
        public StatSnapshot AfterChoice { get; }
        public StatSnapshot AfterUpgrades { get; }
        public RoadUpgrade? TriggeredUpgrade { get; }
        public FailureReason Failure { get; }
        public bool TripCompleted { get; }
        public bool SessionWon { get; }
    }

    public sealed class RoadGameSession
    {
        public const int TotalTrips = 3;
        public const int CardsPerLeg = 3;
        public const int LegsPerTrip = 2;
        public const int CardsPerTrip = CardsPerLeg * LegsPerTrip;
        public const int TotalChoices = TotalTrips * CardsPerTrip;

        private readonly RoadCardDatabase _database;
        private readonly IRandomSource _random;
        private readonly List<RoadUpgrade> _activeUpgrades = new List<RoadUpgrade>();
        private readonly HashSet<string> _usedCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<RoadCard> _deck = Array.Empty<RoadCard>();
        private int _choiceIndex;
        private int _tripNumber;

        public RoadGameSession(RoadCardDatabase database, IRandomSource random = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _random = random ?? new UnityRandomSource();
            Stats = new RoadStats();
            Stage = RoadSessionStage.NotStarted;
        }

        public RoadStats Stats { get; }
        public RoadSessionStage Stage { get; private set; }
        public FailureReason FailureReason { get; private set; }
        public int TripNumber => _tripNumber;
        public int CompletedChoices => _choiceIndex;
        public int CompletedTrips => _choiceIndex / CardsPerTrip;
        public IReadOnlyList<RoadUpgrade> ActiveUpgrades => _activeUpgrades;
        public IReadOnlyCollection<string> UsedCardIds => _usedCardIds;

        public JourneyPhase Phase
        {
            get
            {
                EnsureDriving();
                return CurrentRequest.Phase;
            }
        }

        public int CardNumberInLeg
        {
            get
            {
                EnsureDriving();
                return _choiceIndex % CardsPerLeg + 1;
            }
        }

        public RoadCard CurrentCard => Stage == RoadSessionStage.Driving ? _deck[_choiceIndex] : null;

        public IReadOnlyList<RoadUpgrade> AvailableUpgrades => Enum
            .GetValues(typeof(RoadUpgrade))
            .Cast<RoadUpgrade>()
            .Where(upgrade => !_activeUpgrades.Contains(upgrade))
            .ToArray();

        private CardDrawRequest CurrentRequest => CreateRequests()[_choiceIndex];

        public void Start()
        {
            var requests = CreateRequests();
            _deck = _database.BuildDeck(requests, _random);
            _choiceIndex = 0;
            _tripNumber = 1;
            _activeUpgrades.Clear();
            _usedCardIds.Clear();
            Stats.Reset();
            FailureReason = FailureReason.None;
            Stage = RoadSessionStage.Driving;
        }

        public ChoiceResolution Choose(ChoiceSide side)
        {
            EnsureDriving();
            if (side != ChoiceSide.Left && side != ChoiceSide.Right)
                throw new ArgumentOutOfRangeException(nameof(side), side, "Choice must be Left or Right.");

            var card = CurrentCard;
            var choice = card.GetChoice(side);
            var before = Stats.Snapshot;
            var afterChoice = Stats.Apply(choice.Delta);
            var triggeredUpgrade = ApplyMatchingUpgrade(card.Tag);
            var afterUpgrades = Stats.Snapshot;
            var failure = Stats.GetFailureReason();

            _usedCardIds.Add(card.Id);
            _choiceIndex++;

            var tripCompleted = _choiceIndex % CardsPerTrip == 0;
            var sessionWon = false;
            if (failure != FailureReason.None)
            {
                FailureReason = failure;
                Stage = RoadSessionStage.Lost;
            }
            else if (_choiceIndex >= TotalChoices)
            {
                Stage = RoadSessionStage.Won;
                sessionWon = true;
            }
            else if (tripCompleted)
            {
                Stage = RoadSessionStage.ChoosingUpgrade;
            }

            return new ChoiceResolution(
                card,
                side,
                choice,
                before,
                afterChoice,
                afterUpgrades,
                triggeredUpgrade,
                failure,
                tripCompleted,
                sessionWon);
        }

        public void ChooseUpgrade(RoadUpgrade upgrade)
        {
            if (Stage != RoadSessionStage.ChoosingUpgrade)
                throw new InvalidOperationException($"Cannot choose an upgrade while session stage is {Stage}.");
            if (!Enum.IsDefined(typeof(RoadUpgrade), upgrade))
                throw new ArgumentOutOfRangeException(nameof(upgrade), upgrade, "Unknown road upgrade.");
            if (_activeUpgrades.Contains(upgrade))
                throw new InvalidOperationException($"Upgrade {upgrade} has already been selected.");

            _activeUpgrades.Add(upgrade);
            _tripNumber++;
            Stats.Reset();
            Stage = RoadSessionStage.Driving;
        }

        private RoadUpgrade? ApplyMatchingUpgrade(CardTag tag)
        {
            foreach (var upgrade in _activeUpgrades)
            {
                if (!RoadUpgradeRules.TryGetCorrection(upgrade, tag, out var stat))
                    continue;

                Stats.MoveTowardNeutral(stat, RoadUpgradeRules.CorrectionAmount);
                return upgrade;
            }

            return null;
        }

        private void EnsureDriving()
        {
            if (Stage != RoadSessionStage.Driving)
                throw new InvalidOperationException($"Session is not driving; current stage is {Stage}.");
        }

        private static IReadOnlyList<CardDrawRequest> CreateRequests()
        {
            var requests = new List<CardDrawRequest>(TotalChoices);
            for (var trip = 1; trip <= TotalTrips; trip++)
            {
                for (var i = 0; i < CardsPerLeg; i++)
                    requests.Add(new CardDrawRequest(JourneyPhase.ToCity, trip));
                for (var i = 0; i < CardsPerLeg; i++)
                    requests.Add(new CardDrawRequest(JourneyPhase.FromCity, trip));
            }

            return requests;
        }
    }
}
