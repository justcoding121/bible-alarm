#nullable enable

using System;
using System.Globalization;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Compares codes (track, section, publication) consistently: numeric when both parse as int, otherwise string.
/// Use for ordering and equality so "1" and "01" compare equal when treated as numeric, and "jwb-201708" compares as string.
/// </summary>
public static class CodeComparisonHelper
{
    /// <summary>
    /// Compares two codes: if both are numeric, compares by integer value; otherwise by string (ordinal ignore case).
    /// </summary>
    public static int Compare(string? a, string? b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return -1;
        if (b == null)
            return 1;

        var aIsInt = int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aInt);
        var bIsInt = int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bInt);

        if (aIsInt && bIsInt)
            return aInt.CompareTo(bInt);

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true when both codes are equal: if both numeric, same integer value; otherwise string equal (ignore case).
    /// </summary>
    public static bool Equals(string? a, string? b)
    {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;

        var aIsInt = int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aInt);
        var bIsInt = int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bInt);

        if (aIsInt && bIsInt)
            return aInt == bInt;

        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
