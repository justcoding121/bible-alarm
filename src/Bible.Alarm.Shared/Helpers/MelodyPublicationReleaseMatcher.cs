#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Determines whether a publication code refers to a melody (no-language) release exposed by <see cref="Services.Media.IMediaService.GetMelodyMusicReleases"/>.
/// </summary>
public static class MelodyPublicationReleaseMatcher
{
    public static bool MatchesReleaseKeys(IEnumerable<string> melodyReleaseKeys, string? publicationCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode))
        {
            return false;
        }

        return melodyReleaseKeys.Any(key =>
            string.Equals(key, publicationCode, StringComparison.OrdinalIgnoreCase));
    }
}
