#nullable enable

using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

public sealed class GeneralSettingsModelBibleAlarmTests
{
    [Fact]
    public void Model_properties_can_be_read_and_written()
    {
        var model = new GeneralSettings
        {
            Id = 7,
            Key = "theme",
            Value = "dark",
        };

        Assert.Equal(7, model.Id);
        Assert.Equal("theme", model.Key);
        Assert.Equal("dark", model.Value);
    }
}
