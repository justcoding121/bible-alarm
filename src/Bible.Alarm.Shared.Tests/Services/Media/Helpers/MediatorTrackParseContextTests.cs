#nullable enable

using Bible.Alarm.Shared.Services.Media.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediatorTrackParseContextTests
{
    [Fact]
    public void Positional_Minimal_Use_Default_Boolean_And_Parse_Toggles()
    {
        var sut = new MediatorTrackParseContext("en-gr", "book-101");

        Assert.Equal("en-gr", sut.NormalizedLanguageCode);
        Assert.Equal("book-101", sut.SectionCode);
        Assert.False(sut.IsVideo);
        Assert.Null(sut.TrackNumber);
        Assert.False(sut.AllowAudioDescriptionTitles);
        Assert.False(sut.OmitTrackFromUrlParams);
        Assert.False(sut.UseDocidParam);

        sut.Deconstruct(out var lc, out var sec, out var video, out var num, out var adTitle, out var omitTk, out var docId);
        Assert.Equal("en-gr", lc);
        Assert.Equal("book-101", sec);
        Assert.False(video);
        Assert.Null(num);
        Assert.False(adTitle);
        Assert.False(omitTk);
        Assert.False(docId);

        Assert.Equal(sut, sut with { });
    }

    [Fact]
    public void Named_Constructor_Overrides_Default_Toggles()
    {
        var sut = new MediatorTrackParseContext(
            NormalizedLanguageCode: "M",
            SectionCode: "dwj_E_S_1_AUDIO",
            IsVideo: true,
            TrackNumber: 18,
            AllowAudioDescriptionTitles: true,
            OmitTrackFromUrlParams: true,
            UseDocidParam: true);

        Assert.True(sut.IsVideo);
        Assert.Equal(18, sut.TrackNumber);
        Assert.True(sut.AllowAudioDescriptionTitles);
        Assert.True(sut.OmitTrackFromUrlParams);
        Assert.True(sut.UseDocidParam);

        var toggledAudioOff = sut with { AllowAudioDescriptionTitles = false };
        Assert.False(toggledAudioOff.AllowAudioDescriptionTitles);
        Assert.True(toggledAudioOff.UseDocidParam);
        Assert.NotEqual(sut, toggledAudioOff);
        Assert.True(sut == sut with { }); // shallow copy semantics
    }

    [Fact]
    public void With_Can_Override_Tracked_Field_While_Preserving_Equality_With()
    {
        var baseCtx = new MediatorTrackParseContext("E", "sec-a", OmitTrackFromUrlParams: false);
        var omitted = baseCtx with { OmitTrackFromUrlParams = true, TrackNumber = 999 };

        Assert.True(omitted.OmitTrackFromUrlParams);
        Assert.Equal(999, omitted.TrackNumber);
        Assert.Equal(baseCtx.NormalizedLanguageCode, omitted.NormalizedLanguageCode);

        omitted.Deconstruct(out _, out _, out _, out var num, out _, out _, out _);
        Assert.Equal(999, num);
    }
}
