#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class PlaybackModalServiceTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        PlaybackModalService sut = null!;
        try
        {
            sut = new PlaybackModalService(
                TestLogging.CreateLogger(),
                null!,
                new FakePlaybackState(new PlaybackState()),
                null!,
                new NopDispatcher());

            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
