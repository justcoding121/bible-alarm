#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSchedulePublicationCodeRequiredMetadataTests
{
    [Fact]
    public void PublicationCode_is_marked_required()
    {
        var p = typeof(BiblePublicationSchedule).GetProperty(nameof(BiblePublicationSchedule.PublicationCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
