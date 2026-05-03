using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class AudioDescriptionTitlePhrasesTests
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
}
