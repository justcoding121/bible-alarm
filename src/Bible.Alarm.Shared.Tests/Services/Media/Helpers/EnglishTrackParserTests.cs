#nullable enable

using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class EnglishTrackParserTests
{
    private static JsonElement Root(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ParseIamTracks_ReturnsEmpty_WhenEnglishMp3Absent()
    {
        var tracks = EnglishTrackParser.ParseIamTracks(Root("{ }"));
        Assert.Empty(tracks);
    }

    [Fact]
    public void ParseIamTracks_ParsesAllEntries_Under_DefaultLanguage_MP3()
    {
        const string json =
            """
            {"E":{"MP3":[
              {"file":{"url":"https://cdn/iam-1"},"track":1,"title":"One"},
              {"file":{"url":"https://cdn/iam-2"},"track":42,"title":"&lt;two&gt;"}
            ]}}
            """;

        var tracks = EnglishTrackParser.ParseIamTracks(Root(json));

        Assert.Equal(2, tracks.Count);
        Assert.Equal("1", tracks[0].TrackCode);
        Assert.Equal("One", tracks[0].Title);
        Assert.Equal("https://cdn/iam-1", tracks[0].TrackUrl?.Url);

        Assert.Equal("42", tracks[1].TrackCode);
        Assert.Equal("<two>", tracks[1].Title);
        Assert.Equal("https://cdn/iam-2", tracks[1].TrackUrl?.Url);
    }

    [Fact]
    public void ParseBibleTracks_UsesProvidedLanguage_AsTopLevel_Key()
    {
        const string json =
            """{"JV":{"MP3":[{"file":{"url":"https://b/v"},"track":3}]}}""";

        var tracks = EnglishTrackParser.ParseBibleTracks(Root(json), normalizedLanguageCode: "JV");

        Assert.Single(tracks);
        Assert.Equal("3", tracks[0].TrackCode);
        Assert.Equal("https://b/v", tracks[0].TrackUrl?.Url);
    }

    [Fact]
    public void ParseBibleTracks_IsCaseSensitive_On_LanguageKey_And_Format()
    {
        const string json = """{"de":{"MP3":[{"file":{"url":"u"},"track":9}]}}""";

        Assert.Empty(EnglishTrackParser.ParseBibleTracks(Root(json), "DE"));
        Assert.Single(EnglishTrackParser.ParseBibleTracks(Root(json), "de"));
    }

    [Fact]
    public void ParseGenericTracks_AcceptsArbitrary_FormatKeyLike_mp4()
    {
        const string json =
            """
            {"E":{"MP4":[{"file":{"url":"https://vid/mp4"},"track":101,"title":"Z"}]}}
            """;

        var tracks = EnglishTrackParser.ParseGenericTracks(Root(json), "E", AppConstants.Media.MediaStreamFormatMp4);

        Assert.Single(tracks);
        Assert.Equal("101", tracks[0].TrackCode);
        Assert.Equal("Z", tracks[0].Title);
    }

    [Fact]
    public void ParseTrackFromJson_Rejects_InvalidFileShape_MissingTrackOrZero()
    {
        const string mp3MissingFile =
            """{"E":{"MP3":[{"track":9}]}}""";
        const string missingUrl =
            """{"E":{"MP3":[{"file":{},"track":7}]}}""";
        const string trackZero =
            """{"E":{"MP3":[{"file":{"url":"x"},"track":0}]}}""";

        Assert.Empty(EnglishTrackParser.ParseIamTracks(Root(mp3MissingFile)));
        Assert.Empty(EnglishTrackParser.ParseIamTracks(Root(missingUrl)));
        Assert.Empty(EnglishTrackParser.ParseIamTracks(Root(trackZero)));
    }

    [Fact]
    public void ParseIamTracks_Rejects_FileWithEmptyUrl()
    {
        const string json =
            """{"E":{"MP3":[{"file":{"url":""},"track":11}]}}""";

        Assert.Empty(EnglishTrackParser.ParseIamTracks(Root(json)));
    }

    [Fact]
    public void ParseIamTracks_Rejects_EntryMissingTrackNumber()
    {
        const string json =
            """{"E":{"MP3":[{"file":{"url":"https://u"},"title":"NoNum"}]}}""";

        Assert.Empty(EnglishTrackParser.ParseIamTracks(Root(json)));
    }

    [Fact]
    public void ParseGenericTracks_ReturnsEmpty_WhenFormatSubtreeMissing()
    {
        const string json = """{"E":{ }}""";

        Assert.Empty(
            EnglishTrackParser.ParseGenericTracks(Root(json), "E", AppConstants.Media.MediaStreamFormatMp3));
    }

    [Fact]
    public void ParseTrackFromJson_Applies_AudioDescription_Filter()
    {
        const string json =
            """
            {"E":{"MP3":[
              {"file":{"url":"https://skip"},"track":50,"title":"Lesson With Audio Descriptions included"}
            ]}}
            """;

        Assert.Empty(EnglishTrackParser.ParseIamTracks(Root(json)));
    }

    [Fact]
    public void ParseTrackFromJson_DefaultsTitle_WhenPropertyAbsent()
    {
        const string json =
            """{"E":{"MP3":[{"file":{"url":"https://u"},"track":2}]}}""";

        var tr = Assert.Single(EnglishTrackParser.ParseIamTracks(Root(json)));
        Assert.Equal(MediaTrackTitleHelper.UnknownTitle, tr.Title);
        Assert.Equal("2", tr.TrackCode);
    }

    [Fact]
    public void ParseBibleTracks_Uses_DecodedTitle_FromString()
    {
        const string json =
            """{"JV":{"MP3":[{"file":{"url":"u"},"track":88,"title":"A &amp; B"}]}}""";

        var tr = Assert.Single(EnglishTrackParser.ParseBibleTracks(Root(json), "JV"));
        Assert.Equal("A & B", tr.Title);
    }

    [Fact]
    public void ParseBibleTracks_ReturnsEmpty_WhenLanguageNodeExistsButMp3BranchMissing()
    {
        const string json = """{"JV":{"WAV":[{"file":{"url":"https://u"},"track":1}]}}""";

        Assert.Empty(EnglishTrackParser.ParseBibleTracks(Root(json), "JV"));
    }

    [Fact]
    public void ParseGenericTracks_IsCaseSensitive_OnFormatKey()
    {
        const string lowerKey = """{"E":{"mp4":[{"file":{"url":"https://v"},"track":2}]}}""";
        const string upperKey = """{"E":{"MP4":[{"file":{"url":"https://v"},"track":2}]}}""";

        Assert.Empty(EnglishTrackParser.ParseGenericTracks(Root(lowerKey), "E", AppConstants.Media.MediaStreamFormatMp4));
        Assert.Single(EnglishTrackParser.ParseGenericTracks(Root(upperKey), "E", AppConstants.Media.MediaStreamFormatMp4));
    }

    [Fact]
    public void ParseIamTracks_KeepsLaterValidTrack_AfterDiscardingInvalidNeighbors()
    {
        const string json =
            """
            {"E":{"MP3":[
              {"file":{"url":""},"track":1},
              {"file":{"url":"https://cdn/ok"},"track":2},
              {"file":{"url":"https://drop"},"track":0},
              {"track":99,"title":"skipped"}
            ]}}
            """;

        var list = EnglishTrackParser.ParseIamTracks(Root(json));

        Assert.Single(list);
        Assert.Equal("2", list[0].TrackCode);
        Assert.Equal("https://cdn/ok", list[0].TrackUrl?.Url);
    }

    [Fact]
    public void ParseGenericTracks_Filters_AudioDescriptionPhrases_ForNonEnglishLanguageCatalog()
    {
        const string json =
            """
            {"F":{"MP4":[
              {"file":{"url":"https://keep"},"track":1,"title":"Court"},
              {"file":{"url":"https://drop"},"track":2,"title":"Version AVEC audiodescription"}
            ]}}
            """;

        var list = EnglishTrackParser.ParseGenericTracks(Root(json), "F", AppConstants.Media.MediaStreamFormatMp4);

        Assert.Single(list);
        Assert.Equal("1", list[0].TrackCode);
        Assert.Equal("https://keep", list[0].TrackUrl?.Url);
    }
}
