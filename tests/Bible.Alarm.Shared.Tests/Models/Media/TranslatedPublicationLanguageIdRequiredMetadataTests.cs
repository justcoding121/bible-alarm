#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class TranslatedPublicationLanguageIdRequiredMetadataTests
{
    [Fact]
    public void LanguageId_is_marked_required()
    {
        var p = typeof(TranslatedPublication).GetProperty(nameof(TranslatedPublication.LanguageId))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
