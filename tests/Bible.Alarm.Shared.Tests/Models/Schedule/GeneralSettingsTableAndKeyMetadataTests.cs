#nullable enable

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsTableAndKeyMetadataTests
{
    [Fact]
    public void Type_maps_to_GeneralSettings_with_bounded_key_length()
    {
        var table = typeof(GeneralSettings).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("GeneralSettings", table.Name);

        var keyLen = typeof(GeneralSettings).GetProperty(nameof(GeneralSettings.Key))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(255, keyLen.Length);
    }
}
