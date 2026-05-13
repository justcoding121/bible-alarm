#nullable enable

using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class AudioPlayerMetadataHandlerTests
{
    private sealed class NopDisplayMetadata : IDisplayMetadataService
    {
        public Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track) =>
            Task.FromResult(new MetaData());

        public Task<MetaData> GetCoreDisplayMetadataAsync(AudioPlayerTrack track) =>
            Task.FromResult(new MetaData());
    }

#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new AudioPlayerMetadataHandler(
            TestLogging.CreateLogger(),
            new NopDisplayMetadata(),
            new NopDispatcher());

        Assert.NotNull(sut);
    }
}
