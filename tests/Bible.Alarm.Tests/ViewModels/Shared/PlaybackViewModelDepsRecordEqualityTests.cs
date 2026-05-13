#nullable enable

using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class PlaybackViewModelDepsRecordEqualityTests
{
    [Fact]
    public void Instances_with_matching_null_slots_are_equal()
    {
        var a = new PlaybackViewModelDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var b = new PlaybackViewModelDeps(
            a.Logger,
            a.PlaybackService,
            a.SchedulePlaybackService,
            a.PlaybackState,
            a.ReviewPromptService,
            a.AudioPlayer,
            a.MainThreadScheduler);

        Assert.Equal(a, b);
    }
}
