#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationNavigationServiceTests
{
    private sealed class NavPlaylistStub : IPlaylistService
    {
        public Func<string, string, string, Task<KeyValuePair<string, BiblePublicationSection>>>? OnPreviousSection { get; init; }
        public Func<string, string, string, Task<KeyValuePair<string, BiblePublicationSection>>>? OnNextSection { get; init; }
        public Func<string, string, string?, string, Task<TrackNavigationResult>>? OnPreviousTrack { get; init; }
        public Func<string, string, string?, string, Task<TrackNavigationResult>>? OnNextTrack { get; init; }

        public void Dispose()
        {
        }

        public Task MarkTrackAsPlayed(Bible.Alarm.Shared.Models.Media.TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task MarkTrackAsFinished(Bible.Alarm.Shared.Models.Media.TrackMetadata trackMetadata) => Task.CompletedTask;

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem> NextTrack(int scheduleId) =>
            throw new NotImplementedException();

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem?> NextBiblePublicationTrack(int scheduleId) =>
            Task.FromResult<Bible.Alarm.Shared.Models.Media.PlayItem?>(null);

        public Task<List<Bible.Alarm.Shared.Models.Media.PlayItem>> NextTracks(int scheduleId) =>
            Task.FromResult(new List<Bible.Alarm.Shared.Models.Media.PlayItem>());

        public Task SaveLastPlayed(int currentScheduleId) => Task.CompletedTask;

        public Task<int> GetRelevantScheduleToPlay() => Task.FromResult(0);

        public Task MoveToNextBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task MoveToPreviousBiblePublicationTrack(int scheduleId) => Task.CompletedTask;

        public Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            OnNextTrack?.Invoke(languageCode, publicationCode, sectionCode, trackCode)
            ?? Task.FromResult(new TrackNavigationResult(publicationCode, null, new BiblePublicationTrack()));

        public Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode, string publicationCode,
            string? sectionCode, string trackCode) =>
            OnPreviousTrack?.Invoke(languageCode, publicationCode, sectionCode, trackCode)
            ?? Task.FromResult(new TrackNavigationResult(publicationCode, null, new BiblePublicationTrack()));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            OnPreviousSection?.Invoke(languageCode, publicationCode, sectionCode)
            ?? Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("", null!));

        public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode,
            string publicationCode, string sectionCode) =>
            OnNextSection?.Invoke(languageCode, publicationCode, sectionCode)
            ?? Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("", null!));

        public Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId) => Task.FromResult(false);

        public Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId) => Task.FromResult(TimeSpan.Zero);

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem> GetNextPlayItemAsync(
            Bible.Alarm.Shared.Models.Media.TrackMetadata currentTrackMetadata,
            Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            throw new NotImplementedException();

        public Task<Bible.Alarm.Shared.Models.Media.PlayItem> GetPreviousPlayItemAsync(
            Bible.Alarm.Shared.Models.Media.TrackMetadata currentTrackMetadata,
            Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? sectionFetchProgress = null) =>
            throw new NotImplementedException();

        public Task PersistSchedulePointerToFinishedTrackAsync(Bible.Alarm.Shared.Models.Media.TrackMetadata trackMetadata) =>
            Task.CompletedTask;
    }

    private static IServiceScopeFactory CreateScopeFactory(IPlaylistService playlist)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPlaylistService>(playlist);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task MoveToPreviousSectionAsync_false_when_section_missing()
    {
        var playlist = new NavPlaylistStub();
        var sut = new BiblePublicationNavigationService(TestLogging.CreateLogger(), CreateScopeFactory(playlist));
        var schedule = new BiblePublicationSchedule { SectionCode = "", PublicationCode = "nwt", TrackCode = "1" };

        Assert.False(await sut.MoveToPreviousSectionAsync(schedule));
    }

    [Fact]
    public async Task MoveToPreviousSectionAsync_false_when_next_section_value_null()
    {
        var playlist = new NavPlaylistStub
        {
            OnPreviousSection = (_, _, _) =>
                Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", null!)),
        };
        var sut = new BiblePublicationNavigationService(TestLogging.CreateLogger(), CreateScopeFactory(playlist));
        var schedule = new BiblePublicationSchedule { SectionCode = "40", PublicationCode = "nwt", TrackCode = "5" };

        Assert.False(await sut.MoveToPreviousSectionAsync(schedule));
    }

    [Fact]
    public async Task MoveToPreviousSectionAsync_updates_schedule_when_section_found()
    {
        var nextSection = new BiblePublicationSection { SectionCode = "41", Name = "Mark" };
        var playlist = new NavPlaylistStub
        {
            OnPreviousSection = (_, _, _) =>
                Task.FromResult(new KeyValuePair<string, BiblePublicationSection>("k", nextSection)),
        };
        var sut = new BiblePublicationNavigationService(TestLogging.CreateLogger(), CreateScopeFactory(playlist));
        var schedule = new BiblePublicationSchedule { SectionCode = "42", PublicationCode = "nwt", TrackCode = "9" };

        Assert.True(await sut.MoveToPreviousSectionAsync(schedule));
        Assert.Equal("41", schedule.SectionCode);
        Assert.Equal("1", schedule.TrackCode);
        Assert.Equal(TimeSpan.Zero, schedule.FinishedDuration);
    }

    [Fact]
    public async Task MoveToNextTrackAsync_updates_codes_from_navigation_result()
    {
        var section = new BiblePublicationSection { SectionCode = "50" };
        var track = new BiblePublicationTrack { TrackCode = "7", Title = "Ch 7" };
        var playlist = new NavPlaylistStub
        {
            OnNextTrack = (_, _, _, _) =>
                Task.FromResult(new TrackNavigationResult("nwt", section, track)),
        };
        var sut = new BiblePublicationNavigationService(TestLogging.CreateLogger(), CreateScopeFactory(playlist));
        var schedule = new BiblePublicationSchedule
        {
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "1",
            LanguageCode = "E",
        };

        Assert.True(await sut.MoveToNextTrackAsync(schedule));
        Assert.Equal("50", schedule.SectionCode);
        Assert.Equal("7", schedule.TrackCode);
        Assert.Equal("nwt", schedule.PublicationCode);
        Assert.Equal(TimeSpan.Zero, schedule.FinishedDuration);
    }
}
