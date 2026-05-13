#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Maps normalized lower-case mediator publication codes to mediator category keys used by jw.org APIs.
/// </summary>
public static class MediatorVideoPublicationCategoryKeyMapper
{
    public static bool TryGetCategoryKey(string normalizedPublicationCode, [NotNullWhen(true)] out string? categoryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPublicationCode);

        categoryKey = normalizedPublicationCode.ToLowerInvariant() switch
        {
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews => AppConstants.Media.BiblePublicationCodeDramasGoodNews,
            AppConstants.Media.NormalizedPublicationCodeVODMoviesBibleTimes => AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
            AppConstants.Media.NormalizedPublicationCodeVODMoviesModernDay => AppConstants.Media.BiblePublicationCodeVODMoviesModernDay,
            AppConstants.Media.NormalizedPublicationCodeVODMoviesAnimated => AppConstants.Media.BiblePublicationCodeVODMoviesAnimated,
            AppConstants.Media.NormalizedPublicationCodeVODMoviesExtras => AppConstants.Media.BiblePublicationCodeVODMoviesExtras,
            AppConstants.Media.NormalizedPublicationCodeSeriesDigForTreasures => AppConstants.Media.BiblePublicationCodeSeriesDigForTreasures,
            AppConstants.Media.NormalizedPublicationCodeSeriesBJFLessons => AppConstants.Media.BiblePublicationCodeSeriesBJFLessons,
            _ => null,
        };

        return categoryKey != null;
    }
}
