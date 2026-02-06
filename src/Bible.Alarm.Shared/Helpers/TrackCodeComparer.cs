#nullable enable
using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Comparer for track codes that performs numeric comparison when both codes are numeric,
/// otherwise performs string comparison.
/// This ensures tracks are sorted correctly: "1", "2", "10", "11" instead of "1", "10", "11", "2".
/// </summary>
public static class TrackCodeComparer
{
    public static IComparer<string> Comparer { get; } = new NaturalTrackCodeComparer();

    private sealed class NaturalTrackCodeComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null)
            {
                return 0;
            }
            if (x == null)
            {
                return -1;
            }
            if (y == null)
            {
                return 1;
            }

            var aIsInt = int.TryParse(x, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var aInt);
            var bIsInt = int.TryParse(y, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var bInt);

            // If both are numeric, compare numerically ("2" < "10")
            if (aIsInt && bIsInt)
            {
                return aInt.CompareTo(bInt);
            }

            // If only one is numeric, keep numeric codes grouped first
            if (aIsInt && !bIsInt)
            {
                return -1;
            }
            if (!aIsInt && bIsInt)
            {
                return 1;
            }

            // Otherwise compare as strings
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }
}
