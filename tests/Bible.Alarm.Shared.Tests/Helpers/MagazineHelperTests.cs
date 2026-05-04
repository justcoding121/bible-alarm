using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MagazineHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("w")]
    public void Detectors_ReturnFalse_WhenPublicationCodeTooShort(string? code)
    {
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode(code));
        Assert.False(MagazineHelper.IsAwakeMagazineCode(code));
        Assert.False(MagazineHelper.IsMagazinePublicationCode(code));
    }

    [Fact]
    public void IsWatchtowerMagazineCode_False_ForAwakeLetterOrMalformedYearTrailingCharacters()
    {
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode($"g{MagazineHelper.MagazineStartYear}"));
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode("watchtower2009")); // lacks leading w...
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode($"{MagazineHelper.MagazineStartYear}w")); // wrong shape
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode($"w{MagazineHelper.MagazineStartYear}z"));
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode($"{MagazineHelper.MagazineStartYear}")); // numeric-only, no prefix
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode($"w{MagazineHelper.MagazineStartYear}x")); // trailing garbage
    }

    [Fact]
    public void IsAwakeMagazineCode_False_ForWatchtowerLetterOrMalformedYearTrailingCharacters()
    {
        Assert.False(MagazineHelper.IsAwakeMagazineCode($"w{MagazineHelper.MagazineStartYear}"));
        Assert.False(MagazineHelper.IsAwakeMagazineCode($"g{MagazineHelper.MagazineStartYear}book"));
        Assert.False(MagazineHelper.IsAwakeMagazineCode("gab2008")); // trailing letters block year parse
    }

    [Fact]
    public void MagazineEndYear_Applies_AsUpperInclusiveBound_ForYearDetectors()
    {
        Assert.True(MagazineHelper.MagazineEndYear >= DateTime.UtcNow.Year);

        var end = $"w{MagazineHelper.MagazineEndYear}";
        Assert.True(MagazineHelper.IsWatchtowerMagazineCode(end));
        Assert.True(MagazineHelper.IsMagazinePublicationCode($"g{MagazineHelper.MagazineEndYear}"));
    }

    [Fact]
    public void IsWatchtowerMagazineCode_False_BeforeMinimumYearOrAfterEndYear()
    {
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode("w2007"));
        Assert.False(MagazineHelper.IsWatchtowerMagazineCode($"w{MagazineHelper.MagazineEndYear + 1}"));
        Assert.False(MagazineHelper.IsAwakeMagazineCode($"g{MagazineHelper.MagazineEndYear + 1}"));
    }

    [Fact]
    public void IsMagazinePublicationCode_True_ForPublicationCodesInsideYearWindow()
    {
        Assert.True(MagazineHelper.IsWatchtowerMagazineCode($"w{MagazineHelper.MagazineStartYear}"));
        Assert.True(MagazineHelper.IsAwakeMagazineCode($"g{MagazineHelper.MagazineStartYear}"));
        Assert.True(MagazineHelper.IsMagazinePublicationCode($"w{MagazineHelper.MagazineEndYear}"));
    }

    [Fact]
    public void IsMagazineCode_DetectorsIgnoreCase_OnYearPrefixLetter()
    {
        Assert.True(MagazineHelper.IsWatchtowerMagazineCode($"W{MagazineHelper.MagazineStartYear}"));
        Assert.True(MagazineHelper.IsAwakeMagazineCode($"G{MagazineHelper.MagazineStartYear}"));
        Assert.True(MagazineHelper.IsMagazinePublicationCode($"G{MagazineHelper.MagazineStartYear}"));
    }

    [Fact]
    public void GetYear_ParsesTrailingYearDigits()
    {
        Assert.Equal(2012, MagazineHelper.GetYear("w2012"));
        Assert.Equal(2012, MagazineHelper.GetYear("g2012"));
    }

    [Fact]
    public void ParseSectionCode_SplitsOnLastDash()
    {
        var (api, issue) = MagazineHelper.ParseSectionCode("20080101-wp");
        Assert.Equal("wp", api);
        Assert.Equal("20080101", issue);

        var (apiNested, issueNested) = MagazineHelper.ParseSectionCode("prefix-suffix-issue-wp");
        Assert.Equal("wp", apiNested);
        Assert.Equal("prefix-suffix-issue", issueNested);

        var (apiLeadingDash, emptyIssue) = MagazineHelper.ParseSectionCode("-wp");
        Assert.Equal("wp", apiLeadingDash);
        Assert.Empty(emptyIssue);
    }

    [Fact]
    public void ParseSectionCode_Throws_WhenNoDash()
    {
        Assert.Throws<ArgumentException>(() => MagazineHelper.ParseSectionCode("bad"));
    }

    [Fact]
    public void BuildSectionCode_RoundTripsWithParse()
    {
        const string api = "wp";
        const string issue = "20100301";
        var built = MagazineHelper.BuildSectionCode(api, issue);
        var parsed = MagazineHelper.ParseSectionCode(built);
        Assert.Equal(api, parsed.ApiPubCode);
        Assert.Equal(issue, parsed.IssueCode);
    }

    [Theory]
    [InlineData(null, null, "")]
    [InlineData("", "", "")]
    [InlineData("Pub", null, "Pub")]
    [InlineData(null, "Jan", "Jan")]
    [InlineData("Pub", "Jan", "Pub — Jan")]
    [InlineData("Watchtower &amp; Study", null, "Watchtower & Study")]
    [InlineData("Issue", "Jan \u00A01", "Issue — Jan  1")]
    public void BuildSectionName_FormatsDecodedStrings(string? pub, string? date, string expected)
    {
        Assert.Equal(expected, MagazineHelper.BuildSectionName(pub, date));
    }

    [Fact]
    public void BuildSectionName_CombinesHtmlDecodedPubAndPlainDate()
    {
        Assert.Equal(
            "A & B — Jan",
            MagazineHelper.BuildSectionName("A &amp; B", "Jan"));
    }

    [Fact]
    public void GetPossibleIssues_Awake_EmitsMonthlyCodesEndingWithDecember()
    {
        var issues = MagazineHelper.GetPossibleIssues(MagazineHelper.MagazineStartYear, isWatchtower: false);
        Assert.Equal(12, issues.Count);
        Assert.Contains(("g", $"{MagazineHelper.MagazineStartYear}12"), issues);
    }

    [Fact]
    public void GetPossibleIssues_Watchtower_Pre2016_HasTwentyFourEntries()
    {
        var issues = MagazineHelper.GetPossibleIssues(2015, isWatchtower: true);
        Assert.Equal(24, issues.Count);
        Assert.All(issues, pair => Assert.True(pair.ApiPubCode is "w" or "wp"));
        Assert.All(issues, pair =>
            Assert.True(pair.IssueCode.EndsWith("01", StringComparison.Ordinal) ||
                pair.IssueCode.EndsWith("15", StringComparison.Ordinal)));
    }

    [Fact]
    public void GetPossibleIssues_Watchtower_Post2016_HasTwentyFourEntries()
    {
        var issues = MagazineHelper.GetPossibleIssues(2016, isWatchtower: true);
        Assert.Equal(24, issues.Count);
    }

    [Fact]
    public void GetPossibleIssues_Awake_HasTwelveEntries()
    {
        var issues = MagazineHelper.GetPossibleIssues(2020, isWatchtower: false);
        Assert.Equal(12, issues.Count);
        Assert.All(issues, pair => Assert.Equal("g", pair.ApiPubCode));
    }

    [Theory]
    [InlineData("g", 2020, "g2020")]
    [InlineData("w", 2020, "w2020")]
    [InlineData("wp", 2020, "w2020")]
    public void GetPublicationCode_UsesWatchtowerPublicationPrefixExceptAwake(string api, int year, string expected)
    {
        Assert.Equal(expected, MagazineHelper.GetPublicationCode(api, year));
    }

    [Theory]
    [InlineData("20080301", 2008)]
    [InlineData("201601", 2016)]
    public void GetYearFromIssueCode_ReadsLeadingFourDigitYear(string issue, int expectedYear)
    {
        Assert.Equal(expectedYear, MagazineHelper.GetYearFromIssueCode(issue));
    }
}
