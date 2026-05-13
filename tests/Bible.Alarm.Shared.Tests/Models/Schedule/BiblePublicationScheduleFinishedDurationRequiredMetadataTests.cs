#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleFinishedDurationRequiredMetadataTests
{
    [Fact]
    public void FinishedDuration_is_marked_required()
    {
        var p = typeof(BiblePublicationSchedule).GetProperty(nameof(BiblePublicationSchedule.FinishedDuration))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
