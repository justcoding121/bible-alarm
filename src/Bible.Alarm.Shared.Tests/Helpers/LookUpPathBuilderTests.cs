using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class LookUpPathBuilderTests
{
    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_UsesMp3FlatQuery_WhenNoSection_AndPublicationIsVocalMusic()
    {
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "E",
            AppConstants.Media.MusicPublicationCodeOsg,
            null,
            "1");

        Assert.Contains($"pub={AppConstants.Media.MusicPublicationCodeOsg}", q);
        Assert.Contains($"fileformat={AppConstants.Media.MediaStreamFormatMp3}", q, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}=E", q);
        Assert.DoesNotContain(AppConstants.Media.GetPubQueryParamName.BookNum, q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_WhitespaceSectionOnly_TreatedAsFlatVocalMusicQuery()
    {
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "E",
            AppConstants.Media.MusicPublicationCodeOsg,
            "   ",
            "1");

        Assert.Contains($"pub={AppConstants.Media.MusicPublicationCodeOsg}", q);
        Assert.Contains($"fileformat={AppConstants.Media.MediaStreamFormatMp3}", q, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AppConstants.Media.GetPubQueryParamName.BookNum, q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_UsesMp4FlatQuery_WhenNoSection_AndPublicationIsNotVocalMusic()
    {
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "E",
            AppConstants.Media.SeriesPublicationCodeThv,
            "",
            "1");

        Assert.Contains($"fileformat={AppConstants.Media.MediaStreamFormatMp4}", q, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_UsesBookNum_WhenSectionIsBibleBook()
    {
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "E",
            AppConstants.Media.BiblePublicationCodeNwt,
            AppConstants.Media.BiblePublicationGenesisBookNumber,
            "1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamName.BookNum}={AppConstants.Media.BiblePublicationGenesisBookNumber}", q);
        Assert.Contains(AppConstants.Media.GetPubQueryAllLangsOff, q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_UsesBookNum_WhenHyphenSectionIsNotDiscStyle()
    {
        const string section = "dram-episode-1";

        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "E",
            AppConstants.Media.BiblePublicationCodeNwt,
            section,
            "1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamName.BookNum}={section}", q);
        Assert.Contains(AppConstants.Media.GetPubQueryAllLangsOff, q);
        Assert.DoesNotContain($"pub={section}", q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_UsesDiscSectionAsPub_WhenSectionMatchesDiscPattern()
    {
        var discSection = $"{AppConstants.Media.MelodyMusicPublicationCodeIam}-9";
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "E",
            AppConstants.Media.MelodyMusicPublicationCodeIam,
            discSection,
            "17");

        Assert.Contains($"pub={discSection}", q);
        Assert.Contains($"fileformat={AppConstants.Media.MediaStreamFormatMp3}", q, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_DiscSectionPrefixMatch_IsOrdinalIgnoreCase()
    {
        var upperIam = AppConstants.Media.MelodyMusicPublicationCodeIam.ToUpperInvariant();
        var discSection = $"{upperIam}-9";
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "DE",
            upperIam,
            discSection,
            "12");

        Assert.Contains($"pub={discSection}", q);
        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_FallsBackToDefaultLanguage_WhenLanguageBlankButNotNoLangPublication()
    {
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "  ",
            AppConstants.Media.BiblePublicationCodeNwt,
            "1",
            "1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_TrimsLanguageCode_WhenProvided()
    {
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "  X  ",
            AppConstants.Media.BiblePublicationCodeNwt,
            "1",
            "1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}=X", q);
    }

    [Fact]
    public void BuildBiblePublicationTrackLookUpPath_ForcesDefaultLanguage_WhenMarkedNoLanguagePublication()
    {
        var q = LookUpPathBuilder.BuildBiblePublicationTrackLookUpPath(
            "RU",
            AppConstants.Media.BiblePublicationCodeNwt,
            "1",
            "1",
            isNoLanguagePublication: true);

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_UsesProvidedLanguage_WhenNotDiscStyleDownload()
    {
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            AppConstants.Media.MusicPublicationCodeSjjc,
            "FR",
            "1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}=FR", q);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_PrefersDownloadCode_OverPublicationCode()
    {
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            AppConstants.Media.MelodyMusicPublicationCodeIam,
            "E",
            "3",
            downloadCode: "iam-2");

        Assert.Contains("pub=iam-2", q);
        Assert.Contains($"fileformat={AppConstants.Media.MediaStreamFormatMp3}", q, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_ForDiscDownloadForcesEnglishLanguageParameter()
    {
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            AppConstants.Media.MelodyMusicPublicationCodeIam,
            "RU",
            "1",
            downloadCode: $"{AppConstants.Media.MelodyMusicPublicationCodeIam}-1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_DiscDownloadForcesEnglish_WhenPublicationCasingDiffersFromDownloadCode()
    {
        var iam = AppConstants.Media.MelodyMusicPublicationCodeIam;
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            iam.ToUpperInvariant(),
            "KO",
            "2",
            downloadCode: $"{iam}-3");

        Assert.Contains("pub=iam-3", q);
        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_UsesDefaultLanguage_WhenLanguageNullAndNotDiscStyle()
    {
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            AppConstants.Media.MusicPublicationCodeSjjc,
            languageCode: null,
            trackCode: "1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_ForcesDefaultLanguage_WhenMarkedNoLanguagePublication()
    {
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            AppConstants.Media.MusicPublicationCodeSjjc,
            "DE",
            "1",
            isNoLanguagePublication: true);

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}={AppConstants.Media.DefaultLanguageCode}", q);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_KeepsLanguage_WhenHyphenDownloadIsNotDiscStyle()
    {
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            AppConstants.Media.MelodyMusicPublicationCodeIam,
            "KO",
            "1",
            downloadCode: $"other-{AppConstants.Media.MelodyMusicPublicationCodeIam}-1");

        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}=KO", q);
        Assert.Contains("pub=other-iam-1", q);
    }

    [Fact]
    public void BuildMusicTrackLookUpPath_UsesAlternatePub_WhenPlainDownloadOverrideProvided()
    {
        var q = LookUpPathBuilder.BuildMusicTrackLookUpPath(
            AppConstants.Media.MusicPublicationCodeSjjc,
            "ZH",
            "2",
            downloadCode: "altpub");

        Assert.Contains("pub=altpub", q);
        Assert.Contains($"{AppConstants.Media.GetPubQueryParamLangWritten}=ZH", q);
    }

    [Fact]
    public void BuildMediatorTrackLookUpPath_ContainsCategoryAndLang()
    {
        const string category = "DramasGoodNews";
        var q = LookUpPathBuilder.BuildMediatorTrackLookUpPath(category, "E", "42", naturalKey: "ignored-for-path");

        Assert.Contains($"{AppConstants.Media.MediatorQueryParamName.Category}={category}", q);
        Assert.Contains($"{AppConstants.Media.MediatorQueryParamName.Lang}=E", q);
    }
}
