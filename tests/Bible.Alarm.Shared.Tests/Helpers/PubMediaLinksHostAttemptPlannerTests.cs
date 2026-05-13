#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class PubMediaLinksHostAttemptPlannerTests
{
    [Fact]
    public void BuildThreeAttemptHostIndices_throws_when_host_count_not_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PubMediaLinksHostAttemptPlanner.BuildThreeAttemptHostIndices(0, _ => 0));
    }

    [Fact]
    public void BuildThreeAttemptHostIndices_with_single_host_always_targets_index_zero()
    {
        var indices = PubMediaLinksHostAttemptPlanner.BuildThreeAttemptHostIndices(
            urlCount: 1,
            nextExclusiveBelowCount: _ => throw new InvalidOperationException("RNG must not run for single-host plans"));

        Assert.Equal([0, 0, 0], indices);
    }

    [Fact]
    public void BuildThreeAttemptHostIndices_second_attempt_targets_next_host_using_modulo_wrap()
    {
        var indices = PubMediaLinksHostAttemptPlanner.BuildThreeAttemptHostIndices(
            urlCount: 5,
            nextExclusiveBelowCount: new RngQueue(4, 1).NextBelow);

        Assert.Equal([4, 0, 1], indices);
    }

    private sealed class RngQueue
    {
        private readonly Queue<int> queue;

        public RngQueue(params int[] sequence) =>
            queue = new Queue<int>(sequence);

        public int NextBelow(int upperExclusive)
        {
            var next = queue.Dequeue();
            if (next >= upperExclusive)
            {
                throw new InvalidOperationException("RNG produced out-of-range index.");
            }

            return next;
        }
    }
}
