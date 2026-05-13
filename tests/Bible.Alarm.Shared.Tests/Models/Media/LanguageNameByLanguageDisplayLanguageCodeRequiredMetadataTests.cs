#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageDisplayLanguageCodeRequiredMetadataTests
{
    [Fact]
    public void DisplayLanguageCode_is_marked_required()
    {
        var p = typeof(LanguageNameByLanguage).GetProperty(nameof(LanguageNameByLanguage.DisplayLanguageCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
