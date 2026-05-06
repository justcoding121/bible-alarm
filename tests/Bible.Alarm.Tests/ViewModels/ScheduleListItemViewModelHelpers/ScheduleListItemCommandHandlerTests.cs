#nullable enable

using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Tests;

public sealed class ScheduleListItemCommandHandlerTests
{
    private sealed class RecordingToastMessenger : IDisposable
    {
        public List<string> ToastValues { get; } = [];

        public RecordingToastMessenger() =>
            WeakReferenceMessenger.Default.Register<ShowToastMessage>(this, (r, m) => ToastValues.Add(m.Value));

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<ShowToastMessage>(this);
    }

    private sealed class RecordingPlaybackService : ISchedulePlaybackService
    {
        public List<int> PlayScheduleIds { get; } = [];
        public bool CanMoveTrack { get; set; } = true;

        public Task<bool> CanMoveTrackAsync(int scheduleId) => Task.FromResult(CanMoveTrack);

        public Task PlayScheduleAsync(int scheduleId)
        {
            PlayScheduleIds.Add(scheduleId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPlaylistService : IPlaylistService
    {
        public List<int> PreviousCalls { get; } = [];
        public List<int> NextCalls { get; } = [];

        public Exception? ThrowOnPrevious { get; set; }
        public Exception? ThrowOnNext { get; set; }

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(Bible.Alarm.Shared.Models.Media.TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(Bible.Alarm.Shared.Models.Media.TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem> NextTrack(int scheduleId) =>
            throw new NotImplementedException();

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            throw new NotImplementedException();

        public Task<List<Bible.Alarm.Shared.Models.Media.PlayItem>> NextTracks(int scheduleId) =>
            throw new NotImplementedException();

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId)
        {
            if (ThrowOnNext != null)
            {
                throw ThrowOnNext;
            }

            NextCalls.Add(scheduleId);
            return Task.CompletedTask;
        }

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId)
        {
            if (ThrowOnPrevious != null)
            {
                throw ThrowOnPrevious;
            }

            PreviousCalls.Add(scheduleId);
            return Task.CompletedTask;
        }

        public Task<Bible.Alarm.Services.Media.PlaylistServiceHelpers.TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode, string? sectionCode, string trackCode) =>
            throw new NotImplementedException();

        public Task<Bible.Alarm.Services.Media.PlaylistServiceHelpers.TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode, string? sectionCode, string trackCode) =>
            throw new NotImplementedException();

        public Task<KeyValuePair<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            throw new NotImplementedException();

        public Task<KeyValuePair<string, Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode, string sectionCode) =>
            throw new NotImplementedException();

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem> GetNextPlayItemAsync(Bible.Alarm.Shared.Models.Media.TrackMetadata currentTrackMetadata, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            throw new NotImplementedException();

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem> GetPreviousPlayItemAsync(Bible.Alarm.Shared.Models.Media.TrackMetadata currentTrackMetadata, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            throw new NotImplementedException();

        public Task PersistSchedulePointerToFinishedTrackAsync(Bible.Alarm.Shared.Models.Media.TrackMetadata trackMetadata) => Task.CompletedTask;
    }

    private static AlarmSchedule ValidScheduleWithBible()
    {
        return new AlarmSchedule
        {
            Id = 42,
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                PublicationCode = "nwt",
                TrackCode = "1",
            },
        };
    }

    private static async Task ExecuteCommandAsync(ICommand command)
    {
        if (command is IAsyncRelayCommand asyncRelay)
        {
            await asyncRelay.ExecuteAsync(null);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand");
    }

    [Fact]
    public async Task CreatePlayCommand_NoOps_When_ScheduleMissing()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        var overlayStarted = false;
        await ExecuteCommandAsync(sut.CreatePlayCommand(null, () => overlayStarted = true));

        Assert.False(overlayStarted);
        Assert.Empty(playback.PlayScheduleIds);
    }

    [Fact]
    public async Task CreatePlayCommand_NoOps_When_ScheduleId_Invalid()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        var schedule = ValidScheduleWithBible();
        schedule.Id = 0;

        await ExecuteCommandAsync(sut.CreatePlayCommand(schedule, () => { }));

        Assert.Empty(playback.PlayScheduleIds);
    }

