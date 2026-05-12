#nullable enable

using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Tests;

public sealed class EventHandlerManagerCtorDepsTests
{
    [Fact]
    public void EventHandlerManagerDeps_round_trips_collaborator_slots()
    {
        var sut = new EventHandlerManagerDeps(null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.PositionTracker);
    }

    [Fact]
    public void EventHandlerManagerCallbacks_invoke_and_expose_optional_handlers()
    {
        PlayStatus? statusSet = null;
        var calls = 0;

        var sut = new EventHandlerManagerCallbacks(
            GetCurrentPosition: () => null,
            GetDuration: () => TimeSpan.FromSeconds(3),
            SetStatus: s => statusSet = s,
            OnMediaEnded: _ => calls++,
            OnMediaFailed: null,
            GetMediaOpenedCompletionSource: () => null,
            GetCurrentTrack: () => null);

        Assert.Null(sut.GetCurrentPosition());
        Assert.Equal(TimeSpan.FromSeconds(3), sut.GetDuration());
        sut.SetStatus(PlayStatus.Playing);
        Assert.Equal(PlayStatus.Playing, statusSet);

        sut.OnMediaEnded?.Invoke(EventArgs.Empty);
        Assert.Equal(1, calls);

        Assert.Null(sut.OnMediaFailed);
        Assert.Null(sut.GetMediaOpenedCompletionSource());
        Assert.Null(sut.GetCurrentTrack());
    }
}
