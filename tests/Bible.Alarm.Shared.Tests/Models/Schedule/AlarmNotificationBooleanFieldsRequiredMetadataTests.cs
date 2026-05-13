#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmNotificationBooleanFieldsRequiredMetadataTests
{
    [Fact]
    public void Sent_and_Fired_flags_are_marked_required_for_EF_contract()
    {
        Assert.NotNull(
            typeof(AlarmNotification).GetProperty(nameof(AlarmNotification.Sent))!
                .GetCustomAttribute<RequiredAttribute>());
        Assert.NotNull(
            typeof(AlarmNotification).GetProperty(nameof(AlarmNotification.Fired))!
                .GetCustomAttribute<RequiredAttribute>());
    }
}
