#nullable enable
using System;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Stores.Effects.Services.Helpers;

/// <summary>
/// Utility class for converting section codes to integers.
/// </summary>
internal static class SectionCodeConverter
{
    /// <summary>
    /// Converts SectionCode (string) to the int section number needed for media service calls.
    /// Uses SectionCodeHelper.GetSectionIndexOrZero to support codes like "iam-1".
    /// Returns 0 for null/empty (non-sectioned publications).
    /// </summary>
    public static Task<int> ConvertToIntAsync(string? sectionCode, string languageCode, string publicationCode)
    {
        // languageCode/publicationCode are currently unused, but kept to avoid changing call sites.
        return Task.FromResult(SectionCodeHelper.GetSectionIndexOrZero(sectionCode));
    }
}
