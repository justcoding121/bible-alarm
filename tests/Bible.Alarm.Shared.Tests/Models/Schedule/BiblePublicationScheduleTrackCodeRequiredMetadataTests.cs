#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationScheduleTrackCodeRequiredMetadataTests
{
    [Fact]
    public void TrackCode_is_marked_required()
    {
        var p = typeof(BiblePublicationSchedule).GetProperty(nameof(BiblePublicationSchedule.TrackCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
