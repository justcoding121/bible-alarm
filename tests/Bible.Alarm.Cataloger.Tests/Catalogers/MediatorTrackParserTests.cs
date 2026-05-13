#nullable enable

using Bible.Alarm.Cataloger.Catalogers;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class MediatorTrackParserTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public void ParseTracksFromGetPubMediaLinks_returns_null_tracks_when_files_property_missing()
    {
        var (tracks, sectionName) = MediatorTrackParser.ParseTracksFromGetPubMediaLinks(
            "{}",
            sectionCode: "GEN",
            languageCode: "E",
            SilentLogger);

        Assert.Null(tracks);
        Assert.Null(sectionName);
    }

    [Fact]
    public void ParseTracksFromGetPubMediaLinks_returns_track_with_expected_lookup_path_and_section_name()
    {
        var fmt = AppConstants.Media.MediaStreamFormatMp3;
        var json = $$"""
            {
              "{{AppConstants.Media.PubMediaJson.PubName}}": "Genesis",
              "{{AppConstants.Media.PubMediaJson.Files}}": {
                "E": {
                  "{{fmt}}": [
                    {
                      "{{AppConstants.Media.PubMediaJson.File}}": { "{{AppConstants.Media.PubMediaJson.Url}}": "https://cdn.example.com/gen.mp3" },
                      "{{AppConstants.Media.PubMediaJson.Title}}": "Intro"
                    }
                  ]
                }
              }
            }
            """;

        var (tracks, sectionName) = MediatorTrackParser.ParseTracksFromGetPubMediaLinks(
            json,
            sectionCode: "GEN",
            languageCode: "E",
            SilentLogger);

        Assert.NotNull(tracks);
        var list = tracks!;
        Assert.Single(list);
        Assert.Equal("Genesis", sectionName);
        Assert.Equal("GEN", list[0].TrackCode);
        Assert.Equal("https://cdn.example.com/gen.mp3", list[0].Url);
        Assert.Contains("GEN", list[0].LookUpPath, StringComparison.Ordinal);
        Assert.Contains(fmt, list[0].LookUpPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseTracksFromGetPubMediaLinks_malformed_json_returns_null_tracks()
    {
        var (tracks, _) = MediatorTrackParser.ParseTracksFromGetPubMediaLinks(
            "{not-json",
            sectionCode: "GEN",
            languageCode: "E",
            SilentLogger);

        Assert.Null(tracks);
    }
}
