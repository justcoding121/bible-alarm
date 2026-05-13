#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageDisplayAndNameMetadataTests
{
    [Fact]
    public void Display_locale_and_name_lengths_match_EF_contract()
    {
        Assert.Equal(10, MaxLengthOf(nameof(LanguageNameByLanguage.DisplayLanguageCode)));
        Assert.Equal(255, MaxLengthOf(nameof(LanguageNameByLanguage.Name)));
    }

    private static int MaxLengthOf(string propertyName) =>
        typeof(LanguageNameByLanguage).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single()
            .Length;
}
