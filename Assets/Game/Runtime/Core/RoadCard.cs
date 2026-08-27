using System;
using System.Collections.Generic;

namespace RoadOfLife
{
    public enum RoadCardPhase
    {
        Any,
        ToCity,
        FromCity,
    }

    public sealed class CardChoice
    {
        public CardChoice(string text, string resultText, StatDelta delta)
        {
            Text = RequireText(text, nameof(text));
            ResultText = RequireText(resultText, nameof(resultText));
            Delta = delta;
        }

        public string Text { get; }
        public string ResultText { get; }
        public StatDelta Delta { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty.", parameterName);

            return value.Trim();
        }
    }

    public sealed class RoadCard
    {
        private readonly int _tripMask;

        public RoadCard(
            string id,
            RoadCardPhase phase,
            IEnumerable<int> trips,
            CardTag tag,
            int weight,
            string eventText,
            CardChoice leftChoice,
            CardChoice rightChoice)
        {
            Id = RequireText(id, nameof(id));
            Phase = phase;
            Tag = tag;

            if (weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(weight), weight, "Card weight must be positive.");

            Weight = weight;
            EventText = RequireText(eventText, nameof(eventText));
            LeftChoice = leftChoice ?? throw new ArgumentNullException(nameof(leftChoice));
            RightChoice = rightChoice ?? throw new ArgumentNullException(nameof(rightChoice));

            if (trips == null)
                throw new ArgumentNullException(nameof(trips));

            foreach (var trip in trips)
            {
                if (trip < 1 || trip > RoadGameSession.TotalTrips)
                    throw new ArgumentOutOfRangeException(nameof(trips), trip, "Trip number must be from 1 to 3.");

                _tripMask |= 1 << (trip - 1);
            }

            if (_tripMask == 0)
                throw new ArgumentException("At least one trip must be specified.", nameof(trips));
        }

        public string Id { get; }
        public RoadCardPhase Phase { get; }
        public CardTag Tag { get; }
        public int Weight { get; }
        public string EventText { get; }
        public CardChoice LeftChoice { get; }
        public CardChoice RightChoice { get; }

        public bool IsAvailableFor(JourneyPhase phase, int tripNumber)
        {
            if (tripNumber < 1 || tripNumber > RoadGameSession.TotalTrips)
                return false;

            var phaseMatches = Phase == RoadCardPhase.Any ||
                               Phase == RoadCardPhase.ToCity && phase == JourneyPhase.ToCity ||
                               Phase == RoadCardPhase.FromCity && phase == JourneyPhase.FromCity;
            var tripMatches = (_tripMask & (1 << (tripNumber - 1))) != 0;
            return phaseMatches && tripMatches;
        }

        public bool IsAvailableOnTrip(int tripNumber) =>
            tripNumber >= 1 && tripNumber <= RoadGameSession.TotalTrips &&
            (_tripMask & (1 << (tripNumber - 1))) != 0;

        public CardChoice GetChoice(ChoiceSide side) => side switch
        {
            ChoiceSide.Left => LeftChoice,
            ChoiceSide.Right => RightChoice,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "A card requires Left or Right choice."),
        };

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty.", parameterName);

            return value.Trim();
        }
    }
}
