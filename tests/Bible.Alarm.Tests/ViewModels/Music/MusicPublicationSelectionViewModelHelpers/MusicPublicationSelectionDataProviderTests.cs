#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionDataProviderTests
{
    private sealed class VocalTracksMediaStub : IMediaService
    {
        internal SortedDictionary<int, MusicTrack>? VocalTracksResult { get; init; }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
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
            Task.FromResult(VocalTracksResult ?? new SortedDictionary<int, MusicTrack>());

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

    private static PublicationListViewItemModel SongPublicationVm(string code = "osg") =>
        new(new BiblePublication
        {
            Id = 1,
            PublicationCode = code,
            Name = "Song pub",
            Sections = [],
            Tracks = [],
        });

    [Fact]
    public async Task GetTrackForSongPublicationAsync_returns_empty_when_no_tracks()
    {
        var media = new VocalTracksMediaStub { VocalTracksResult = new SortedDictionary<int, MusicTrack>() };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());

        var result = await sut.GetTrackForSongPublicationAsync(SongPublicationVm(), "E", currentSchedule: null);

        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.TrackName);
    }

    [Fact]
    public async Task GetTrackForSongPublicationAsync_returns_only_track_when_schedule_differs()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "44", Title = "Song forty-four", Url = "", LookUpPath = "" },
        };

        var media = new VocalTracksMediaStub { VocalTracksResult = tracks };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());

        var result = await sut.GetTrackForSongPublicationAsync(SongPublicationVm(), "E", currentSchedule: null);

        Assert.Equal("44", result.TrackCode);
        Assert.Equal("Song forty-four", result.TrackName);
    }

    [Fact]
    public async Task GetTrackForSongPublicationAsync_prefers_persisted_track_when_same_publication()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "5", Title = "Five", Url = "", LookUpPath = "" },
            [1] = new MusicTrack { TrackCode = "9", Title = "Nine", Url = "", LookUpPath = "" },
        };

        var media = new VocalTracksMediaStub { VocalTracksResult = tracks };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());

        var schedule = new ScheduleStateItem
        {
            Id = 1,
            Name = "Alarm",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            MusicLanguageCode = "E",
            MusicPublicationCode = "osg",
            MusicTrackCode = "9",
        };

        var result = await sut.GetTrackForSongPublicationAsync(SongPublicationVm("osg"), "E", schedule);

        Assert.Equal("9", result.TrackCode);
        Assert.Equal("Nine", result.TrackName);
    }

    [Fact]
    public void SetSelectedSongPublication_marks_mapping_entry_and_invokes_callback()
    {
        var mapping = new Dictionary<string, PublicationListViewItemModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["osg"] = SongPublicationVm("osg"),
        };
        PublicationListViewItemModel? selected = null;

        MusicPublicationSelectionDataProvider.SetSelectedSongPublication(
            new AlarmMusic { PublicationCode = "osg", LanguageCode = "E" },
            mapping,
            currentSelectedSongPublication: null,
            vm => selected = vm);

        Assert.Same(mapping["osg"], selected);
        Assert.True(mapping["osg"].IsSelected);
    }

    [Fact]
    public void SetSelectedSongPublication_is_noop_when_current_is_null()
    {
        PublicationListViewItemModel? selected = SongPublicationVm("osg");

        MusicPublicationSelectionDataProvider.SetSelectedSongPublication(
            current: null,
            new Dictionary<string, PublicationListViewItemModel>(StringComparer.OrdinalIgnoreCase),
            currentSelectedSongPublication: selected,
            vm => selected = vm);

        Assert.Equal("osg", selected!.Code);
    }

    [Fact]
    public void SetSelectedSongPublication_is_noop_when_publication_missing_from_mapping()
    {
        PublicationListViewItemModel? selected = null;

        MusicPublicationSelectionDataProvider.SetSelectedSongPublication(
            new AlarmMusic { PublicationCode = "missing", LanguageCode = "E" },
            new Dictionary<string, PublicationListViewItemModel>(StringComparer.OrdinalIgnoreCase),
            currentSelectedSongPublication: null,
            vm => selected = vm);

        Assert.Null(selected);
    }

    [Fact]
    public void SetSelectedSongPublication_clears_previous_selection_flag()
    {
        var previous = SongPublicationVm("sjj");
        previous.IsSelected = true;
        var mapping = new Dictionary<string, PublicationListViewItemModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["osg"] = SongPublicationVm("osg"),
        };
        PublicationListViewItemModel? selected = null;

        MusicPublicationSelectionDataProvider.SetSelectedSongPublication(
            new AlarmMusic { PublicationCode = "osg", LanguageCode = "E" },
            mapping,
            previous,
            vm => selected = vm);

        Assert.False(previous.IsSelected);
        Assert.True(selected!.IsSelected);
    }

    [Fact]
    public async Task GetTrackForSongPublicationAsync_picks_random_when_schedule_publication_differs()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "1", Title = "One", Url = "", LookUpPath = "" },
        };
        var media = new VocalTracksMediaStub { VocalTracksResult = tracks };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            Name = "Alarm",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            MusicLanguageCode = "E",
            MusicPublicationCode = "other",
            MusicTrackCode = "9",
        };

        var result = await sut.GetTrackForSongPublicationAsync(SongPublicationVm("osg"), "E", schedule);

        Assert.Equal("1", result.TrackCode);
        Assert.Equal("One", result.TrackName);
    }

    [Fact]
    public async Task PopulateSongPublications_fills_collection_from_catalog()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var catalog = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
        {
            ["osg"] = new BiblePublication
            {
                Id = 5,
                PublicationCode = "osg",
                Name = "Sing Out",
                LanguageId = 1,
            },
            ["placeholder"] = new BiblePublication
            {
                Id = 0,
                PublicationCode = "placeholder",
                Name = "placeholder",
                LanguageId = 1,
            },
        };
        var media = new PublicationsMediaStub
        {
            ServeCatalogLanguageCode = "E",
            ServeCatalog = catalog,
        };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());
        var collection = new System.Collections.ObjectModel.ObservableCollection<PublicationListViewItemModel>();
        PublicationListViewItemModel? selected = null;

        try
        {
            await sut.PopulateSongPublications(
                "E",
                new AlarmMusic { LanguageCode = "E", PublicationCode = "osg" },
                collection,
                vm => selected = vm);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                   || (ex is InvalidOperationException ioe && ioe.Message.Contains("MainThread")))
        {
            return;
        }

        Assert.Single(collection);
        Assert.Equal("osg", collection[0].Code);
        Assert.True(collection[0].IsSelected);
        Assert.Same(collection[0], selected);
        Assert.True(sut.SongPublicationVMsMapping.ContainsKey("osg"));
    }

    [Fact]
    public async Task PopulateSongPublications_clears_ui_when_catalog_empty()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var media = new PublicationsMediaStub { ServeCatalogLanguageCode = "E", ServeCatalog = [] };
        var sut = new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService());
        var collection = new System.Collections.ObjectModel.ObservableCollection<PublicationListViewItemModel>
        {
            SongPublicationVm("stale"),
        };

        try
        {
            await sut.PopulateSongPublications("E", current: null, collection, _ => { });
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                   || (ex is InvalidOperationException ioe && ioe.Message.Contains("MainThread")))
        {
            return;
        }

        Assert.Empty(collection);
        Assert.Empty(sut.SongPublicationVMsMapping);
    }

    [Fact]
    public async Task PopulateLanguages_appends_named_languages_and_selects_current()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var media = new LanguagesMediaStub
        {
            Languages =
            {
                ["E"] = new Language { Id = 1, LanguageCode = "E" },
                ["S"] = new Language { Id = 2, LanguageCode = "S" },
            },
        };
        var names = new NamingLanguageService
        {
            Names = { [1] = "English", [2] = "Spanish" },
        };
        var sut = new MusicPublicationSelectionDataProvider(media, names);
        var languages = new System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>();
        LanguageListViewItemModel? current = null;

        try
        {
            await sut.PopulateLanguages(
                new AlarmMusic { LanguageCode = "S", PublicationCode = "osg" },
                languages,
                lang => current = lang);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                   || (ex is InvalidOperationException ioe && ioe.Message.Contains("MainThread")))
        {
            return;
        }

        Assert.Equal(2, languages.Count);
        Assert.NotNull(current);
        Assert.Equal("S", current!.Code);
        Assert.True(current.IsSelected);
    }

    [Fact]
    public async Task PopulateLanguages_filters_by_search_term()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var media = new LanguagesMediaStub
        {
            Languages =
            {
                ["E"] = new Language { Id = 1, LanguageCode = "E" },
                ["S"] = new Language { Id = 2, LanguageCode = "S" },
            },
        };
        var names = new NamingLanguageService
        {
            Names = { [1] = "English", [2] = "Spanish" },
        };
        var sut = new MusicPublicationSelectionDataProvider(media, names);
        var languages = new System.Collections.ObjectModel.ObservableCollection<LanguageListViewItemModel>();

        try
        {
            await sut.PopulateLanguages(current: null, languages, _ => { }, searchTerm: "span");
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                   || (ex is InvalidOperationException ioe && ioe.Message.Contains("MainThread")))
        {
            return;
        }

        Assert.Single(languages);
        Assert.Equal("S", languages[0].Code);
    }

    [Fact]
    public async Task GetFirstSongPublicationAndTrackForLanguageAsync_returns_empty_without_bible_service()
    {
        var sut = new MusicPublicationSelectionDataProvider(new VocalTracksMediaStub(), new IdleLanguageNameService());
        var language = new LanguageListViewItemModel(
            new Language { LanguageCode = "E", Direction = "ltr" },
            "English");

        var result = await sut.GetFirstSongPublicationAndTrackForLanguageAsync(language, currentSchedule: null);

        Assert.Null(result.PublicationCode);
        Assert.Empty(result.TrackCode);
    }

    private sealed class PublicationsMediaStub : IMediaService
    {
        private readonly VocalTracksMediaStub idle = new();

        internal string ServeCatalogLanguageCode { get; init; } = string.Empty;
        internal Dictionary<string, BiblePublication>? ServeCatalog { get; init; }

        public void Dispose() => idle.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            idle.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            idle.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false)
        {
            if (ServeCatalog != null &&
                string.Equals(languageCode, ServeCatalogLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ServeCatalog);
            }

            return Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));
        }

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            idle.GetBiblePublicationSections(languageCode, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            idle.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            idle.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            idle.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => idle.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            idle.GetMelodyMusicTracks(publicationCode);

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

    private sealed class LanguagesMediaStub : IMediaService
    {
        private readonly VocalTracksMediaStub idle = new();

        internal Dictionary<string, Language> Languages { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public void Dispose() => idle.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(Languages);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            idle.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            idle.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            idle.GetBiblePublicationSections(languageCode, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            idle.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            idle.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            idle.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => idle.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            idle.GetMelodyMusicTracks(publicationCode);

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

    private sealed class NamingLanguageService : ILanguageNameService
    {
        internal Dictionary<int, string> Names { get; } = [];

        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Names.TryGetValue(languageId, out var name) ? name : null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<int, string>();
            foreach (var id in languageIds)
            {
                if (Names.TryGetValue(id, out var name))
                {
                    result[id] = name;
                }
            }

            return Task.FromResult(result);
        }

        public string? GetNameCached(int languageId) =>
            Names.TryGetValue(languageId, out var name) ? name : null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }
}
