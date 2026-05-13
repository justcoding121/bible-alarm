#nullable enable

using System.Text.Json;
using Bible.Alarm.Cataloger.Catalogers;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class MusicTrackCatalogerParsingTests
{
    [Fact]
    public void BuildMusicCatalogLink_embeds_default_language_when_language_code_is_null()
    {
        var link = MusicTrackCatalogParsing.BuildMusicCatalogLink("sjjm", languageCode: null);

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", link);
    }

    [Fact]
    public void BuildMusicCatalogLink_embeds_explicit_language_code_when_provided()
    {
        var link = MusicTrackCatalogParsing.BuildMusicCatalogLink("sjjm", languageCode: "S");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}=S", link);
    }

    [Fact]
    public void ProcessMusicFiles_skips_zip_urls()
    {
        var json = $$"""
            [{
              "{{AppConstants.Media.PubMediaJson.File}}": { "{{AppConstants.Media.PubMediaJson.Url}}": "https://cdn.example.com/album.zip" },
              "{{AppConstants.Media.PubMediaJson.Track}}": 1,
              "{{AppConstants.Media.PubMediaJson.Title}}": "Zip Track"
            }]
            """;

        using var doc = JsonDocument.Parse(json);
        var tracks = new List<MusicTrack>();

        var nextCode = MusicTrackCatalogParsing.ProcessMusicFiles(doc.RootElement, "pub", null, trackCode: 1, tracks);

        Assert.Empty(tracks);
        Assert.Equal(1, nextCode);
    }

    [Fact]
    public void ProcessMusicFiles_skips_track_number_zero()
    {
        var json = $$"""
            [{
              "{{AppConstants.Media.PubMediaJson.File}}": { "{{AppConstants.Media.PubMediaJson.Url}}": "https://cdn.example.com/a.mp3" },
              "{{AppConstants.Media.PubMediaJson.Track}}": 0,
              "{{AppConstants.Media.PubMediaJson.Title}}": "Silent"
            }]
            """;

        using var doc = JsonDocument.Parse(json);
        var tracks = new List<MusicTrack>();

        _ = MusicTrackCatalogParsing.ProcessMusicFiles(doc.RootElement, "pub", null, trackCode: 1, tracks);

        Assert.Empty(tracks);
    }

    [Fact]
    public void ProcessMusicFiles_adds_music_track_when_entry_is_valid_and_track_positive()
    {
        var json = $$"""
            [{
              "{{AppConstants.Media.PubMediaJson.File}}": { "{{AppConstants.Media.PubMediaJson.Url}}": "https://cdn.example.com/a.mp3" },
              "{{AppConstants.Media.PubMediaJson.Track}}": 3,
              "{{AppConstants.Media.PubMediaJson.Title}}": "Song"
            }]
            """;

        using var doc = JsonDocument.Parse(json);
        var tracks = new List<MusicTrack>();

        var nextCode = MusicTrackCatalogParsing.ProcessMusicFiles(doc.RootElement, "melpub", languageCode: "E", trackCode: 10, tracks);

        Assert.Single(tracks);
        Assert.Equal("https://cdn.example.com/a.mp3", tracks[0].Url);
        Assert.Equal(10, tracks[0].Number);
        Assert.Equal(3, tracks[0].OriginalTrackCode);
        Assert.Equal("melpub", tracks[0].DownloadCode);
        Assert.Equal(11, nextCode);
    }
}
