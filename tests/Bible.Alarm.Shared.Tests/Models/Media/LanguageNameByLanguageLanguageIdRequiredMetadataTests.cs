#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageLanguageIdRequiredMetadataTests
{
    [Fact]
    public void LanguageId_is_marked_required()
    {
        var p = typeof(LanguageNameByLanguage).GetProperty(nameof(LanguageNameByLanguage.LanguageId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
