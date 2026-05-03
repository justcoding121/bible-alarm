#nullable enable

using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsTests
{
    [Fact]
    public void Keys_and_values_assign_and_allow_null_value()
    {
        var sut = new GeneralSettings
        {
            Id = 501,
            Key = "settings.test-flag",
            Value = "enabled",
        };

        Assert.Equal(501, sut.Id);
        Assert.Equal("settings.test-flag", sut.Key);
        Assert.Equal("enabled", sut.Value);

        sut.Value = null;
        Assert.Null(sut.Value);

        sut.Key = "minimal";
        Assert.Equal("minimal", sut.Key);
    }
}
