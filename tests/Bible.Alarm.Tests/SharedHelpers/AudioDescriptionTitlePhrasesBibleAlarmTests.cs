#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class AudioDescriptionTitlePhrasesBibleAlarmTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ContainsAudioDescriptionPhrase_ReturnsFalse_ForBlankTitle(string? title)
    {
        Assert.False(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase("E", title));
    }

    [Fact]
    public void ContainsAudioDescriptionPhrase_FindsEmbeddedEnglishPhrase_SubstringMatch()
    {
        Assert.True(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase(
            "E",
            "Lesson With Audio Descriptions included"));
    }

    [Fact]
    public void GetPhrasesForLanguage_ReturnsFallback_WhenUnknownLanguageSymbol()
    {
        var english = AudioDescriptionTitlePhrases.GetPhrasesForLanguage(AppConstants.Media.DefaultLanguageCode);
        var fallback = AudioDescriptionTitlePhrases.GetPhrasesForLanguage("ZZ-UnknownLang");
        Assert.Equal(english, fallback);
        Assert.NotEmpty(fallback);
    }

    [Fact]
    public void GetPhrasesForLanguage_TrimsLanguageCode_Key()
    {
        var spaced = AudioDescriptionTitlePhrases.GetPhrasesForLanguage("  E  ");
        Assert.NotEmpty(spaced);
        Assert.True(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase("  E ", "With Audio Descriptions"));
    }

    [Fact]
    public void GetPhrasesForLanguage_null_or_trimmed_empty_behaves_like_default_language_list()
    {
        var english = AudioDescriptionTitlePhrases.GetPhrasesForLanguage(AppConstants.Media.DefaultLanguageCode);

        Assert.Equal(english, AudioDescriptionTitlePhrases.GetPhrasesForLanguage(null));

        Assert.Equal(english, AudioDescriptionTitlePhrases.GetPhrasesForLanguage(""));

        Assert.Equal(english, AudioDescriptionTitlePhrases.GetPhrasesForLanguage("    "));
    }

    [Fact]
    public void ContainsAudioDescriptionPhrase_ReturnsFalse_WhenTitleOmitsConfiguredMarkers()
    {
        Assert.False(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase("E", "Chapter review only"));
        Assert.False(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase("F", "Court métrage"));
    }

    [Fact]
    public void ContainsAudioDescriptionPhrase_MatchesNonEnglishEmbeddedPhrasesIgnoringCase()
    {
        Assert.True(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase("AF", "Program MET AUDIObeskrywings"));
        Assert.True(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase("F", "Version AVEC Audiodescription"));
        Assert.True(AudioDescriptionTitlePhrases.ContainsAudioDescriptionPhrase("VT", "Bản có Với Mô Tả Âm Thanh phụ đề"));
    }

    [Fact]
    public void GetPhrasesForLanguage_ReturnsDistinctList_WhenCultureSpecificEntryRegistered()
    {
        var french = AudioDescriptionTitlePhrases.GetPhrasesForLanguage("F");
        var english = AudioDescriptionTitlePhrases.GetPhrasesForLanguage("E");

        Assert.NotEqual(english, french);
        Assert.Contains("avec audiodescription", french);
        Assert.DoesNotContain("With Audio Descriptions", french);
    }

}
