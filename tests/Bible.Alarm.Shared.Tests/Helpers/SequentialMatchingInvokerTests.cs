#nullable enable

using System;
using System.Collections.Generic;

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class SequentialMatchingInvokerTests
{
    [Fact]
    public void InvokeEachMatching_counts_every_matching_item_even_when_invoke_throws()
    {
        var failedKeys = new List<int>();

        var matchedCount = SequentialMatchingInvoker.InvokeEachMatching(
            new[] { 1, 2, 3 },
            n => n % 2 == 1,
            n =>
            {
                if (n == 3)
                {
                    throw new InvalidOperationException();
                }
            },
            (n, _) => failedKeys.Add(n));

        Assert.Equal(2, matchedCount);
        Assert.Equal(new[] { 3 }, failedKeys);
    }

    [Fact]
    public void InvokeEachMatching_skips_items_that_fail_predicate_without_calling_invoke()
    {
        var invoked = new List<char>();

        var matchedCount = SequentialMatchingInvoker.InvokeEachMatching(
            new[] { 'a', 'b' },
            ch => ch == 'z',
            ch => invoked.Add(ch),
            null);

        Assert.Equal(0, matchedCount);
        Assert.Empty(invoked);
    }

    [Fact]
    public void InvokeEachMatching_throws_when_sequence_null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SequentialMatchingInvoker.InvokeEachMatching<object>(
                sequence: null!,
                _ => true,
                _ => { },
                null));
    }
}
