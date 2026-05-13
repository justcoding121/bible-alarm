#nullable enable

using Bible.Alarm.Services.Media.AudioPlayerHelpers;

namespace Bible.Alarm.Tests;

public sealed class EventHandlerManagerRecordBundlesTests
{
    [Fact]
    public void EventHandlerManagerDeps_instances_with_null_collaborators_compare_equal()
    {
        var a = new EventHandlerManagerDeps(null!, null!, null!, null!);
        var b = new EventHandlerManagerDeps(a.Logger, a.StateManager, a.MetadataHandler, a.PositionTracker);
        Assert.Equal(a, b);
    }

    [Fact]
    public void EventHandlerManagerCallbacks_instances_with_null_handlers_compare_equal()
    {
        var a = new EventHandlerManagerCallbacks(
            null!,
            null!,
            null!,
            null,
            null,
            null!,
            null!);

        var b = new EventHandlerManagerCallbacks(
            a.GetCurrentPosition,
            a.GetDuration,
            a.SetStatus,
            a.OnMediaEnded,
            a.OnMediaFailed,
            a.GetMediaOpenedCompletionSource,
            a.GetCurrentTrack);

        Assert.Equal(a, b);
    }
}
