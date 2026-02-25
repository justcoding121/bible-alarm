#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Utilities for working with section codes.
/// Section codes are stored as strings in the schedule DB to match the media index DB.
/// Examples:
/// - "1" (Bible book 1)
/// - "iam-1" (melody disc 1)
/// - null/empty (non-sectioned publications)
/// </summary>
public static class SectionCodeHelper
{
    public static IComparer<string?> SectionCodeComparer { get; } = new NaturalSectionCodeComparer();

    /// <summary>
    /// Normalizes section code for persistence: trims and converts whitespace-only to null.
    /// </summary>
    public static string? Normalize(string? sectionCode)
        => string.IsNullOrWhiteSpace(sectionCode) ? null : sectionCode.Trim();

    /// <summary>
    /// Returns true when both section codes are equal: if both numeric, same integer value; otherwise string equal (ignore case).
    /// </summary>
    public static bool CodeEquals(string? a, string? b) => CodeComparisonHelper.Equals(Normalize(a), Normalize(b));

    private sealed class NaturalSectionCodeComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y) => CodeComparisonHelper.Compare(Normalize(x), Normalize(y));
    }
}

