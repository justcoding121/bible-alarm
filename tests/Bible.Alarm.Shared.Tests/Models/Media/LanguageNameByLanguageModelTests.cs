#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageModelTests
{
    [Fact]
    public void Default_ctor_uses_clr_defaults_for_keys_and_nav()
    {
        var sut = new LanguageNameByLanguage();

        Assert.Equal(0, sut.Id);
        Assert.Equal(0, sut.LanguageId);
        Assert.Null(sut.Language);
        Assert.Equal(string.Empty, sut.DisplayLanguageCode);
        Assert.Equal(string.Empty, sut.Name);

        sut.DisplayLanguageCode = "JP";
        sut.Name = "日本語で";
        sut.LanguageId = 404;

        Assert.Equal("JP", sut.DisplayLanguageCode);
        Assert.Equal("日本語で", sut.Name);
        Assert.Equal(404, sut.LanguageId);
        Assert.Null(sut.Language);
    }

    [Fact]
    public void Localized_name_records_display_locale_and_navigation()
    {
        var language = new Language
        {
            Id = 14,
            LanguageCode = "LL",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var sut = new LanguageNameByLanguage
        {
            Id = 900,
            LanguageId = language.Id,
            Language = language,
            DisplayLanguageCode = "E",
            Name = "Sample Language Name",
        };

        Assert.Equal(900, sut.Id);
        Assert.Equal(14, sut.LanguageId);
        Assert.Same(language, sut.Language);
        Assert.Equal("E", sut.DisplayLanguageCode);
        Assert.Equal("Sample Language Name", sut.Name);

        sut.DisplayLanguageCode = "MY";
        sut.Name = "മികച്ച";
        Assert.Equal("MY", sut.DisplayLanguageCode);
        Assert.Equal("മികച്ച", sut.Name);
    }

    [Fact]
    public void Language_navigation_can_attach_after_primitive_fields()
    {
        var language = new Language
        {
            Id = 20,
            LanguageCode = "Z",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        var sut = new LanguageNameByLanguage
        {
            DisplayLanguageCode = "E",
            Name = "Z Language",
            LanguageId = language.Id,
        };

        Assert.Null(sut.Language);

        sut.Language = language;
        Assert.Same(language, sut.Language);
        Assert.Equal(language.Id, sut.LanguageId);
    }
}
