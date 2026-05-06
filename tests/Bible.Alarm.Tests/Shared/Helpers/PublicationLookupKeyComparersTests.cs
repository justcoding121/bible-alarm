#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class PublicationLookupKeyComparersTests
{
    [Fact]
    public void LanguagePublication_ignores_case_for_both_fields()
    {
        var cmp = PublicationLookupKeyComparers.LanguagePublication.Instance;
        var a = ("en", "nwt");
        var b = ("EN", "NWT");

        Assert.True(cmp.Equals(a, b));
        Assert.Equal(cmp.GetHashCode(a), cmp.GetHashCode(b));
        Assert.False(cmp.Equals(a, ("en", "nwtsty")));
    }

    [Fact]
    public void LanguagePublicationSection_ignores_case_for_all_fields()
    {
        var cmp = PublicationLookupKeyComparers.LanguagePublicationSection.Instance;
        var a = ("en", "pub", "sec1");
        var b = ("EN", "PUB", "SEC1");

        Assert.True(cmp.Equals(a, b));
        Assert.Equal(cmp.GetHashCode(a), cmp.GetHashCode(b));
        Assert.False(cmp.Equals(a, ("en", "pub", "other")));
    }

    [Fact]
    public void LanguagePublicationNullableSectionTrack_normalizes_null_section_and_uses_ordinal_track()
    {
        var cmp = PublicationLookupKeyComparers.LanguagePublicationNullableSectionTrack.Instance;
        (string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode) withNullSection =
            ("en", "pub", null, "trk");
        (string LanguageCode, string PublicationCode, string? SectionCode, string TrackCode) withEmptySection =
            ("EN", "PUB", "", "trk");

        Assert.True(cmp.Equals(withNullSection, withEmptySection));
        Assert.Equal(cmp.GetHashCode(withNullSection), cmp.GetHashCode(withEmptySection));

        Assert.False(cmp.Equals(withNullSection, ("en", "pub", null, "TRK")));
        Assert.True(cmp.Equals(("en", "pub", "s", "x"), ("en", "pub", "S", "x")));
    }

    [Fact]
    public void PublicationSection_ignores_case()
    {
        var cmp = PublicationLookupKeyComparers.PublicationSection.Instance;
        var a = ("pub", "sec");
        Assert.True(cmp.Equals(a, ("PUB", "SEC")));
        Assert.Equal(cmp.GetHashCode(a), cmp.GetHashCode(("pub", "sec")));
    }

    [Fact]
    public void PublicationNullableSectionTrack_matches_null_and_empty_section()
    {
        var cmp = PublicationLookupKeyComparers.PublicationNullableSectionTrack.Instance;
        (string PublicationCode, string? SectionCode, string TrackCode) a = ("pub", null, "t");
        (string PublicationCode, string? SectionCode, string TrackCode) b = ("PUB", "", "t");

        Assert.True(cmp.Equals(a, b));
        Assert.Equal(cmp.GetHashCode(a), cmp.GetHashCode(b));
    }

    [Fact]
    public void PublicationNullableLanguageCode_matches_null_and_empty_language()
    {
        var cmp = PublicationLookupKeyComparers.PublicationNullableLanguageCode.Instance;
        (string PublicationCode, string? LanguageCode) a = ("pub", null);
        (string PublicationCode, string? LanguageCode) b = ("PUB", "");

        Assert.True(cmp.Equals(a, b));
        Assert.Equal(cmp.GetHashCode(a), cmp.GetHashCode(b));
        Assert.False(cmp.Equals(a, ("other", null)));
    }

    [Fact]
    public void PublicationLanguage_ignores_case_for_both_codes()
    {
        var cmp = PublicationLookupKeyComparers.PublicationLanguage.Instance;
        var a = ("pub", "en");

        Assert.True(cmp.Equals(a, ("PUB", "EN")));
        Assert.Equal(cmp.GetHashCode(a), cmp.GetHashCode(("pub", "en")));
    }
}
