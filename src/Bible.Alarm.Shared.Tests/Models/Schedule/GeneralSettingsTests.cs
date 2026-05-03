#nullable enable

using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsTests
{
    [Fact]
    public void Default_ctor_uses_clr_defaults_for_id_and_binding_fields()
    {
        var sut = new GeneralSettings();

        Assert.Equal(0, sut.Id);
        Assert.Equal(string.Empty, sut.Key);
        Assert.Null(sut.Value);

        sut.Key = "wake.window";
        sut.Value = string.Empty;

        Assert.Equal("wake.window", sut.Key);
        Assert.Equal(string.Empty, sut.Value);

        sut.Value = null;
        Assert.Null(sut.Value);
    }

    [Fact]
    public void Key_accepts_exactly_max_annotation_length()
    {
        var key255 = new string('k', 255);

        var sut = new GeneralSettings { Id = 1, Key = key255 };

        Assert.Equal(255, sut.Key.Length);
        Assert.All(sut.Key, c => Assert.Equal('k', c));
    }

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
