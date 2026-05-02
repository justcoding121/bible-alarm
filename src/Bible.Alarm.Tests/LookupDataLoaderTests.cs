#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class LookupDataLoaderTests
{
    private static LookupDataCollector.LookupKeys EmptyKeys() =>
        new(
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance),
            new HashSet<(string LanguageCode, string PublicationCode, string SectionCode)>(PublicationLookupKeyComparers.LanguagePublicationSection.Instance),
            new HashSet<(string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode)>(
                PublicationLookupKeyComparers.LanguagePublicationNullableSectionTrack.Instance),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance),
            new HashSet<(string LanguageCode, string PublicationCode)>(PublicationLookupKeyComparers.LanguagePublication.Instance),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<(string PublicationCode, string SectionCode)>(PublicationLookupKeyComparers.PublicationSection.Instance));

    [Fact]
    public async Task LoadAllAsync_WithEmptyKeys_ReturnsEmptyLookupTables()
    {
        var sut = new LookupDataLoader(
            BiblePublicationService: null,
            biblePublicationSectionService: null,
            mediaService: null,
            vocalMusicService: null,
            scopeFactory: null);

        var data = await sut.LoadAllAsync(EmptyKeys());

        Assert.Empty(data.Publications);
        Assert.Empty(data.Sections);
        Assert.Empty(data.NoLanguagePublications);
        Assert.Empty(data.NoLanguageSections);
        Assert.Empty(data.NoLanguageTrackTitles);
        Assert.Empty(data.VocalLanguages);
        Assert.Empty(data.VocalReleases);
        Assert.Empty(data.VocalTracks);
        Assert.Empty(data.MelodyTracksFlat);
        Assert.Empty(data.MelodyTracksBySection);
        Assert.Empty(data.MelodyReleases);
    }

    [Fact]
    public async Task LoadAllAsync_materializes_publications_sections_and_music_lookup_slices()
    {
        var nwt = AppConstants.Media.BiblePublicationCodeNwt;
        var vod = AppConstants.Media.MediatorPublicationCodeVODBibleTeachings;
        var osg = AppConstants.Media.MusicPublicationCodeOsg;
        var iam = AppConstants.Media.MelodyMusicPublicationCodeIam;

        var nwtPublication = new BiblePublication
        {
            Id = 101,
            PublicationCode = nwt,
            Name = "New World Translation",
            Sections = [],
            Tracks = [],
        };

        var vodPublication = new BiblePublication
        {
            Id = 202,
            PublicationCode = vod,
            Name = "Bible Teachings",
            Sections = [],
            Tracks = [],
        };

        var normalizedGen = SectionCodeHelper.Normalize("gen")!;
        var bibleService = new RoutingBiblePublicationService(nwtPublication, vodPublication);
        var sectionService = new StubBiblePublicationSectionService(("E", nwt, normalizedGen, "Genesis"));

        var vocalRelease = new BiblePublication
        {
            Id = 303,
            PublicationCode = osg,
            Name = "Sing Out Joyfully",
            LanguageId = 1,
            Sections = [],
            Tracks = [],
        };

        var vocalMusicService = new StubVocalMusicService(("E", new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase)
        {
            [osg] = vocalRelease,
        }));

        var melodyTracksFlat = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "10", Title = "Flat melody", Url = "", LookUpPath = "" },
        };

        var melodyTracksSection = new SortedDictionary<int, MusicTrack>
        {
            [2] = new MusicTrack { TrackCode = "20", Title = "Section melody", Url = "", LookUpPath = "" },
        };

        var iamMelodyRelease = new BiblePublication
        {
            Id = 404,
            PublicationCode = iam,
            Name = "Kingdom Melodies",
            LanguageId = null,
            Sections = [],
            Tracks = [],
        };

        var mediaService = new LookupBatchMediaServiceStub(
            vocalLanguages: new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language { Id = 1, LanguageCode = "E" },
            },
            vocalTracks: new SortedDictionary<int, MusicTrack>
            {
                [0] = new MusicTrack { TrackCode = "1", Title = "Vocal track", Url = "", LookUpPath = "" },
            },
            melodyTracksFlat,
            melodyTracksBySection: melodyTracksSection,
            melodyReleases: new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [iam] = iamMelodyRelease,
            });

        var keys = EmptyKeys();
        keys.PublicationKeys.Add(("E", nwt));
        keys.PublicationKeys.Add(("E", vod));
        keys.SectionKeys.Add(("E", nwt, normalizedGen));
        keys.VocalMusicLanguageCodes.Add("E");
        keys.VocalMusicKeys.Add(("E", osg));
        keys.VocalTrackKeys.Add(("E", osg));
        keys.MelodyPublicationCodes.Add(osg);
        keys.MelodySectionKeys.Add((iam, "iam-1"));

        var sut = new LookupDataLoader(
            bibleService,
            sectionService,
            mediaService,
            vocalMusicService,
            scopeFactory: null);

        var data = await sut.LoadAllAsync(keys);

        Assert.Equal(2, data.Publications.Count);
        Assert.Same(nwtPublication, data.Publications[("E", nwt)]);
        Assert.Same(vodPublication, data.Publications[("E", vod)]);

        Assert.Equal("Genesis", data.Sections[("E", nwt, normalizedGen)]);

        Assert.Equal("E", data.VocalLanguages["E"].LanguageCode);

        Assert.Same(vocalRelease, data.VocalReleases[("E", osg)].Publication);

        Assert.Single(data.VocalTracks[("E", osg)]);
        Assert.Equal("Vocal track", data.VocalTracks[("E", osg)][0].Title);

        Assert.Same(melodyTracksFlat, data.MelodyTracksFlat[osg]);
        Assert.Same(melodyTracksSection, data.MelodyTracksBySection[(iam, "iam-1")]);
        Assert.Equal(iamMelodyRelease.Name, data.MelodyReleases[iam].Name);
    }

    private sealed class RoutingBiblePublicationService : IBiblePublicationService
    {
        private readonly BiblePublication sectionedPublication;
        private readonly BiblePublication tracksPublication;

        public RoutingBiblePublicationService(BiblePublication sectionedPublication, BiblePublication tracksPublication)
        {
            this.sectionedPublication = sectionedPublication;
            this.tracksPublication = tracksPublication;
        }

        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PublicationTypeHelper.HasSectionStructure(publicationCode) ? sectionedPublication : null);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!PublicationTypeHelper.HasSectionStructure(publicationCode) ? tracksPublication : null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private sealed class StubBiblePublicationSectionService : IBiblePublicationSectionService
    {
        private readonly Dictionary<(string Lang, string Pub, string Section), string> names;

        public StubBiblePublicationSectionService(params (string Lang, string Pub, string Section, string Name)[] rows)
        {
            names = rows.ToDictionary(r => (r.Lang, r.Pub, r.Section), r => r.Name);
        }

        public void Dispose()
        {
        }

        public Task<string?> GetSectionNameAsync(string languageCode, string publicationCode, string sectionCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(names.TryGetValue((languageCode, publicationCode, sectionCode), out var n) ? n : null);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationWithoutLanguageAsync(
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetSectionAsync(string languageCode, string publicationCode, string sectionCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublicationSection?>(null);
    }

    private sealed class StubVocalMusicService : IVocalMusicService
    {
        private readonly Dictionary<string, Dictionary<string, VocalMusic>> byLanguage;

        public StubVocalMusicService(params (string LanguageCode, Dictionary<string, VocalMusic> Releases)[] languages)
        {
            byLanguage = languages.ToDictionary(x => x.LanguageCode, x => x.Releases, StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
        }

        public Task<VocalMusic?> GetByLanguageAndCodeAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<VocalMusic?>(null);

        public Task<Dictionary<string, VocalMusic>> GetByLanguageCodeAsync(string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(byLanguage.TryGetValue(languageCode, out var rel)
                ? rel
                : new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByLanguageAndCodeAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string languageCode, string publicationCode, string trackCode, string url,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class LookupBatchMediaServiceStub : IMediaService
    {
        private readonly Dictionary<string, Language> vocalLanguages;
        private readonly SortedDictionary<int, MusicTrack> vocalTracks;
        private readonly SortedDictionary<int, MusicTrack> melodyTracksFlat;
        private readonly SortedDictionary<int, MusicTrack> melodyTracksBySection;
        private readonly Dictionary<string, MelodyMusic> melodyReleases;

        public LookupBatchMediaServiceStub(
            Dictionary<string, Language> vocalLanguages,
            SortedDictionary<int, MusicTrack> vocalTracks,
            SortedDictionary<int, MusicTrack> melodyTracksFlat,
            SortedDictionary<int, MusicTrack> melodyTracksBySection,
            Dictionary<string, MelodyMusic> melodyReleases)
        {
            this.vocalLanguages = vocalLanguages;
            this.vocalTracks = vocalTracks;
            this.melodyTracksFlat = melodyTracksFlat;
            this.melodyTracksBySection = melodyTracksBySection;
            this.melodyReleases = melodyReleases;
        }

        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

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
            Task.FromResult(melodyReleases);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(melodyTracksFlat);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(melodyTracksBySection);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(vocalLanguages);

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(vocalTracks);

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
}
