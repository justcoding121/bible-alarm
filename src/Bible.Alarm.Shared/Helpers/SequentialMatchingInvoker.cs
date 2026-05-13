#nullable enable

using System;

using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Invokes an action for each sequence item matching a predicate, counting matches even when the action throws.
/// </summary>
public static class SequentialMatchingInvoker
{
    public static int InvokeEachMatching<T>(
        IEnumerable<T> sequence,
        Func<T, bool> isMatch,
        Action<T> invoke,
        Action<T, Exception>? onFailure)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(isMatch);
        ArgumentNullException.ThrowIfNull(invoke);

        var matched = 0;
        foreach (var item in sequence)
        {
            if (!isMatch(item))
            {
                continue;
            }

            matched++;
            try
            {
                invoke(item);
            }
            catch (Exception ex)
            {
                onFailure?.Invoke(item, ex);
            }
        }

        return matched;
    }
}
