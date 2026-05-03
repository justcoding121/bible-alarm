#nullable enable

using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediatorTrackParserTests
{
    private static List<BiblePublicationTrack> Parse(string filesJson, MediatorTrackParseContext context)
    {
        using var doc = JsonDocument.Parse(filesJson);
        return MediatorTrackParser.ParseTracksFromJson(doc.RootElement, context);
    }

    [Fact]
    public void ParseTracksFromJson_ReturnsEmpty_WhenLanguageOrFormatAbsent()
    {
        var ctx = Ctx("ZZ", "s1");
        Assert.Empty(Parse(@"{}", ctx));
        Assert.Empty(Parse(@"{ ""E"": { } }", Ctx("E", "s1")));
    }

    [Fact]
    public void ParseTracksFromJson_FallsBack_ToLowerMp3Key_OnUppercaseAbsent()
    {
        const string json =
            """{"E":{"mp3":[{"file":"https://aud/x.mp3","title":"Hello"}]}}""";

        var tracks = Parse(
            json,
            Ctx("E", "part-a"));

        Assert.Single(tracks);
        Assert.Equal("part-a", tracks[0].TrackCode);
        Assert.Equal("https://aud/x.mp3", tracks[0].TrackUrl?.Url);
    }

    [Fact]
    public void ParseTracksFromJson_FallsBack_ToLowerMp4Key_OnUppercaseAbsent()
    {
        const string json =
            """{"E":{"mp4":[{"file":"https://v/x.mp4","title":"Vid"}]}}""";

        var tracks = Parse(
            json,
            Ctx("E", "chapter-1", isVideo: true));

        Assert.Single(tracks);
        Assert.Equal("chapter-1", tracks[0].TrackCode);
        Assert.Equal("https://v/x.mp4", tracks[0].TrackUrl?.Url);
    }

    [Fact]
    public void ParseTracksFromJson_UseVideoFormat_WhenIsVideoTrue()
    {
        const string json =
            """{"fr":{"MP4":[{"file":"https://v/a.mp4","title":"Vid"}]}}""";

        var tracks = Parse(
            json,
            Ctx("FR", "m-1", isVideo: true));

        Assert.Single(tracks);
        Assert.Equal("https://v/a.mp4", tracks[0].TrackUrl?.Url);
        Assert.Equal("m-1", tracks[0].TrackCode);
    }

    [Fact]
    public void ParseTracksFromJson_BreaksAfterFirstRenderable_WhenTrackNumberSet()
    {
        const string json =
            """
            {"E":{"MP3":[
              {"file":"https://cdn/1.mp3","title":"A"},
              {"file":"https://cdn/2.mp3","title":"B"}
            ]}}
            """;

        var tracks = Parse(
            json,
            Ctx("E", "sec", trackNumber: 42));

        Assert.Single(tracks);
        Assert.Equal("sec-42", tracks[0].TrackCode);
        Assert.Equal("A", tracks[0].Title);
        Assert.Equal("https://cdn/1.mp3", tracks[0].TrackUrl?.Url);
    }

    [Fact]
    public void ParseTracksFromJson_ReachesLaterEntry_WhenEarlierInvalid_AndBreaksOnceSuccessful()
    {
        const string json =
            """
            {"E":{"MP3":[
              {"title":"skip"},
              {"file":"https://ok/z.mp3","title":"Fine"}
            ]}}
            """;

        var tracks = Parse(
            json,
            Ctx("E", "z", trackNumber: 1));

        Assert.Single(tracks);
        Assert.Equal("Fine", tracks[0].Title);
    }

    [Fact]
    public void ParseTracksFromJson_FileObjectWithUrl_DecodesTitleObjectText()
    {
        const string json =
            """
            {"xx":{"MP3":[{"file":{"url":"https://u/obj"},"title":{"text":"&amp;Amp"}}]}}
            """;

        var tracks = Parse(
            json,
            Ctx("xx", "sc"));

        Assert.Single(tracks);
        Assert.Equal("https://u/obj", tracks[0].TrackUrl?.Url);
        Assert.Equal("&Amp", tracks[0].Title);
    }

    [Fact]
    public void ParseTracksFromJson_UsesUnknownTitle_WhenTitleMissingOrUnexpectedShape()
    {
        const string jsonMissing =
            """{"E":{"MP3":[{"file":"https://a.mp3"}]}}""";
        const string jsonBadShape =
            """{"E":{"MP3":[{"file":"https://b.mp3","title":7}]}}""";

        var t1 = Parse(jsonMissing, Ctx("E", "s"));
        Assert.Equal(MediaTrackTitleHelper.UnknownTitle, t1[0].Title);

        var t2 = Parse(jsonBadShape, Ctx("E", "s"));
        Assert.Equal(MediaTrackTitleHelper.UnknownTitle, t2[0].Title);
    }

    [Fact]
    public void ParseTracksFromJson_LanguageKey_FindsVariantCasing_FromNormalizedUpper()
    {
        const string jsonLower =
            """{"e":{"MP3":[{"file":"https://x.mp3","title":"Low"}]}}""";
        const string jsonUpper =
            """{"E":{"MP3":[{"file":"https://x.mp3","title":"Up"}]}}""";

        var lowerHit = Parse(jsonLower, Ctx("E", "s"));
        var upperHit = Parse(jsonUpper, Ctx("e", "s"));

        Assert.Single(lowerHit);
        Assert.Single(upperHit);
    }

    [Fact]
    public void ParseTracksFromJson_Filters_AudioDescriptionTitles_WhenNotAllowed()
    {
        const string json =
            """{"E":{"MP3":[{"file":"https://ad.mp3","title":"Lesson With Audio Descriptions included"}]}}""";

        Assert.Empty(Parse(json, Ctx("E", "s", allowAudioDescriptionTitles: false)));
        Assert.NotEmpty(Parse(json, Ctx("E", "s", allowAudioDescriptionTitles: true)));
    }

    [Fact]
    public void ResolveTrackCode_DocIdWithTrack_AppendsHyphenSeparation()
    {
        var docSection = $"{AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix}abc987";
        const string json = """{"E":{"MP3":[{"file":"https://d.mp3","title":"D"}]}}""";

        var tracks = Parse(json, Ctx("E", docSection, trackNumber: 2, useDocidParam: true));

        Assert.Equal("abc987-2", tracks[0].TrackCode);
    }

    [Fact]
    public void ResolveTrackCode_DocIdWithoutTrackNumber_ReturnsDocIdValueOnly()
    {
        var docSection = $"{AppConstants.Media.MediatorIdentifiers.DocIdSectionPrefix}abc987";
        const string json = """{"E":{"MP3":[{"file":"https://d.mp3","title":"D"}]}}""";

        var tracks = Parse(json, Ctx("E", docSection, useDocidParam: true));

        Assert.Equal("abc987", tracks[0].TrackCode);
    }

    [Fact]
    public void ResolveTrackCode_DocIdSectionPrefix_IsMatchedCaseInsensitively()
    {
        const string json = """{"E":{"MP3":[{"file":"https://d.mp3","title":"D"}]}}""";

        var upper = Parse(json, Ctx("E", "DOCID:mix", useDocidParam: true));
        Assert.Single(upper);
        Assert.Equal("mix", upper[0].TrackCode);

        var camel = Parse(json, Ctx("E", "DocId:z9", trackNumber: 1, useDocidParam: true));
        Assert.Single(camel);
        Assert.Equal("z9-1", camel[0].TrackCode);
    }

    [Fact]
    public void ResolveTrackCode_OmitTrack_IgnoresTrackNumber_AppendsNothing()
    {
        const string json = """{"E":{"MP3":[{"file":"https://omit.mp3","title":"O"}]}}""";

        var tracks = Parse(
            json,
            Ctx("E", "solo", trackNumber: 77, omitTrackFromUrlParams: true));

        Assert.Single(tracks);
        Assert.Equal("solo", tracks[0].TrackCode);
    }

    [Fact]
    public void ParseTracksFromJson_ReturnsMultipleEntries_WhenTrackNumberNotConstraining()
    {
        const string json =
            """{"E":{"MP3":[{"file":"https://1.mp3","title":"One"},{"file":"https://2.mp3","title":"Two"}]}}""";

        var tracks = Parse(json, Ctx("E", "ch"));

        Assert.Equal(2, tracks.Count);
        Assert.Equal("https://2.mp3", tracks[1].TrackUrl?.Url);
        Assert.Equal("Two", tracks[1].Title);
    }

    [Fact]
    public void ParseTracksFromJson_Rejects_InvalidFileKinds_AndEmptyStringFiles()
    {
        const string primitiveFile =
            """{"E":{"MP3":[{"file":99,"title":"bad"},{"file":"https://good.mp3","title":"Fine"}]}}""";
        const string emptyUrlString =
            """{"E":{"MP3":[{"file":"","title":"x"},{"file":"https://kept.mp3","title":"Held"}]}}""";

        var fromPrimitive = Parse(primitiveFile, Ctx("E", "sect", trackNumber: 1));
        Assert.Single(fromPrimitive);
        Assert.Equal("https://good.mp3", fromPrimitive[0].TrackUrl?.Url);

        var fromEmpty = Parse(emptyUrlString, Ctx("E", "sect"));
        Assert.Single(fromEmpty);
        Assert.Equal("https://kept.mp3", fromEmpty[0].TrackUrl?.Url);
    }

    [Fact]
    public void ParseTracksFromJson_Skips_FileObject_WithMissingUrl_ThenParsesLaterEntry()
    {
        const string json =
            """
            {"E":{"MP3":[
              {"file":{},"title":"skip"},
              {"file":{"other":"x"},"title":"also skip"},
              {"file":{"url":"https://ok.mp3"},"title":"Held"}
            ]}}
            """;

        var tracks = Parse(json, Ctx("E", "sect"));
        Assert.Single(tracks);
        Assert.Equal("https://ok.mp3", tracks[0].TrackUrl?.Url);
        Assert.Equal("Held", tracks[0].Title);
    }

    [Fact]
    public void ParseTracksFromJson_Filters_AudioDescriptionTitles_ForLanguageF_WhenNotAllowed()
    {
        const string json =
            """{"F":{"MP3":[{"file":"https://drop.mp3","title":"avec AUDIODEScription mixed"}]}}""";

        Assert.Empty(Parse(json, Ctx("F", "sc", allowAudioDescriptionTitles: false)));
        Assert.NotEmpty(Parse(json, Ctx("F", "sc", allowAudioDescriptionTitles: true)));
    }

    [Fact]
    public void ResolveTrackCode_When_UseDocParamTrue_But_section_Lacks_doc_prefix_uses_hyphen_scheme()
    {
        const string json = """{"E":{"MP3":[{"file":"https://n.mp3","title":"N"}]}}""";

        var tracks = Parse(json, Ctx("E", "regular-sec", trackNumber: 4, useDocidParam: true));

        Assert.Single(tracks);
        Assert.Equal("regular-sec-4", tracks[0].TrackCode);
    }

    private static MediatorTrackParseContext Ctx(
        string normalizedLanguageCode,
        string sectionCode,
        bool isVideo = false,
        int? trackNumber = null,
        bool allowAudioDescriptionTitles = false,
        bool omitTrackFromUrlParams = false,
        bool useDocidParam = false) =>
        new(
            normalizedLanguageCode,
            sectionCode,
            isVideo,
            trackNumber,
            allowAudioDescriptionTitles,
            omitTrackFromUrlParams,
            useDocidParam);

}
