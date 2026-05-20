#nullable enable

using System.Globalization;
using Bible.Alarm.Common;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Bible.Alarm.Tests;

public sealed class SelectableTextColorConverterTests
{
    static SelectableTextColorConverterTests()
    {
        TryBootstrapMauiApp();
    }

    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_uses_primary_resource_when_available_otherwise_purple()
    {
        var sut = new SelectableTextColorConverter();

        var result = sut.Convert(false, typeof(Color), null!, Cul);

        var color = Assert.IsType<Color>(result);
        if (Application.Current?.Resources.TryGetValue("PrimaryColor", out var primaryObj) is true &&
            primaryObj is Color primary)
        {
            Assert.Equal(primary, color);
        }
        else
        {
            Assert.Equal(Colors.Purple, color);
        }
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new SelectableTextColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.Purple, typeof(Color), null!, Cul));
    }

    [Fact]
    public void Convert_reads_primary_color_from_application_resources_when_host_available()
    {
        if (!TryBootstrapMauiApp())
        {
            return;
        }

        var expected = Color.FromArgb("#AABBCC");
        Application.Current!.Resources["PrimaryColor"] = expected;

        var sut = new SelectableTextColorConverter();
        var result = Assert.IsType<Color>(sut.Convert(true, typeof(Color), null!, Cul));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_returns_purple_fallback_when_primary_resource_missing()
    {
        if (!TryBootstrapMauiApp())
        {
            return;
        }

        Application.Current!.Resources.Remove("PrimaryColor");

        var sut = new SelectableTextColorConverter();
        var result = Assert.IsType<Color>(sut.Convert(false, typeof(Color), null!, Cul));

        Assert.Equal(Colors.Purple, result);
    }

    private static bool TryBootstrapMauiApp()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return true;
        }

        try
        {
            MauiAppHolder.CreateAndStore();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
