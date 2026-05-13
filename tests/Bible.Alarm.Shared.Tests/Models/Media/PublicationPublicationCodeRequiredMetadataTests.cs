#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationPublicationCodeRequiredMetadataTests
{
    [Fact]
    public void PublicationCode_is_marked_required()
    {
        var p = typeof(Publication).GetProperty(nameof(Publication.PublicationCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
