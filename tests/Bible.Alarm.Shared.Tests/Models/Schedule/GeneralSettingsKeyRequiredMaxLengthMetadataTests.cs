#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsKeyRequiredMaxLengthMetadataTests
{
    [Fact]
    public void Key_has_required_and_max_length_constraints()
    {
        var keyProp = typeof(GeneralSettings).GetProperty(nameof(GeneralSettings.Key))!;
        Assert.NotNull(keyProp.GetCustomAttribute<RequiredAttribute>());
        var ml = keyProp.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(ml);
        Assert.Equal(255, ml.Length);
    }
}
