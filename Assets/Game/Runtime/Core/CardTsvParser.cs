using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RoadOfLife
{
    public sealed class CardDataException : FormatException
    {
        public CardDataException(int lineNumber, string columnName, string details)
            : base(BuildMessage(lineNumber, columnName, details))
        {
            LineNumber = lineNumber;
            ColumnName = columnName;
        }

        public int LineNumber { get; }
        public string ColumnName { get; }

        private static string BuildMessage(int lineNumber, string columnName, string details)
        {
            var location = lineNumber > 0 ? $"line {lineNumber}" : "header";
            if (!string.IsNullOrEmpty(columnName))
                location += $", column '{columnName}'";

            return $"Invalid card TSV at {location}: {details}";
        }
    }

    public static class CardTsvParser
    {
        private static readonly string[] Headers =
        {
            "id", "phase", "trips", "tag", "weight", "event_text",
            "left_text", "left_result", "left_tempo", "left_engine", "left_visibility", "left_load",
            "right_text", "right_result", "right_tempo", "right_engine", "right_visibility", "right_load",
        };

        public static IReadOnlyList<RoadCard> Parse(TextAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            return Parse(asset.text);
        }

        public static IReadOnlyList<RoadCard> Parse(string tsv)
        {
            if (string.IsNullOrWhiteSpace(tsv))
                throw new CardDataException(0, null, "file is empty.");

            var cards = new List<RoadCard>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var reader = new StringReader(tsv);
            var headerLine = reader.ReadLine();
            ValidateHeader(headerLine);

            var lineNumber = 1;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = line.Split('\t');
                if (fields.Length != Headers.Length)
                {
                    throw new CardDataException(
                        lineNumber,
                        null,
                        $"expected {Headers.Length} tab-separated fields, found {fields.Length}.");
                }

                var id = Required(fields[0], lineNumber, Headers[0]);
                if (!ids.Add(id))
                    throw new CardDataException(lineNumber, Headers[0], $"duplicate card id '{id}'.");

                var phase = ParseEnum<RoadCardPhase>(fields[1], lineNumber, Headers[1]);
                var trips = ParseTrips(fields[2], lineNumber);
                var tag = ParseEnum<CardTag>(fields[3], lineNumber, Headers[3]);
                var weight = ParsePositiveInt(fields[4], lineNumber, Headers[4]);
                var eventText = Required(fields[5], lineNumber, Headers[5]);

                var left = new CardChoice(
                    Required(fields[6], lineNumber, Headers[6]),
                    Required(fields[7], lineNumber, Headers[7]),
                    ParseDelta(fields, 8, lineNumber, "left"));
                var right = new CardChoice(
                    Required(fields[12], lineNumber, Headers[12]),
                    Required(fields[13], lineNumber, Headers[13]),
                    ParseDelta(fields, 14, lineNumber, "right"));

                cards.Add(new RoadCard(id, phase, trips, tag, weight, eventText, left, right));
            }

            if (cards.Count == 0)
                throw new CardDataException(0, null, "file contains a header but no cards.");

            return cards;
        }

        private static void ValidateHeader(string line)
        {
            if (line == null)
                throw new CardDataException(0, null, "header is missing.");

            line = line.TrimStart('\uFEFF');
            var fields = line.Split('\t');
            if (fields.Length != Headers.Length)
            {
                throw new CardDataException(
                    0,
                    null,
                    $"expected {Headers.Length} columns, found {fields.Length}.");
            }

            for (var i = 0; i < Headers.Length; i++)
            {
                if (!string.Equals(fields[i].Trim(), Headers[i], StringComparison.Ordinal))
                {
                    throw new CardDataException(
                        0,
                        Headers[i],
                        $"expected column {i + 1} to be '{Headers[i]}', found '{fields[i]}'.");
                }
            }
        }

        private static StatDelta ParseDelta(string[] fields, int offset, int lineNumber, string side)
        {
            return new StatDelta(
                ParseInt(fields[offset], lineNumber, $"{side}_tempo"),
                ParseInt(fields[offset + 1], lineNumber, $"{side}_engine"),
                ParseInt(fields[offset + 2], lineNumber, $"{side}_visibility"),
                ParseInt(fields[offset + 3], lineNumber, $"{side}_load"));
        }

        private static IReadOnlyList<int> ParseTrips(string value, int lineNumber)
        {
            value = Required(value, lineNumber, Headers[2]);
            var parts = value.Split(',');
            var trips = new List<int>(parts.Length);
            var seen = new HashSet<int>();
            foreach (var part in parts)
            {
                if (!int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var trip) ||
                    trip < 1 || trip > RoadGameSession.TotalTrips)
                {
                    throw new CardDataException(lineNumber, Headers[2], $"'{part}' is not a trip number from 1 to 3.");
                }

                if (!seen.Add(trip))
                    throw new CardDataException(lineNumber, Headers[2], $"trip {trip} is listed more than once.");

                trips.Add(trip);
            }

            return trips;
        }

        private static T ParseEnum<T>(string value, int lineNumber, string columnName) where T : struct
        {
            value = Required(value, lineNumber, columnName);
            if (!Enum.TryParse(value, true, out T result) || !Enum.IsDefined(typeof(T), result))
            {
                throw new CardDataException(
                    lineNumber,
                    columnName,
                    $"unknown value '{value}'. Expected one of: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
            }

            return result;
        }

        private static int ParsePositiveInt(string value, int lineNumber, string columnName)
        {
            var result = ParseInt(value, lineNumber, columnName);
            if (result <= 0)
                throw new CardDataException(lineNumber, columnName, "value must be greater than zero.");

            return result;
        }

        private static int ParseInt(string value, int lineNumber, string columnName)
        {
            value = Required(value, lineNumber, columnName);
            if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
                throw new CardDataException(lineNumber, columnName, $"'{value}' is not an integer.");

            return result;
        }

        private static string Required(string value, int lineNumber, string columnName)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value))
                throw new CardDataException(lineNumber, columnName, "value cannot be empty.");

            return value;
        }
    }
}
