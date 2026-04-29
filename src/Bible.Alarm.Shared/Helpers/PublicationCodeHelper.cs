#nullable enable
using System;
using System.Collections.Generic;
using Bible.Alarm.Shared.Constants;

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
    private static readonly string[] PriorityPublicationCodes =
    [
        AppConstants.Media.BiblePublicationCodeNwt,
        AppConstants.Media.BiblePublicationCodeBi12
    ];

    /// <summary>
    /// Music category: preferred publication code first (osg = Original Songs).
    /// </summary>
    private static readonly string[] MusicPriorityPublicationCodes =
        [AppConstants.Media.MusicPublicationCodeOsg];

    /// <summary>
    /// Comparer for Bible publication codes (nwt first, then bi12). Use for Bible category only.
    /// </summary>
    public static IComparer<string?> PublicationCodeComparer { get; } = new PriorityPublicationCodeComparer();

    /// <summary>
    /// Returns a display comparer for the given category. Bible (or null) = nwt first; Music = osg first;
    /// magazine categories = latest year first; others = no priority.
    /// Use for UI publication lists only; navigation should use <see cref="GetNavigationComparerForCategory"/>.
    /// </summary>
    public static IComparer<string?> GetPublicationCodeComparerForCategory(string? categoryName)
    {
        var c = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        if (string.Equals(c, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase))
            return new CategoryPriorityPublicationCodeComparer(MusicPriorityPublicationCodes);
        if (string.Equals(c, AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase))
            return PublicationCodeComparer;
        if (string.Equals(c, AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c, AppConstants.Media.BiblePublicationCategoryAwakeMagazine, StringComparison.OrdinalIgnoreCase))
            return MagazineDescendingYearComparer;
        return new CategoryPriorityPublicationCodeComparer(Array.Empty<string>());
    }

    /// <summary>
    /// Returns a comparer for chronological/navigation order.
    /// Magazines sort by year ascending (oldest first) so that next/previous track
    /// navigation moves forward/backward through time.
    /// All other categories use the same comparer as <see cref="GetPublicationCodeComparerForCategory"/>.
    /// </summary>
    public static IComparer<string?> GetNavigationComparerForCategory(string? categoryName)
    {
        var c = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        if (string.Equals(c, AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c, AppConstants.Media.BiblePublicationCategoryAwakeMagazine, StringComparison.OrdinalIgnoreCase))
            return new CategoryPriorityPublicationCodeComparer(Array.Empty<string>());
        return GetPublicationCodeComparerForCategory(categoryName);
    }

    /// <summary>
    /// Normalizes a publication code: trims and converts whitespace-only to null.
    /// </summary>
    public static string? Normalize(string? publicationCode)
        => string.IsNullOrWhiteSpace(publicationCode) ? null : publicationCode.Trim();

    /// <summary>
    /// Returns true when both publication codes are equal: if both numeric, same integer value; otherwise string equal (ignore case).
    /// </summary>
    public static bool CodeEquals(string? a, string? b) => CodeComparisonHelper.Equals(Normalize(a), Normalize(b));

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

            return CodeComparisonHelper.Compare(a, b);
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

            return CodeComparisonHelper.Compare(a, b);
        }

        private int GetPriorityForCodes(string code)
        {
            for (var i = 0; i < _priorityCodes.Length; i++)
            {
                if (string.Equals(code, _priorityCodes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return _priorityCodes.Length;
        }
    }

    private static IComparer<string?> MagazineDescendingYearComparer { get; } = new MagazineYearDescendingComparer();

    /// <summary>
    /// Sorts magazine publication codes by year descending (latest first).
    /// Falls back to ordinal string compare for non-magazine codes.
    /// </summary>
    private sealed class MagazineYearDescendingComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y)
        {
            var a = Normalize(x);
            var b = Normalize(y);

            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            var aIsMag = MagazineHelper.IsMagazinePublicationCode(a);
            var bIsMag = MagazineHelper.IsMagazinePublicationCode(b);

            if (aIsMag && bIsMag)
            {
                return MagazineHelper.GetYear(b).CompareTo(MagazineHelper.GetYear(a));
            }

            if (aIsMag) return -1;
            if (bIsMag) return 1;

            return CodeComparisonHelper.Compare(a, b);
        }
    }
}

