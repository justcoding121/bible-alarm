#nullable enable

using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DisplayMetadataServiceMusicHelperTests
{
    private sealed class StubMediaService : IMediaService
    {
        internal Func<Task<SortedDictionary<int, MusicTrack>>>? MelodyTracksAsync { get; init; }
        internal Func<Task<Dictionary<string, MelodyMusic>>>? MelodyReleasesAsync { get; init; }
        internal Func<Task<Dictionary<string, VocalMusic>>>? VocalReleasesAsync { get; init; }
        internal Func<Task<SortedDictionary<int, MusicTrack>>>? VocalTracksAsync { get; init; }

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
            MelodyReleasesAsync?.Invoke() ?? Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            MelodyTracksAsync?.Invoke() ?? Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            VocalReleasesAsync?.Invoke() ?? Task.FromResult(new Dictionary<string, VocalMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            VocalTracksAsync?.Invoke() ?? Task.FromResult(new SortedDictionary<int, MusicTrack>());

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

    [Fact]
    public async Task SetMusicMetadataAsync_MelodyBranch_SetsTitleAndArtist_FromCatalog()
    {
        var iam = AppConstants.Media.MelodyMusicPublicationCodeIam;
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "105", Title = "Melody Song Title", Url = "", LookUpPath = "" },
        };
        var releases = new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
        {
            [iam] = new BiblePublication { Id = 1, PublicationCode = iam, Name = "Kingdom Melodies" },
        };

        var media = new StubMediaService
        {
            MelodyTracksAsync = () => Task.FromResult(tracks),
            MelodyReleasesAsync = () => Task.FromResult(releases),
        };

        var sut = new DisplayMetadataServiceMusicHelper(TestLogging.CreateLogger(), media, vocalMusicService: null);
        var meta = new MetaData();
        var trackMeta = new TrackMetadata
        {
            LanguageCode = string.Empty,
            PublicationCode = iam,
            TrackCode = "105",
        };

        var callbackRan = false;
        await sut.SetMusicMetadataAsync(trackMeta, meta, "file:///music.mp3", async () =>
        {
            await Task.CompletedTask;
            callbackRan = true;
        });

        Assert.True(callbackRan);
        Assert.Equal("Melody Song Title", meta.Title);
        Assert.Equal($"Kingdom Melodies{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}", meta.Artist);
    }

    [Fact]
    public async Task SetMusicMetadataAsync_VocalBranch_SetsAlbumAndTitle_FromCatalog()
    {
        var osg = AppConstants.Media.MusicPublicationCodeOsg;
        var vocalTracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "1", Title = "Vocal Track One", Url = "", LookUpPath = "" },
        };
        var vocalReleases = new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase)
        {
            [osg] = new BiblePublication { Id = 2, PublicationCode = osg, Name = "Vocal Album" },
        };

        var media = new StubMediaService
        {
            VocalReleasesAsync = () => Task.FromResult(vocalReleases),
            VocalTracksAsync = () => Task.FromResult(vocalTracks),
        };

        var sut = new DisplayMetadataServiceMusicHelper(TestLogging.CreateLogger(), media, vocalMusicService: null);
        var meta = new MetaData();
        var trackMeta = new TrackMetadata
        {
            LanguageCode = "E",
            PublicationCode = osg,
            TrackCode = "1",
        };

        await sut.SetMusicMetadataAsync(trackMeta, meta, "file:///vocal.mp3", () => Task.CompletedTask);

        Assert.Equal("Vocal Album", meta.Album);
        Assert.Equal("Vocal Track One", meta.Title);
    }
}
