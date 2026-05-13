#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageLanguageCodeRequiredMetadataTests
{
    [Fact]
    public void LanguageCode_is_marked_required()
    {
        var p = typeof(Language).GetProperty(nameof(Language.LanguageCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
