#nullable enable

using System;

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Whether publication-language queries should filter to rows marked as music for the Music category browse path.
/// </summary>
public static class MusicCategoryPublicationQueryGate
{
    public static bool AppliesMusicOnlyFilter(string? categoryName, bool requireIsMusicForMusicCategory)
    {
        return requireIsMusicForMusicCategory
            && string.Equals(categoryName, AppConstants.Media.BiblePublicationCategoryMusic, StringComparison.OrdinalIgnoreCase);
    }
}
