using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoadOfLife
{
    public readonly struct CardDrawRequest
    {
        public CardDrawRequest(JourneyPhase phase, int tripNumber, CardTag? requiredTag = null)
        {
            if (tripNumber < 1 || tripNumber > RoadGameSession.TotalTrips)
                throw new ArgumentOutOfRangeException(nameof(tripNumber));

            Phase = phase;
            TripNumber = tripNumber;
            RequiredTag = requiredTag;
        }

        public JourneyPhase Phase { get; }
        public int TripNumber { get; }
        public CardTag? RequiredTag { get; }

        public override string ToString() =>
            RequiredTag.HasValue
                ? $"trip {TripNumber}, {Phase}, tag {RequiredTag.Value}"
                : $"trip {TripNumber}, {Phase}";
    }

    public sealed class RoadCardDatabase
    {
        private readonly List<RoadCard> _cards;

        public RoadCardDatabase(IEnumerable<RoadCard> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));

            _cards = cards.ToList();
            if (_cards.Count == 0)
                throw new ArgumentException("Card database cannot be empty.", nameof(cards));
            if (_cards.Any(card => card == null))
                throw new ArgumentException("Card database cannot contain null cards.", nameof(cards));

            var duplicate = _cards
                .GroupBy(card => card.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
                throw new ArgumentException($"Card database contains duplicate id '{duplicate.Key}'.", nameof(cards));
        }

        public IReadOnlyList<RoadCard> Cards => _cards;

        public static RoadCardDatabase FromTsv(TextAsset asset) =>
            new RoadCardDatabase(CardTsvParser.Parse(asset));

        public static RoadCardDatabase FromTsv(string tsv) =>
            new RoadCardDatabase(CardTsvParser.Parse(tsv));

        public IReadOnlyList<RoadCard> GetEligible(
            CardDrawRequest request,
            ICollection<string> excludedIds = null)
        {
            return _cards
                .Where(card => IsEligible(card, request, excludedIds))
                .ToArray();
        }

        public RoadCard Draw(
            CardDrawRequest request,
            IRandomSource random,
            ICollection<string> excludedIds = null)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var eligible = GetEligible(request, excludedIds);
            if (eligible.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No eligible cards remain for {request}. " +
                    $"Database has {_cards.Count} cards and {excludedIds?.Count ?? 0} ids are excluded.");
            }

            return DrawWeighted(eligible, random);
        }

        public IReadOnlyList<RoadCard> BuildDeck(
            IReadOnlyList<CardDrawRequest> requests,
            IRandomSource random,
            ICollection<string> excludedIds = null)
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (requests.Count == 0)
                return Array.Empty<RoadCard>();

            var usedIds = excludedIds == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(excludedIds, StringComparer.OrdinalIgnoreCase);
            var result = new RoadCard[requests.Count];
            var assigned = new bool[requests.Count];

            if (!TryAssign(requests, random, usedIds, result, assigned, 0))
            {
                throw new InvalidOperationException(
                    $"Unable to build a non-repeating deck for {requests.Count} requested slots. " +
                    "Add more cards matching the requested phases, trips and tags.");
            }

            return result;
        }

        private bool TryAssign(
            IReadOnlyList<CardDrawRequest> requests,
            IRandomSource random,
            HashSet<string> usedIds,
            RoadCard[] result,
            bool[] assigned,
            int assignedCount)
        {
            if (assignedCount == requests.Count)
                return true;

            var requestIndex = FindMostConstrainedRequest(requests, usedIds, assigned);
            if (requestIndex < 0)
                return false;

            var candidates = _cards
                .Where(card => IsEligible(card, requests[requestIndex], usedIds))
                .ToList();
            if (candidates.Count == 0)
                return false;

            foreach (var candidate in WeightedOrder(candidates, random))
            {
                usedIds.Add(candidate.Id);
                result[requestIndex] = candidate;
                assigned[requestIndex] = true;

                if (TryAssign(requests, random, usedIds, result, assigned, assignedCount + 1))
                    return true;

                assigned[requestIndex] = false;
                result[requestIndex] = null;
                usedIds.Remove(candidate.Id);
            }

            return false;
        }

        private int FindMostConstrainedRequest(
            IReadOnlyList<CardDrawRequest> requests,
            HashSet<string> usedIds,
            bool[] assigned)
        {
            var bestIndex = -1;
            var bestCount = int.MaxValue;
            for (var i = 0; i < requests.Count; i++)
            {
                if (assigned[i])
                    continue;

                var count = 0;
                foreach (var card in _cards)
                {
                    if (IsEligible(card, requests[i], usedIds))
                        count++;
                }

                if (count >= bestCount)
                    continue;

                bestIndex = i;
                bestCount = count;
                if (bestCount == 0)
                    break;
            }

            return bestIndex;
        }

        private static IEnumerable<RoadCard> WeightedOrder(List<RoadCard> candidates, IRandomSource random)
        {
            var remaining = new List<RoadCard>(candidates);
            while (remaining.Count > 0)
            {
                var selected = DrawWeighted(remaining, random);
                remaining.Remove(selected);
                yield return selected;
            }
        }

        private static RoadCard DrawWeighted(IReadOnlyList<RoadCard> cards, IRandomSource random)
        {
            var totalWeight = 0;
            foreach (var card in cards)
                totalWeight = checked(totalWeight + card.Weight);

            var roll = random.Next(totalWeight);
            foreach (var card in cards)
            {
                if (roll < card.Weight)
                    return card;

                roll -= card.Weight;
            }

            throw new InvalidOperationException("Weighted card selection failed unexpectedly.");
        }

        private static bool IsEligible(
            RoadCard card,
            CardDrawRequest request,
            ICollection<string> excludedIds)
        {
            return card.IsAvailableFor(request.Phase, request.TripNumber) &&
                   (!request.RequiredTag.HasValue || card.Tag == request.RequiredTag.Value) &&
                   (excludedIds == null || !excludedIds.Contains(card.Id));
        }
    }
}
