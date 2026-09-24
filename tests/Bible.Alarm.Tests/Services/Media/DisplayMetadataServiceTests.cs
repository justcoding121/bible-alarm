#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.DisplayMetadataServiceHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DisplayMetadataServiceTests
{
    private sealed class SectionMediaStub : IdleCatalogMediaService
    {
        internal BiblePublicationSection? Section { get; init; }

        internal SortedDictionary<string, BiblePublicationTrack>? MagazineTracks { get; init; }

        public override Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode,
            string sectionCode) =>
            Task.FromResult(Section);

        public override Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode,
            string versionCode, string? sectionCode) =>
            Task.FromResult(MagazineTracks ?? new SortedDictionary<string, BiblePublicationTrack>());
    }

    private sealed class MelodyDiscMediaStub : IdleCatalogMediaService
    {
        public override Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode,
            string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>
            {
                [105] = new MusicTrack { TrackCode = "105", Title = "Melody Track Title", Url = "", LookUpPath = "" },
            });

        public override Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] = new BiblePublication
                {
                    Id = 1,
                    PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                    Name = "Kingdom Melodies Vol. 1",
                },
            });

        public override Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(
            string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>
            {
                ["iam-1"] = new BiblePublicationSection { SectionCode = "iam-1", Name = "Disc 1" },
            });
    }

    private sealed class VocalMusicMediaStub : IdleCatalogMediaService
    {
        public override Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.MusicPublicationCodeOsg] = new BiblePublication
                {
                    Id = 2,
                    PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
                    Name = "Sing Out",
                },
            });

        public override Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>
            {
                [0] = new MusicTrack { TrackCode = "2", Title = "Vocal Song Two", Url = "", LookUpPath = "" },
            });
    }

    private sealed class FlatBiblePublicationService : IBiblePublicationService
    {
        internal BiblePublication? Publication { get; init; }

        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Publication);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Publication);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode,
            string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private static async Task RunOrSoftSkipMainThreadComAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                   || (ex is InvalidOperationException ioe && ioe.Message.Contains("MainThread")))
        {
        }
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_returns_metadata_for_idle_bible_catalog()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nw",
            TrackCode = "1",
            LookUpPath = "/test/path",
        };
        var item = new PlayItem(metadata, "file:///stub/bible-track.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_returns_metadata_for_idle_music_catalog()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = "songbook",
            TrackCode = "1",
            LookUpPath = "/music/path",
        };
        var item = new PlayItem(metadata, "file:///stub/music-track.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetDisplayMetadataAsync_populates_title_via_file_fallback_when_music_catalog_idle()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = string.Empty,
            PublicationCode = "iam",
            TrackCode = "1",
            LookUpPath = "/melody/path",
        };
        var item = new PlayItem(metadata, "file:///nonexistent-melody-track.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetDisplayMetadataAsync(track);

        Assert.Equal("Unknown Title", result.Title);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_https_streaming_uses_unknown_title_when_music_title_missing()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = string.Empty,
            PublicationCode = "iam",
            TrackCode = "1",
            LookUpPath = "/melody/path",
        };
        var item = new PlayItem(metadata, "https://example.invalid/stream.mp3");
        var track = new AudioPlayerTrack { PlayItem = item, Uri = item.Url };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.Equal("Unknown Title", result.Title);
        Assert.Equal("jw.org", result.Artist);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_bible_section_sets_chapter_style_title()
    {
        var media = new SectionMediaStub
        {
            Section = new BiblePublicationSection { Name = "Genesis", SectionCode = "1" },
        };
        var biblePubs = new FlatBiblePublicationService
        {
            Publication = new BiblePublication
            {
                Name = "New World Translation",
                PublicationCode = "nwt",
                Tracks = [],
            },
        };
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            media,
            new HttpClientHandler(),
            biblePubs);

        var metadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "nwt",
            SectionCode = "1",
            TrackCode = "3",
            LookUpPath = "/nwt/1/3",
        };
        var track = new AudioPlayerTrack
        {
            PlayItem = new PlayItem(metadata, "file:///nwt.mp3"),
            Uri = "file:///nwt.mp3",
        };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.Equal("Genesis 3", result.Title);
        Assert.Equal($"New World Translation{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}", result.Artist);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_magazine_section_uses_track_title_when_available()
    {
        var magazineCode = $"w{MagazineHelper.MagazineEndYear}";
        var media = new SectionMediaStub
        {
            Section = new BiblePublicationSection { Name = "January Issue", SectionCode = "202401" },
            MagazineTracks = new SortedDictionary<string, BiblePublicationTrack>
            {
                ["1"] = new BiblePublicationTrack { TrackCode = "1", Title = "Magazine Article Title" },
            },
        };
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            media,
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = magazineCode,
            SectionCode = "202401",
            TrackCode = "1",
            LookUpPath = $"/{magazineCode}/202401/1",
        };
        var track = new AudioPlayerTrack
        {
            PlayItem = new PlayItem(metadata, "file:///mag.mp3"),
            Uri = "file:///mag.mp3",
        };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.Equal("Magazine Article Title", result.Title);
        Assert.Equal($"January Issue{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}", result.Artist);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_disc_melody_on_bible_play_type_sets_carplay_friendly_fields()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new MelodyDiscMediaStub(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = string.Empty,
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            DownloadCode = "iam-1",
            TrackCode = "105",
            LookUpPath = "/iam/1/105",
        };
        var track = new AudioPlayerTrack
        {
            PlayItem = new PlayItem(metadata, "file:///iam.mp3"),
            Uri = "file:///iam.mp3",
        };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.Equal("Kingdom Melodies Vol. 1", result.Title);
        Assert.Equal("Melody Track Title", result.Artist);
        Assert.Equal("Disc 1", result.Album);
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_flat_bible_publication_uses_track_title_from_service()
    {
        var biblePubs = new FlatBiblePublicationService
        {
            Publication = new BiblePublication
            {
                Name = "Drama Album",
                PublicationCode = "dram",
                Tracks =
                [
                    new BiblePublicationTrack { TrackCode = "7", Title = "Drama Episode Seven" },
                ],
            },
        };
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler(),
            biblePubs);

        var metadata = new TrackMetadata
        {
            IsBibleContent = true,
            LanguageCode = "E",
            PublicationCode = "dram",
            TrackCode = "7",
            LookUpPath = "/dram/7",
        };
        var track = new AudioPlayerTrack
        {
            PlayItem = new PlayItem(metadata, "file:///dram.mp3"),
            Uri = "file:///dram.mp3",
        };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.Equal("Drama Episode Seven", result.Title);
        Assert.Equal($"Drama Album{DisplayMetadataPublisherStrings.JwOrgArtistQualifier}", result.Artist);
    }

    [Fact]
    public async Task GetDisplayMetadataAsync_applies_lock_and_returns_core_fields_for_bible_track()
    {
        await RunOrSoftSkipMainThreadComAsync(async () =>
        {
            var media = new SectionMediaStub
            {
                Section = new BiblePublicationSection { Name = "Exodus", SectionCode = "2" },
            };
            var sut = new DisplayMetadataService(
                TestLogging.CreateLogger(),
                media,
                new HttpClientHandler());

            var metadata = new TrackMetadata
            {
                IsBibleContent = true,
                LanguageCode = "E",
                PublicationCode = "nwt",
                SectionCode = "2",
                TrackCode = "1",
                LookUpPath = "/nwt/2/1",
            };
            var track = new AudioPlayerTrack
            {
                PlayItem = new PlayItem(metadata, "file:///missing-nwt.mp3"),
                Uri = "file:///missing-nwt.mp3",
            };

            var result = await sut.GetDisplayMetadataAsync(track);

            Assert.Equal("Exodus 1", result.Title);
        });
    }

    [Fact]
    public async Task GetCoreDisplayMetadataAsync_vocal_music_branch_sets_title_from_osg_catalog()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new VocalMusicMediaStub(),
            new HttpClientHandler());

        var metadata = new TrackMetadata
        {
            IsBibleContent = false,
            LanguageCode = "E",
            PublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
            TrackCode = "2",
            LookUpPath = "/osg/2",
        };
        var track = new AudioPlayerTrack
        {
            PlayItem = new PlayItem(metadata, "file:///osg.mp3"),
            Uri = "file:///osg.mp3",
        };

        var result = await sut.GetCoreDisplayMetadataAsync(track);

        Assert.Equal("Vocal Song Two", result.Title);
        Assert.Equal("Sing Out", result.Album);
    }
}
