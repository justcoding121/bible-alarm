#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageDirectionRequiredMetadataTests
{
    [Fact]
    public void Direction_is_marked_required()
    {
        var p = typeof(Language).GetProperty(nameof(Language.Direction))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
