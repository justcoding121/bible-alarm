#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Plans which host index to try on each of the three GETPUBMEDIALINKS attempts.
/// </summary>
public static class PubMediaLinksHostAttemptPlanner
{
    public static int[] BuildThreeAttemptHostIndices(int urlCount, Func<int, int> nextExclusiveBelowCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(urlCount, 0);

        if (urlCount == 1)
        {
            return [0, 0, 0];
        }

        var firstIndex = nextExclusiveBelowCount(urlCount);
        return [firstIndex, (firstIndex + 1) % urlCount, nextExclusiveBelowCount(urlCount)];
    }
}
