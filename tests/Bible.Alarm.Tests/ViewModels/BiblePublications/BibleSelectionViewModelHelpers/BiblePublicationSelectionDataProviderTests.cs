#nullable enable

using System.Collections.ObjectModel;
using System.Reflection;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionDataProviderTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class IdleDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action) =>
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
    }

    private sealed class IdleMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

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

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }

    private static BiblePublicationSelectionDataProvider CreateSut()
    {
        var app = new ApplicationState([], currentSchedule: null);
        return new BiblePublicationSelectionDataProvider(
            new IdleMediaService(),
            new IdleLanguageNameService(),
            new FakeApplicationState(app),
            new IdleDispatcher());
    }

    [Fact]
    public async Task PopulateLanguagesAsync_ReturnsImmediately_When_LanguagesNull()
    {
        var sut = CreateSut();
        await sut.PopulateLanguagesAsync(searchTerm: null, languages: null);
    }

    [Fact]
    public void ClearPublicationVMsMapping_RemovesCachedRows()
    {
        var sut = CreateSut();
        var pub = new BiblePublication { PublicationCode = "nwtsty", Name = "NWT", LanguageId = 1 };
        sut.publicationVMsMapping["nwtsty"] = new PublicationListViewItemModel(pub);

        sut.ClearPublicationVMsMapping();

        Assert.Empty(sut.publicationVMsMapping);
    }

    [Fact]
    public void GetPublicationVMsMapping_returns_same_instance_as_public_field()
    {
        var sut = CreateSut();
        Assert.Same(sut.publicationVMsMapping, sut.GetPublicationVMsMapping());
    }

    [Fact]
    public async Task PopulatePublicationsAsync_returns_immediately_when_publications_null()
    {
        var sut = CreateSut();
        await sut.PopulatePublicationsAsync("E", publications: null, languageChanged: false);
    }

    [Fact]
    public async Task PopulatePublicationsAsync_throws_when_category_missing_from_state_and_argument()
    {
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationCategoryName = null,
        };
        var app = new ApplicationState([], schedule);
        var sut = new BiblePublicationSelectionDataProvider(
            new IdleMediaService(),
            new IdleLanguageNameService(),
            new FakeApplicationState(app),
            new IdleDispatcher());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.PopulatePublicationsAsync("E", new ObservableCollection<PublicationListViewItemModel>(), languageChanged: false));
    }

    [Fact]
    public void RemoveIncompleteCatalogPlaceholders_removes_publications_that_fail_fully_cataloged_check()
    {
        var method = typeof(BiblePublicationSelectionDataProvider).GetMethod(
            "RemoveIncompleteCatalogPlaceholders",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var publications = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
        {
            ["nwtsty"] = new BiblePublication { PublicationCode = "nwtsty", Name = "Study Edition", Id = 10 },
            ["stub"] = new BiblePublication { PublicationCode = "stub", Name = "stub", Id = 3 },
        };

        method!.Invoke(null, [publications]);

        Assert.Single(publications);
        Assert.Equal("nwtsty", Assert.Single(publications.Keys));
    }

    [Fact]
    public void BuildSortedLanguageViewModels_filters_by_search_term_case_insensitively()
    {
        var method = typeof(BiblePublicationSelectionDataProvider).GetMethod(
            "BuildSortedLanguageViewModels",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var languagesData = new Dictionary<string, Language>
        {
            ["E"] = new Language { Id = 1, LanguageCode = "E" },
            ["S"] = new Language { Id = 2, LanguageCode = "S" },
        };
        var names = new Dictionary<int, string> { [1] = "English", [2] = "Español" };

        var result = Assert.IsType<List<LanguageListViewItemModel>>(
            method!.Invoke(null, [languagesData, names, "esp", null])!);

        var vm = Assert.Single(result);
        Assert.Equal("S", vm.Code);
    }

    [Fact]
    public void BuildSortedLanguageViewModels_marks_current_schedule_language_selected()
    {
        var method = typeof(BiblePublicationSelectionDataProvider).GetMethod(
            "BuildSortedLanguageViewModels",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var languagesData = new Dictionary<string, Language>
        {
            ["E"] = new Language { Id = 1, LanguageCode = "E" },
            ["S"] = new Language { Id = 2, LanguageCode = "S" },
        };
        var names = new Dictionary<int, string> { [1] = "English", [2] = "Spanish" };

        var result = Assert.IsType<List<LanguageListViewItemModel>>(
            method!.Invoke(null, [languagesData, names, null, "S"])!);

        var selected = Assert.Single(result, x => x.IsSelected);
        Assert.Equal("S", selected.Code);
    }

    [Fact]
    public async Task BuildPublicationPopulateResultAsync_prefers_first_sorted_publication_as_default()
    {
        var pubs = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
        {
            ["nwt"] = new BiblePublication { PublicationCode = "nwt", Name = "Holy Scriptures", Id = 5 },
        };
        var media = new ConfigurablePublicationMediaService(pubs);
        var app = new ApplicationState([], currentSchedule: null);
        var sut = new BiblePublicationSelectionDataProvider(
            media,
            new IdleLanguageNameService(),
            new FakeApplicationState(app),
            new IdleDispatcher());

        var method = typeof(BiblePublicationSelectionDataProvider).GetMethod(
            "BuildPublicationPopulateResultAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var taskObj = method!.Invoke(
            sut,
            ["E", AppConstants.Media.BiblePublicationCategoryBible, false, null]);
        Assert.NotNull(taskObj);
        var task = (Task)taskObj;
        await task;
        var resultProperty = taskObj.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var tuple = resultProperty!.GetValue(taskObj)!;
        var tupleType = tuple.GetType();
        var vms = Assert.IsType<List<PublicationListViewItemModel>>(tupleType.GetField("Item1")!.GetValue(tuple));
        var mapping = Assert.IsType<Dictionary<string, PublicationListViewItemModel>>(tupleType.GetField("Item2")!.GetValue(tuple));
        var preferred = tupleType.GetField("Item3")!.GetValue(tuple) as PublicationListViewItemModel;

        var vm = Assert.Single(vms);
        Assert.Equal("nwt", vm.Code);
        Assert.Single(mapping);
        Assert.NotNull(preferred);
        Assert.Equal("nwt", preferred!.Code);
    }

    private sealed class ConfigurablePublicationMediaService(Dictionary<string, BiblePublication> biblePublications) : IMediaService
    {
        private readonly IdleMediaService idle = new();

        public void Dispose() => idle.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            idle.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(biblePublications);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            idle.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            idle.GetBiblePublicationSections(languageCode, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            idle.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            idle.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            idle.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => idle.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) => idle.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            idle.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => idle.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            idle.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            idle.GetVocalMusicTracks(languageCode, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            idle.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            idle.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            idle.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            idle.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            idle.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            idle.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            idle.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            idle.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            idle.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }
}
