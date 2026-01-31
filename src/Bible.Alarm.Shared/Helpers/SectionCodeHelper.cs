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
    /// Extracts a numeric section index from a section code when possible.
    /// Returns 0 when section code is null/empty or not parseable.
    /// </summary>
    public static int GetSectionIndexOrZero(string? sectionCode)
    {
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return 0;
        }

        // Direct numeric section codes (e.g., "1")
        if (int.TryParse(sectionCode, out var numeric))
        {
            return numeric;
        }

        // Suffix pattern (e.g., "iam-1")
        var parts = sectionCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 1 && int.TryParse(parts[^1], out var extracted))
        {
            return extracted;
        }

        return 0;
    }

    /// <summary>
    /// Normalizes section code for persistence: trims and converts whitespace-only to null.
    /// </summary>
    public static string? Normalize(string? sectionCode)
        => string.IsNullOrWhiteSpace(sectionCode) ? null : sectionCode.Trim();

    private sealed class NaturalSectionCodeComparer : IComparer<string?>
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
                return -1;
            }
            if (b == null)
            {
                return 1;
            }

            var aIsInt = int.TryParse(a, out var aInt);
            var bIsInt = int.TryParse(b, out var bInt);

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
            return StringComparer.OrdinalIgnoreCase.Compare(a, b);
        }
    }
}

