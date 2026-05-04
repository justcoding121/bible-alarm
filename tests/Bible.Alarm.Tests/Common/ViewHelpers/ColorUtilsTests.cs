#nullable enable

using Bible.Alarm.Common.ViewHelpers;
using Microsoft.Maui.Graphics;

namespace Bible.Alarm.Tests;

public sealed class ColorUtilsTests
{
    [Fact]
    public void ToHexString_emits_argb_hex_uppercase()
    {
        var c = Color.FromRgba(10, 20, 30, 255);

        Assert.Equal("#FF0A141E", ColorUtils.ToHexString(c));
    }
}
