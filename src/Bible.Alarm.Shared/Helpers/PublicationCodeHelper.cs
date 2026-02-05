#nullable enable
using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Utilities for working with publication codes.
/// Publication codes are stored/treated as strings and should be compared consistently across the app.
///
/// Primary requirement:
/// - Bible: sort prioritizes "nwt" over others (for display and "pick first" cascade logic).
/// - Music: sort prioritizes "osg" over others (for display in publication/music containers).
/// </summary>
public static class PublicationCodeHelper
{
    /// <summary>
    /// Bible category: preferred publication codes in priority order (lower index = higher priority).
    /// </summary>
    private static readonly string[] PriorityPublicationCodes = ["nwt", "bi12"];

    /// <summary>
    /// Music category: preferred publication code first (osg = Original Songs).
    /// </summary>
    private static readonly string[] MusicPriorityPublicationCodes = ["osg"];

    /// <summary>
    /// Comparer for Bible publication codes (nwt first, then bi12). Use for Bible category only.
    /// </summary>
    public static IComparer<string?> PublicationCodeComparer { get; } = new PriorityPublicationCodeComparer();

    /// <summary>
    /// Returns a comparer for the given category. Bible (or null) = nwt first; Music = osg first; others = no priority.
    /// </summary>
    public static IComparer<string?> GetPublicationCodeComparerForCategory(string? categoryName)
    {
        var c = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        if (string.Equals(c, "Music", StringComparison.OrdinalIgnoreCase))
            return new CategoryPriorityPublicationCodeComparer(MusicPriorityPublicationCodes);
        if (string.Equals(c, "Bible", StringComparison.OrdinalIgnoreCase))
            return PublicationCodeComparer;
        return new CategoryPriorityPublicationCodeComparer(Array.Empty<string>());
    }

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
            // null/empty codes come last
            return PriorityPublicationCodes.Length;
        }

        var lower = normalized.ToLowerInvariant();
        for (var i = 0; i < PriorityPublicationCodes.Length; i++)
        {
            if (lower == PriorityPublicationCodes[i])
            {
                return i;
            }
        }

        // Others come after priority publications
        return PriorityPublicationCodes.Length;
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
                // null last
                return 1;
            }
            if (b == null)
            {
                // null last
                return -1;
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

    /// <summary>
    /// Category-specific priority comparer (e.g. Music = osg first; empty array = no priority).
    /// </summary>
    private sealed class CategoryPriorityPublicationCodeComparer : IComparer<string?>
    {
        private readonly string[] _priorityCodes;

        internal CategoryPriorityPublicationCodeComparer(string[] priorityCodes)
        {
            _priorityCodes = priorityCodes ?? Array.Empty<string>();
        }

        public int Compare(string? x, string? y)
        {
            var a = Normalize(x);
            var b = Normalize(y);

            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            var aPriority = GetPriorityForCodes(a);
            var bPriority = GetPriorityForCodes(b);
            var priorityCompare = aPriority.CompareTo(bPriority);
            if (priorityCompare != 0) return priorityCompare;

            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }

        private int GetPriorityForCodes(string code)
        {
            var lower = code.ToLowerInvariant();
            for (var i = 0; i < _priorityCodes.Length; i++)
            {
                if (lower == _priorityCodes[i].ToLowerInvariant())
                    return i;
            }
            return _priorityCodes.Length;
        }
    }
}

