#nullable enable
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Stores.Effects.Services.Helpers;

/// <summary>
/// Utility class for converting section codes to integers.
/// </summary>
internal static class SectionCodeConverter
{
    /// <summary>
    /// Converts SectionCode (string) to the int section number needed for media service calls.
    /// Tries to parse SectionCode to int.
    /// Returns 0 for null/empty (non-sectioned publications).
    /// </summary>
    public static Task<int> ConvertToIntAsync(string? sectionCode, string languageCode, string publicationCode)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            return Task.FromResult(0);
        }

        // Try to parse SectionCode directly to int
        if (int.TryParse(sectionCode, out var sectionNumber))
        {
            return Task.FromResult(sectionNumber);
        }

        // If parsing fails, SectionCode is not numeric (e.g., "gen" for Genesis)
        // For non-numeric section codes, return 0
        return Task.FromResult(0);
    }
}
