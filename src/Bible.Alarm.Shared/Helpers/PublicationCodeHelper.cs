#nullable enable
using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Utilities for working with publication codes.
/// Publication codes are stored/treated as strings and should be compared consistently across the app.
///
/// Primary requirement:
/// - Sort should prioritize "nwt" over others (for display and "pick first" cascade logic).
/// </summary>
public static class PublicationCodeHelper
{
    /// <summary>
    /// Preferred publication codes in priority order (lower index = higher priority).
    /// </summary>
    private static readonly string[] PriorityPublicationCodes = ["nwt", "bi12"];

    /// <summary>
    /// Comparer for publication codes used for display ordering and "pick-first" logic.
    /// </summary>
    public static IComparer<string?> PublicationCodeComparer { get; } = new PriorityPublicationCodeComparer();

    /// <summary>
    /// Normalizes a publication code: trims and converts whitespace-only to null.
    /// </summary>
    public static string? Normalize(string? publicationCode)
        => string.IsNullOrWhiteSpace(publicationCode) ? null : publicationCode.Trim();

    /// <summary>
    /// Gets the sort priority for a publication code.
    /// Lower is higher priority.
    /// </summary>
    public static int GetPublicationSortPriority(string? publicationCode)
    {
        var normalized = Normalize(publicationCode);
        if (normalized == null)
        {
            return PriorityPublicationCodes.Length; // null/empty codes come last
        }

        var lower = normalized.ToLowerInvariant();
        for (var i = 0; i < PriorityPublicationCodes.Length; i++)
        {
            if (lower == PriorityPublicationCodes[i])
            {
                return i;
            }
        }

        return PriorityPublicationCodes.Length; // Others come after priority publications
    }

    /// <summary>
    /// Returns the priority publication codes in order (e.g. "nwt", "bi12").
    /// </summary>
    public static string[] GetPriorityPublicationCodes()
        => (string[])PriorityPublicationCodes.Clone();

    private sealed class PriorityPublicationCodeComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y)
        {
            var a = Normalize(x);
            var b = Normalize(y);

            if (a == null && b == null)
            {
                return 0;
            }
            if (a == null)
            {
                return 1; // null last
            }
            if (b == null)
            {
                return -1; // null last
            }

            var aPriority = GetPublicationSortPriority(a);
            var bPriority = GetPublicationSortPriority(b);
            var priorityCompare = aPriority.CompareTo(bPriority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            // Stable fallback: compare codes case-insensitively.
            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }
    }
}

