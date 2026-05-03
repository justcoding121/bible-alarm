#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui;

namespace Bible.Alarm.Tests;

public sealed class MusicDisplayTextProviderTests
{
    private sealed class FakeState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class StubMediaService : IMediaService
    {
        public Dictionary<string, Language> BiblePublicationLanguagesResult { get; set; } = [];
        public SortedDictionary<string, BiblePublicationTrack> BiblePublicationTracksResult { get; set; } = [];

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(BiblePublicationLanguagesResult);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(BiblePublicationTracksResult);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, Bible.Alarm.Shared.Services.Media.Interfaces.IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    private static ScheduleStateItem BaseSchedule() =>
        new()
        {
            Id = 1,
            Name = "S",
            IsEnabled = true,
            Hour = 6,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.All,
            NotificationEnabled = true,
            MusicEnabled = true,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Music,
            MusicPublicationCode = "iam-1",
            MusicLanguageCode = "MY",
            MusicLanguageName = "Malay",
            MusicSectionCode = "sec-a",
            MusicTrackCode = "5",
            MusicTrackName = "Track Five",
            MusicPublicationName = "Kingdom Songs",
            MusicRepeat = false,
            MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight
        };

    [Fact]
    public void GetMusicLanguageDisplayText_Returns_Empty_When_No_CurrentSchedule()
    {
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], currentSchedule: null)),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal(string.Empty, sut.GetMusicLanguageDisplayText());
    }

    [Fact]
    public void GetMusicLanguageDisplayText_Returns_Display_Name_When_Set()
    {
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], BaseSchedule())),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal("Malay", sut.GetMusicLanguageDisplayText());
    }

    [Fact]
    public void GetMusicLanguageDisplayText_Returns_Code_When_No_Display_Name_And_Not_Default_Language()
    {
        var schedule = BaseSchedule();
        schedule.MusicLanguageName = null;
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], schedule)),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal("MY", sut.GetMusicLanguageDisplayText());
    }

    [Fact]
    public void GetIsSongPublicationVisible_Reflects_MusicEnabled()
    {
        var off = BaseSchedule();
        off.MusicEnabled = false;
        var sutOff = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], off)),
            new StubMediaService(),
            TestLogging.CreateLogger());
        Assert.False(sutOff.GetIsSongPublicationVisible());

        var sutOn = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], BaseSchedule())),
            new StubMediaService(),
            TestLogging.CreateLogger());
        Assert.True(sutOn.GetIsSongPublicationVisible());
    }

    [Fact]
    public void ClearSongPublicationCache_Drops_Cached_Name_Fallback_To_Code()
    {
        var schedule = BaseSchedule();
        schedule.MusicPublicationName = null;
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], schedule)),
            new StubMediaService(),
            TestLogging.CreateLogger());

        sut.UpdateSongPublicationCache("Was Cached", "iam-1");
        Assert.Equal("Was Cached", sut.GetSongPublicationDisplayText());

        sut.ClearSongPublicationCache();
        Assert.Equal("iam-1", sut.GetSongPublicationDisplayText());
    }

    [Fact]
    public void GetTrackDisplayText_Prefers_State_Track_Name()
    {
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], BaseSchedule())),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal("Track Five", sut.GetTrackDisplayText());
    }

    [Fact]
    public void GetFlowDirection_Returns_Rtl_When_Schedule_Direction_Rtl()
    {
        var schedule = BaseSchedule();
        schedule.MusicLanguageDirection = AppConstants.Media.TextDirectionRightToLeft;
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], schedule)),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal(FlowDirection.RightToLeft, sut.GetFlowDirection());
    }

    [Fact]
    public void GetMusicSectionDisplayText_Prefers_Name_Over_Code()
    {
        var schedule = BaseSchedule();
        schedule.MusicSectionName = "Section Alpha";
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], schedule)),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal("Section Alpha", sut.GetMusicSectionDisplayText());
    }

    [Fact]
    public async Task GetIsMusicLanguageSelectableAsync_Returns_True_When_More_Than_One_Language()
    {
        var media = new StubMediaService
        {
            BiblePublicationLanguagesResult = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
                ["M"] = new Language { LanguageCode = "M", Direction = AppConstants.Media.TextDirectionLeftToRight }
            }
        };

        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], BaseSchedule())),
            media,
            TestLogging.CreateLogger());

        Assert.True(await sut.GetIsMusicLanguageSelectableAsync());
    }

    [Fact]
    public async Task GetIsMusicTrackSelectableAsync_Returns_True_When_Multiple_Tracks()
    {
        var tracks = new SortedDictionary<string, BiblePublicationTrack>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "A" },
            ["2"] = new BiblePublicationTrack { TrackCode = "2", Title = "B" }
        };

        var media = new StubMediaService { BiblePublicationTracksResult = tracks };

        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], BaseSchedule())),
            media,
            TestLogging.CreateLogger());

        Assert.True(await sut.GetIsMusicTrackSelectableAsync());
    }

    [Fact]
    public async Task GetIsSongPublicationSelectableAsync_Uses_Db_When_Scope_Factory_Present()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(musicCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            foreach (var code in new[] { "pub-a", "pub-b" })
            {
                seed.PublicationLanguages.Add(new PublicationLanguage
                {
                    PublicationCode = code,
                    Category = musicCat,
                    CategoryId = musicCat.Id,
                    Language = lang,
                    LanguageId = lang.Id,
                    IsMusic = true
                });
            }

            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);
        var schedule = BaseSchedule();
        schedule.MusicLanguageCode = AppConstants.Media.DefaultLanguageCode;

        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], schedule)),
            new StubMediaService(),
            TestLogging.CreateLogger(),
            serviceScopeFactory: factory);

        Assert.True(await sut.GetIsSongPublicationSelectableAsync());
    }

    [Fact]
    public async Task GetIsMusicSectionSelectableAsync_Uses_Db_SectionLanguages_Count()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new MediaDbContext(options))
        {
            var musicCat = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            seed.Categories.Add(musicCat);
            seed.Languages.Add(lang);
            await seed.SaveChangesAsync();

            var pl = new PublicationLanguage
            {
                PublicationCode = "iam-1",
                Category = musicCat,
                CategoryId = musicCat.Id,
                Language = lang,
                LanguageId = lang.Id,
                IsMusic = true
            };
            seed.PublicationLanguages.Add(pl);
            await seed.SaveChangesAsync();

            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = "iam-1",
                SectionCode = "iam-1-a",
                Language = lang,
                PublicationLanguage = pl
            });
            seed.SectionLanguages.Add(new SectionLanguage
            {
                PublicationCode = "iam-1",
                SectionCode = "iam-1-b",
                Language = lang,
                PublicationLanguage = pl
            });
            await seed.SaveChangesAsync();
        }

        var factory = new MediaTestScopeFactory(options);
        var schedule = BaseSchedule();
        schedule.MusicPublicationCode = "iam-1";
        schedule.MusicLanguageCode = AppConstants.Media.DefaultLanguageCode;

        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], schedule)),
            new StubMediaService(),
            TestLogging.CreateLogger(),
            serviceScopeFactory: factory);

        Assert.True(sut.GetIsMusicSectionVisible());
        Assert.True(await sut.GetIsMusicSectionSelectableAsync());
    }

    [Fact]
    public async Task GetSongPublicationDisplayTextAsync_Returns_State_Name_When_Present()
    {
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], BaseSchedule())),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal("Kingdom Songs", await sut.GetSongPublicationDisplayTextAsync());
    }

    [Fact]
    public async Task GetTrackDisplayTextAsync_Returns_State_Name_When_Present()
    {
        var sut = new MusicDisplayTextProvider(
            new FakeState(new ApplicationState([], BaseSchedule())),
            new StubMediaService(),
            TestLogging.CreateLogger());

        Assert.Equal("Track Five", await sut.GetTrackDisplayTextAsync());
    }
}
