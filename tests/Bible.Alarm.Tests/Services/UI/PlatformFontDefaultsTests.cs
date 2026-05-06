#nullable enable

using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Tests;

public sealed class PlatformFontDefaultsTests
{
    [Fact]
    public void Windows_defaults_use_Fluent_typography_scale()
    {
        var w = PlatformFontDefaults.Windows;

        Assert.Equal(14.0, w.StandardSize);
        Assert.Equal(18.0, w.HeaderSize);
        Assert.Equal(12.0, w.SmallSize);
        Assert.Equal(13.0, w.SmallMediumSize);
        Assert.Equal(14.0, w.MediumSize);
        Assert.Equal(18.0, w.LargeSize);
        Assert.Equal(24.0, w.TitleSize);
        Assert.Equal(32.0, w.AlarmTimeSize);
        Assert.Equal(18.0, w.AlarmMeridianSize);
    }

    [Fact]
    public void iOS_defaults_use_HIG_sizes()
    {
        var i = PlatformFontDefaults.iOS;

        Assert.Equal(17.0, i.StandardSize);
        Assert.Equal(22.0, i.HeaderSize);
        Assert.Equal(36.0, i.AlarmTimeSize);
    }

    [Fact]
    public void Android_defaults_use_Material_scale()
    {
        var a = PlatformFontDefaults.Android;

        Assert.Equal(16.0, a.StandardSize);
        Assert.Equal(20.0, a.HeaderSize);
        Assert.Equal(28.0, a.AlarmTimeSize);
    }
}
