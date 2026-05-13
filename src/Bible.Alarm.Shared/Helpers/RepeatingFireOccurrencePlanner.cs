#nullable enable

using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Mirrors Windows notification scheduling iteration (advance cursor, horizon cutoff, skip fires not after latest clock reading).
/// </summary>
public static class RepeatingFireOccurrencePlanner
{
    public static List<DateTimeOffset> CollectFutureOccurrences(
        Func<DateTimeOffset, DateTimeOffset> getNextFireAfter,
        Func<DateTimeOffset> clock,
        TimeSpan lookahead,
        int maxIterations)
    {
        ArgumentNullException.ThrowIfNull(getNextFireAfter);
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock();
        var maxDate = now + lookahead;
        var currentDate = now;

        var results = new List<DateTimeOffset>();

        for (var i = 0; i < maxIterations; i++)
        {
            var fireDate = getNextFireAfter(currentDate);

            if (fireDate > maxDate)
            {
                break;
            }

            var liveNow = clock();
            if (fireDate <= liveNow)
            {
                currentDate = fireDate;
                continue;
            }

            results.Add(fireDate);
            currentDate = fireDate;
        }

        return results;
    }
}
