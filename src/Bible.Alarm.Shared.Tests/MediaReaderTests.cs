#nullable enable

using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediaReaderTests : IDisposable
{
    private readonly string indexRoot = Path.Combine(Path.GetTempPath(), $"ba-mr-{Guid.NewGuid():N}");

    public MediaReaderTests() => Directory.CreateDirectory(indexRoot);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(indexRoot))
            {
                Directory.Delete(indexRoot, true);
            }
        }
        catch
        {
            // Best-effort temp cleanup after non-admin tests on Windows occasionally locks.
        }
    }

    private MediaReader Reader => new(indexRoot);

    private static Task WriteUtf8(string fullPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        return File.WriteAllTextAsync(fullPath, content);
    }

    [Fact]
    public async Task GetBiblePublicationLanguages_DeserializesToDictionaryIgnoringCaseKeys()
    {
        var langsPath = Path.Combine(
            indexRoot,
            AppConstants.ApiEndpoints.MediaIndexFolderAudio,
            AppConstants.Media.BiblePublicationCategoryBible,
            AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);

        await WriteUtf8(
            langsPath,
            """[{"LanguageCode":"e","Direction":"ltr"},{"LanguageCode":"J","Direction":"rtl"}]""");

        var map = await Reader.GetBiblePublicationLanguages();

        Assert.Equal(2, map.Count);
        Assert.Equal("e", map["e"].LanguageCode);
        Assert.Equal("J", map["j"].LanguageCode);
    }

    [Fact]
    public async Task GetBiblePublications_BuildsKeyedMap_ByPublicationCode()
    {
        var path = Path.Combine(
            indexRoot,
            AppConstants.ApiEndpoints.MediaIndexFolderAudio,
            AppConstants.Media.BiblePublicationCategoryBible,
            "JV",
            AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);

        await WriteUtf8(
            path,
            """[{"Name":"Nw","PublicationCode":"nw"},{"Name":"Ay","PublicationCode":"ay"}]""");

        var map = await Reader.GetBiblePublications("JV");

        Assert.Equal("Nw", map["nw"].Name);
        Assert.Equal("Ay", map["Ay"].Name);
    }

    [Fact]
    public async Task GetBiblePublicationSections_Dedupes_IgnoresBlankCodes_SortsNaturally()
    {
        var sectionsPath = Path.Combine(
            indexRoot,
            AppConstants.ApiEndpoints.MediaIndexFolderAudio,
            AppConstants.Media.BiblePublicationCategoryBible,
            "E",
            "nw",
            AppConstants.ApiEndpoints.MediaIndexSectionsFileName);

        await WriteUtf8(
            sectionsPath,
            JsonSerializer.Serialize(
                new[]
                {
                    new { SectionCode = "10", Name = "Later", BiblePublicationId = 1 },
                    new { SectionCode = "2", Name = "Early", BiblePublicationId = 1 },
                    new { SectionCode = "2", Name = "Duplicate", BiblePublicationId = 1 },
                    new { SectionCode = "   ", Name = "BlankCode", BiblePublicationId = 1 },
                }));

        var map = await Reader.GetBiblePublicationSections("E", "nw");

        Assert.Equal(2, map.Count);
        var ordered = map.Keys.ToList();
        Assert.Equal("2", ordered[0]);
        Assert.Equal("10", ordered[1]);
        Assert.Equal("Early", map["2"].Name);
    }

    [Fact]
    public async Task GetBiblePublicationTracks_Sorts_ByNumericTrackOrdering()
    {
        var trackPath = Path.Combine(
            indexRoot,
            AppConstants.ApiEndpoints.MediaIndexFolderAudio,
            AppConstants.Media.BiblePublicationCategoryBible,
            "JV",
            "nw",
            "gen",
            AppConstants.ApiEndpoints.MediaIndexTracksFileName);

        await WriteUtf8(
            trackPath,
            """[{"TrackCode":"110","Title":"Late"},{"TrackCode":"11","Title":"Mid"},{"TrackCode":"2","Title":"Early"}]""");

        var map = await Reader.GetBiblePublicationTracks("JV", "nw", "gen");

        var titlesInOrder = map.Values.Select(v => v.Title).ToArray();
        Assert.Equal(new[] { "Early", "Mid", "Late" }, titlesInOrder);
    }

    [Fact]
    public async Task GetMelodyMusicReleases_And_Tracks_UseMusicLayout()
    {
        var releasesPath = Path.Combine(
            indexRoot,
            AppConstants.Media.BiblePublicationCategoryMusic,
            AppConstants.ApiEndpoints.MediaIndexFolderMelodies,
            AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);

        await WriteUtf8(
            releasesPath,
            """[{"Name":"M1","PublicationCode":"iam-x"}]""");

        var trackPath = Path.Combine(
            indexRoot,
            AppConstants.Media.BiblePublicationCategoryMusic,
            AppConstants.ApiEndpoints.MediaIndexFolderMelodies,
            "iam-x",
            AppConstants.ApiEndpoints.MediaIndexTracksFileName);

        await WriteUtf8(
            trackPath,
            """[{"TrackCode":"03","Title":"C","Url":"","LookUpPath":""},{"TrackCode":"2","Title":"B","Url":"","LookUpPath":""},{"TrackCode":"001","Title":"A","Url":"","LookUpPath":""}]""");

        var rel = await Reader.GetMelodyMusicReleases();
        Assert.Equal("iam-x", rel["Iam-X"].PublicationCode);

        var tracks = await Reader.GetMelodyMusicTracks("iam-x");
        Assert.Equal(3, tracks.Count);
        Assert.Equal(["A", "B", "C"], tracks.Values.Select(v => v.Title));
    }

    [Fact]
    public async Task GetVocalMusicLanguages_Releases_Tracks_UseVocalLayout()
    {
        var langPath = Path.Combine(
            indexRoot,
            AppConstants.Media.BiblePublicationCategoryMusic,
            AppConstants.ApiEndpoints.MediaIndexFolderVocals,
            AppConstants.ApiEndpoints.MediaIndexLanguagesFileName);

        await WriteUtf8(langPath, """[{"LanguageCode":"fr","Direction":"ltr"}]""");

        var pubPath = Path.Combine(
            indexRoot,
            AppConstants.Media.BiblePublicationCategoryMusic,
            AppConstants.ApiEndpoints.MediaIndexFolderVocals,
            "fr",
            AppConstants.ApiEndpoints.MediaIndexPublicationsFileName);

        await WriteUtf8(
            pubPath,
            """[{"Name":"Song","PublicationCode":"song-pub"}]""");

        var trackPath = Path.Combine(
            indexRoot,
            AppConstants.Media.BiblePublicationCategoryMusic,
            AppConstants.ApiEndpoints.MediaIndexFolderVocals,
            "fr",
            "song-pub",
            AppConstants.ApiEndpoints.MediaIndexTracksFileName);

        await WriteUtf8(
            trackPath,
            """[{"TrackCode":"2","Title":"Second","Url":"","LookUpPath":""},{"TrackCode":"1","Title":"First","Url":"","LookUpPath":""}]""");

        var langs = await Reader.GetVocalMusicLanguages();
        Assert.Equal("fr", langs["FR"].LanguageCode);

        var releases = await Reader.GetVocalMusicReleases("FR");
        Assert.Single(releases);
        Assert.Equal("Song", releases["song-pub"].Name);

        var tracks = await Reader.GetVocalMusicTracks("FR", "song-pub");
        Assert.Equal(2, tracks.Count);
        Assert.Equal("First", tracks[0].Title);
        Assert.Equal("Second", tracks[1].Title);
    }
}
