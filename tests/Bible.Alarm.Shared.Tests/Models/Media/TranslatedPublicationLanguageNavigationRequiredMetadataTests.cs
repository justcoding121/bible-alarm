#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class TranslatedPublicationLanguageNavigationRequiredMetadataTests
{
    [Fact]
    public void Language_navigation_is_marked_required()
    {
        var p = typeof(TranslatedPublication).GetProperty(nameof(TranslatedPublication.Language))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