    [Fact]
    public async Task CreatePlayCommand_Starts_Playback_For_Valid_Schedule()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreatePlayCommand(ValidScheduleWithBible(), () => { }));

        Assert.Equal([42], playback.PlayScheduleIds);
    }

    [Fact]
    public async Task CreatePreviousCommand_NoOps_When_Bible_Not_Configured()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        var schedule = ValidScheduleWithBible();
        schedule.BiblePublicationSchedule = null;

        await ExecuteCommandAsync(sut.CreatePreviousCommand(schedule));

        Assert.Empty(playlist.PreviousCalls);
    }

    [Fact]
    public async Task CreatePreviousCommand_NoOps_When_Cannot_Move()
    {
        var playback = new RecordingPlaybackService { CanMoveTrack = false };
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreatePreviousCommand(ValidScheduleWithBible()));

        Assert.Empty(playlist.PreviousCalls);
    }

    [Fact]
    public async Task CreatePreviousCommand_Moves_When_Allowed()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreatePreviousCommand(ValidScheduleWithBible()));

        Assert.Equal([42], playlist.PreviousCalls);
    }

    [Fact]
    public async Task CreateNextCommand_NoOps_When_Bible_Not_Configured()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        var schedule = ValidScheduleWithBible();
        schedule.BiblePublicationSchedule = null;

        await ExecuteCommandAsync(sut.CreateNextCommand(schedule));

        Assert.Empty(playlist.NextCalls);
    }

    [Fact]
    public async Task CreateNextCommand_Moves_When_Allowed()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreateNextCommand(ValidScheduleWithBible()));

        Assert.Equal([42], playlist.NextCalls);
    }

    [Fact]
    public async Task CreatePlayCommand_invokes_onPlayStarted_before_playback()
    {
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        var started = false;
        await ExecuteCommandAsync(sut.CreatePlayCommand(ValidScheduleWithBible(), () => started = true));

        Assert.True(started);
        Assert.Equal([42], playback.PlayScheduleIds);
    }

    [Fact]
    public async Task CreatePreviousCommand_sends_schedule_not_found_toast_when_schedule_null()
    {
        using var toasts = new RecordingToastMessenger();
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreatePreviousCommand(null));

        Assert.Equal("Schedule not found", Assert.Single(toasts.ToastValues));
        Assert.Empty(playlist.PreviousCalls);
    }

    [Fact]
    public async Task CreateNextCommand_sends_schedule_not_found_toast_when_schedule_id_invalid()
    {
        using var toasts = new RecordingToastMessenger();
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        var schedule = ValidScheduleWithBible();
        schedule.Id = -1;

        await ExecuteCommandAsync(sut.CreateNextCommand(schedule));

        Assert.Equal("Schedule not found", Assert.Single(toasts.ToastValues));
        Assert.Empty(playlist.NextCalls);
    }

    [Fact]
    public async Task CreateNextCommand_NoOps_When_Cannot_Move()
    {
        var playback = new RecordingPlaybackService { CanMoveTrack = false };
        var playlist = new RecordingPlaylistService();
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreateNextCommand(ValidScheduleWithBible()));

        Assert.Empty(playlist.NextCalls);
    }

    [Fact]
    public async Task CreatePreviousCommand_sends_error_toast_when_playlist_throws()
    {
        using var toasts = new RecordingToastMessenger();
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService { ThrowOnPrevious = new InvalidOperationException("db") };
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreatePreviousCommand(ValidScheduleWithBible()));

        Assert.Equal("Error moving to previous track", Assert.Single(toasts.ToastValues));
    }

    [Fact]
    public async Task CreateNextCommand_sends_error_toast_when_playlist_throws()
    {
        using var toasts = new RecordingToastMessenger();
        var playback = new RecordingPlaybackService();
        var playlist = new RecordingPlaylistService { ThrowOnNext = new InvalidOperationException("db") };
        var sut = new Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers.ScheduleListItemCommandHandler(
            TestLogging.CreateLogger(),
            playback,
            playlist);

        await ExecuteCommandAsync(sut.CreateNextCommand(ValidScheduleWithBible()));

        Assert.Equal("Error moving to next track", Assert.Single(toasts.ToastValues));
    }
}
