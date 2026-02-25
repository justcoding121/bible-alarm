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
        public int Compare(string? x, string? y) => CodeComparisonHelper.Compare(x, y);
    }
}
