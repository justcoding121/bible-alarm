#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class DefaultMusicPopulatorTests
{
    private sealed class StubMelodyMusicService : IMelodyMusicService
    {
        public Dictionary<string, MelodyMusic> AllAsyncResult { get; set; } = [];

        public MelodyMusic? ByCodeResult { get; set; }

        public void Dispose()
        {
        }

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(ByCodeResult);

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AllAsyncResult);

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static AlarmSchedule BaseSchedule(int id = 5) =>
        new()
        {
            Id = id,
            Name = "Wake",
            IsEnabled = true,
            Hour = 6,
            Minute = 30,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
        };

    [Fact]
    public async Task PopulateBatchAsync_WhenMelodyServiceNull_LeavesStateEmpty()
    {
        var sut = new DefaultMusicPopulator(melodyMusicService: null);
        var state = new ScheduleStateItem();

        await sut.PopulateBatchAsync([BaseSchedule()], [state]);

        Assert.True(string.IsNullOrEmpty(state.MusicPublicationCode));
        Assert.True(string.IsNullOrWhiteSpace(state.MusicTrackCode));
    }

    [Fact]
    public async Task PopulateBatchAsync_WhenMusicAlreadyConfigured_DoesNotQueryMelodyService()
    {
        var throwing = new ThrowingMelodyMusicService();
        var sut = new DefaultMusicPopulator(throwing);
        var state = new ScheduleStateItem
        {
            MusicPublicationCode = "iam",
            MusicTrackCode = "1",
        };

        await sut.PopulateBatchAsync([BaseSchedule()], [state]);
    }

    [Fact]
    public async Task PopulateBatchAsync_WhenCatalogEmpty_LeavesStateEmpty()
    {
        var sut = new DefaultMusicPopulator(new StubMelodyMusicService { AllAsyncResult = [] });
        var state = new ScheduleStateItem();

        await sut.PopulateBatchAsync([BaseSchedule()], [state]);

        Assert.True(string.IsNullOrEmpty(state.MusicPublicationCode));
    }

    [Fact]
    public async Task PopulateBatchAsync_WhenIamAvailable_SetsDefaultTrackFields()
    {
        var track = new BiblePublicationTrack { TrackCode = "9", Title = "Nine" };
        var publication = new BiblePublication
        {
            Name = "Kingdom Melodies",
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            LanguageId = null,
            IsVideo = false,
            IsMusic = true,
            Tracks = [track],
        };

        var melody = new MelodyMusic { Publication = publication };

        var service = new StubMelodyMusicService
        {
            AllAsyncResult = new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] = melody,
            },
            ByCodeResult = melody,
        };

        var sut = new DefaultMusicPopulator(service);
        var state = new ScheduleStateItem();

        await sut.PopulateBatchAsync([BaseSchedule()], [state]);

        Assert.Equal(AppConstants.Media.MelodyMusicPublicationCodeIam, state.MusicPublicationCode);
        Assert.Equal("Kingdom Melodies", state.MusicPublicationName);
        Assert.Equal(AppConstants.Media.DefaultLanguageCode, state.MusicLanguageCode);
        Assert.Equal("9", state.MusicTrackCode);
        Assert.Equal("Nine", state.MusicTrackName);
        Assert.False(state.MusicRepeat);
    }

    private sealed class ThrowingMelodyMusicService : IMelodyMusicService
    {
        public void Dispose()
        {
        }

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
    }
}
