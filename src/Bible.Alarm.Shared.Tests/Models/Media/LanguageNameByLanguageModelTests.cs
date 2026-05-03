#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageModelTests
{
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
}
