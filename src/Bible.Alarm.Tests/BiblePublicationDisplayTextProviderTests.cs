#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationDisplayTextProviderTests
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
        public Func<string?, Dictionary<string, Language>> GetLanguages { get; set; } = _ => [];

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(GetLanguages(categoryName));

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
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

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public string? ResolvedName { get; set; }

        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => ResolvedName;
    }

    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Tests must not touch EF-backed publication/section selectability.");
    }

    private static ScheduleStateItem BaseSchedule() =>
        new()
        {
            Id = 1,
            Name = "S",
            BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight,
        };

    private static BiblePublicationDisplayTextProvider Create(
        ScheduleStateItem? currentSchedule,
        StubMediaService? media = null,
        StubCategoryNameService? categories = null) =>
        new(
            new FakeState(new ApplicationState([], currentSchedule)),
            TestLogging.CreateLogger(),
            media ?? new StubMediaService(),
            categories ?? new StubCategoryNameService(),
            new UnusedScopeFactory());

    [Fact]
    public void GetIsSectionVisible_DefaultsTrue_When_No_CurrentSchedule()
    {
        var sut = Create(null);
        Assert.True(sut.GetIsSectionVisible());
    }

    [Fact]
    public void GetIsSectionVisible_DefaultsTrue_When_BiblePublicationCode_Empty()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCode = "   ";
        var sut = Create(schedule);
        Assert.True(sut.GetIsSectionVisible());
    }

    [Fact]
    public void GetIsSectionVisible_False_For_Flat_Drama_Publication()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCode = AppConstants.Media.BiblePublicationCategoryDramas;
        var sut = Create(schedule);
        Assert.False(sut.GetIsSectionVisible());
    }

    [Fact]
    public void GetFlowDirection_RightToLeft_When_LanguageDirection_Rtl()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionRightToLeft;
        var sut = Create(schedule);
        Assert.Equal(FlowDirection.RightToLeft, sut.GetFlowDirection());
    }

    [Fact]
    public void GetLanguageDisplayText_Returns_Default_Language_When_Code_Absent()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationLanguageCode = null;
        schedule.BiblePublicationLanguageName = null;
        var sut = Create(schedule);
        Assert.Equal(AppConstants.Media.DefaultLanguageCode, sut.GetLanguageDisplayText());
    }

    [Fact]
    public void GetPublicationDisplayText_Prefers_Display_Name()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationName = "Holy Scriptures";
        schedule.BiblePublicationCode = "nwt";
        var sut = Create(schedule);
        Assert.Equal("Holy Scriptures", sut.GetPublicationDisplayText());
    }

    [Fact]
    public void GetTrackDisplayText_ChapterPrefix_When_Bible_With_Sections()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible;
        schedule.BiblePublicationCode = "nwt";
        schedule.BiblePublicationTrackTitle = null;
        schedule.BiblePublicationTrackCode = "8";
        var sut = Create(schedule);
        Assert.Equal("Chapter 8", sut.GetTrackDisplayText());
    }

    [Fact]
    public void GetTrackDisplayText_MusicPublication_Appends_Code_To_Publication_Name()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationIsMusic = true;
        schedule.BiblePublicationCode = "voc2025";
        schedule.BiblePublicationName = "Vocal Album";
        schedule.BiblePublicationTrackTitle = null;
        schedule.BiblePublicationTrackCode = "12";
        var sut = Create(schedule);
        Assert.Equal("Vocal Album 12", sut.GetTrackDisplayText());
    }

    [Fact]
    public void GetCategoryDisplayText_Uses_Service_Display_Name_When_Present()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryName = "bkcat";
        var categories = new StubCategoryNameService { ResolvedName = "Books" };
        var sut = Create(schedule, categories: categories);
        Assert.Equal("Books", sut.GetCategoryDisplayText());
    }

    [Fact]
    public void GetCategoryDisplayText_Falls_Back_To_Category_Code_When_Service_Returns_Empty()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryName = "bkcat";
        var categories = new StubCategoryNameService { ResolvedName = " " };
        var sut = Create(schedule, categories: categories);
        Assert.Equal("bkcat", sut.GetCategoryDisplayText());
    }

    [Fact]
    public async Task GetIsLanguageSelectableAsync_ReturnsFalse_When_No_Schedule()
    {
        var sut = Create(null);
        Assert.False(await sut.GetIsLanguageSelectableAsync());
    }

    [Fact]
    public async Task GetIsLanguageSelectableAsync_ReturnsFalse_When_Category_Missing()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryName = null;
        var sut = Create(schedule);
        Assert.False(await sut.GetIsLanguageSelectableAsync());
    }

    [Theory]
    [InlineData(AppConstants.Media.BiblePublicationCategoryBible)]
    [InlineData(AppConstants.Media.BiblePublicationCategoryDramas)]
    [InlineData(AppConstants.Media.BiblePublicationCategoryMusic)]
    public async Task GetIsLanguageSelectableAsync_ReturnsTrue_For_Core_Browsing_Categories(string category)
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryName = category;
        var sut = Create(schedule);
        Assert.True(await sut.GetIsLanguageSelectableAsync());
    }

    [Fact]
    public async Task GetIsLanguageSelectableAsync_Queries_Service_When_Category_Not_ShortCircuited()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryName = "OtherCat";
        var media = new StubMediaService
        {
            GetLanguages = _ => new Dictionary<string, Language>
            {
                ["a"] = new Language(),
                ["b"] = new Language(),
            },
        };
        var sut = Create(schedule, media);
        Assert.True(await sut.GetIsLanguageSelectableAsync());
    }

    [Fact]
    public async Task GetIsLanguageSelectableAsync_ReturnsFalse_On_Service_Error()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCategoryName = "OtherCat";
        var media = new StubMediaService
        {
            GetLanguages = _ => throw new InvalidOperationException("boom"),
        };
        var sut = Create(schedule, media);
        Assert.False(await sut.GetIsLanguageSelectableAsync());
    }

    [Fact]
    public async Task GetIsTrackSelectableAsync_Uses_Expected_Count_From_State()
    {
        var schedule = BaseSchedule();
        schedule.BiblePublicationCode = "nwt";
        schedule.BiblePublicationTrackModalItemCount = 4;
        var sut = Create(schedule);
        Assert.True(await sut.GetIsTrackSelectableAsync());

        schedule.BiblePublicationTrackModalItemCount = 1;
        Assert.False(await sut.GetIsTrackSelectableAsync());
    }
}
