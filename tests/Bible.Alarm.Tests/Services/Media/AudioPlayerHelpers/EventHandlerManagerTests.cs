#nullable enable

using Bible.Alarm.Services.Media.AudioPlayerHelpers;

namespace Bible.Alarm.Tests;

public sealed class EventHandlerManagerTests
{
    [Fact]
    public void Ctor_wires_deps_and_callbacks()
    {
        var deps = new EventHandlerManagerDeps(null!, null!, null!, null!);
        var callbacks = new EventHandlerManagerCallbacks(
            GetCurrentPosition: () => null,
            GetDuration: () => TimeSpan.Zero,
            SetStatus: _ => { },
            OnMediaEnded: null,
            OnMediaFailed: null,
            GetMediaOpenedCompletionSource: () => null,
            GetCurrentTrack: () => null);

        var sut = new EventHandlerManager(deps, callbacks);
        Assert.NotNull(sut);
    }
}
