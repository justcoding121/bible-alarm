#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageLanguageNavigationRequiredMetadataTests
{
    [Fact]
    public void Language_navigation_is_marked_required()
    {
        var p = typeof(LanguageNameByLanguage).GetProperty(nameof(LanguageNameByLanguage.Language))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
