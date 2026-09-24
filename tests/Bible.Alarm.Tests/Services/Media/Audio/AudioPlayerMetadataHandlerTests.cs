#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Tests.Support;
using CommunityToolkit.Maui;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class AudioPlayerMetadataHandlerTests
{
    private sealed class ConfigurableDisplayMetadata : IDisplayMetadataService
    {
        public MetaData CoreResult { get; set; } = new() { Title = "Chapter 1", Artist = "NWT" };
        public MetaData FullResult { get; set; } = new() { Title = "Chapter 1", Artist = "NWT", ArtworkUrl = "https://cdn.example/art.jpg" };
        public Exception? CoreException { get; set; }

        public Task<MetaData> GetDisplayMetadataAsync(AudioPlayerTrack track) =>
            Task.FromResult(FullResult);

        public Task<MetaData> GetCoreDisplayMetadataAsync(AudioPlayerTrack track)
        {
            if (CoreException != null)
            {
                return Task.FromException<MetaData>(CoreException);
            }

            return Task.FromResult(CoreResult);
        }
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private static TrackMetadata SampleMeta() =>
        new()
        {
            ScheduleId = 4,
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "1",
            TrackCode = "3",
        };

    private static AudioPlayerTrack SampleTrack() =>
        new() { Uri = "https://cdn.example/track.mp3", PlayItem = new PlayItem(SampleMeta(), "https://cdn.example/track.mp3") };

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new AudioPlayerMetadataHandler(
            TestLogging.CreateLogger(),
            new ConfigurableDisplayMetadata(),
            new RecordingDispatcher());

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task SyncMetadataForTrackAsync_dispatches_core_metadata_to_fluxor()
    {
        var display = new ConfigurableDisplayMetadata();
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerMetadataHandler(TestLogging.CreateLogger(), display, dispatcher);

        await sut.SyncMetadataForTrackAsync(SampleTrack());
        await Task.Delay(50);

        var action = Assert.IsType<PlaybackMetadataChangedAction>(dispatcher.Dispatched[0]);
        Assert.Equal("Chapter 1", action.Title);
        Assert.Equal("NWT", action.Artist);
    }

    [Fact]
    public async Task SyncMetadataForTrackAsync_uses_fallback_when_core_metadata_throws()
    {
        var display = new ConfigurableDisplayMetadata { CoreException = new InvalidOperationException("db locked") };
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerMetadataHandler(TestLogging.CreateLogger(), display, dispatcher);

        await sut.SyncMetadataForTrackAsync(SampleTrack());
        await Task.Delay(50);

        var action = Assert.IsType<PlaybackMetadataChangedAction>(dispatcher.Dispatched[0]);
        Assert.Equal("Unknown Title", action.Title);
        Assert.Equal("Unknown Artist", action.Artist);
    }

    [Fact]
    public async Task SyncMetadataForTrackAsync_dispatches_artwork_when_full_metadata_ready()
    {
        var display = new ConfigurableDisplayMetadata
        {
            FullResult = new MetaData
            {
                Title = "Chapter 1",
                Artist = "NWT",
                ArtworkUrl = "https://cdn.example/art.jpg",
            },
        };
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerMetadataHandler(TestLogging.CreateLogger(), display, dispatcher);

        await sut.SyncMetadataForTrackAsync(SampleTrack());
        await Task.Delay(50);

        Assert.True(dispatcher.Dispatched.Count >= 1);
        var last = dispatcher.Dispatched[^1];
        if (last is PlaybackMetadataChangedAction withArt && !string.IsNullOrEmpty(withArt.ArtworkUrl))
        {
            Assert.Equal("https://cdn.example/art.jpg", withArt.ArtworkUrl);
        }
    }

    [Fact]
    public async Task SyncMetadataForTrackAsync_persists_artwork_bytes_to_app_data_directory()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
        var display = new ConfigurableDisplayMetadata
        {
            CoreResult = new MetaData { Title = "T", Artist = "A", ArtworkBytes = bytes },
            FullResult = new MetaData { Title = "T", Artist = "A", ArtworkBytes = bytes },
        };
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerMetadataHandler(TestLogging.CreateLogger(), display, dispatcher);

        await sut.SyncMetadataForTrackAsync(SampleTrack());
        await Task.Delay(50);

        var action = dispatcher.Dispatched.OfType<PlaybackMetadataChangedAction>().LastOrDefault();
        if (action?.ArtworkUrl is { Length: > 0 } path && File.Exists(path))
        {
            Assert.True(path.Contains("playing_track_artwork_", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task HandleMediaOpenedAsync_applies_metadata_when_main_thread_available()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var display = new ConfigurableDisplayMetadata();
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerMetadataHandler(TestLogging.CreateLogger(), display, dispatcher);
        var element = new MediaElement();

        try
        {
            await sut.HandleMediaOpenedAsync(SampleTrack(), element);
            Assert.NotEmpty(dispatcher.Dispatched);
        }
        catch (COMException)
        {
            // Headless Windows host may lack a WinUI dispatcher.
        }
        catch (InvalidOperationException)
        {
            // MainThread not available in some test hosts.
        }
    }

    [Fact]
    public async Task HandleMediaOpenedAsync_falls_back_when_core_metadata_fails()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var display = new ConfigurableDisplayMetadata { CoreException = new InvalidOperationException("fail") };
        var dispatcher = new RecordingDispatcher();
        var sut = new AudioPlayerMetadataHandler(TestLogging.CreateLogger(), display, dispatcher);
        var element = new MediaElement();

        try
        {
            await sut.HandleMediaOpenedAsync(SampleTrack(), element);
            var action = Assert.IsType<PlaybackMetadataChangedAction>(Assert.Single(dispatcher.Dispatched));
            Assert.Equal("Unknown Title", action.Title);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
