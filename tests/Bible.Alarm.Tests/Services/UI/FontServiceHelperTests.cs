#nullable enable

using System.Reflection;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class FontServiceHelperTests : IDisposable
{
    public FontServiceHelperTests() => ResetStaticFontService();

    public void Dispose() => ResetStaticFontService();

    private static void ResetStaticFontService()
    {
        var field = typeof(FontServiceHelper).GetField("fontService", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(null, null);
    }

    [Fact]
    public void Initialize_routes_properties_to_service_instance()
    {
        using var fake = new FakeFontService { StandardFontSize = 10, HeaderFontSize = 20, AlarmBellIconFontSize = 99 };

        FontServiceHelper.Initialize(fake);

        Assert.Equal(10, FontServiceHelper.StandardFontSize);
        Assert.Equal(20, FontServiceHelper.HeaderFontSize);
        Assert.Equal(99, FontServiceHelper.AlarmBellIconFontSize);
    }

    [Fact]
    public void Initialize_exposes_all_font_size_properties()
    {
        using var fake = new FakeFontService
        {
            StandardFontSize = 10,
            HeaderFontSize = 20,
            ButtonFontSize = 21,
            SmallFontSize = 22,
            SmallMediumFontSize = 23,
            MediumFontSize = 24,
            LargeFontSize = 25,
            TitleFontSize = 26,
            AlarmTimeFontSize = 27,
            AlarmMeridianFontSize = 28,
            AlarmBellIconFontSize = 99,
        };

        FontServiceHelper.Initialize(fake);

        Assert.Equal(10, FontServiceHelper.StandardFontSize);
        Assert.Equal(20, FontServiceHelper.HeaderFontSize);
        Assert.Equal(21, FontServiceHelper.ButtonFontSize);
        Assert.Equal(22, FontServiceHelper.SmallFontSize);
        Assert.Equal(23, FontServiceHelper.SmallMediumFontSize);
        Assert.Equal(24, FontServiceHelper.MediumFontSize);
        Assert.Equal(25, FontServiceHelper.LargeFontSize);
        Assert.Equal(26, FontServiceHelper.TitleFontSize);
        Assert.Equal(27, FontServiceHelper.AlarmTimeFontSize);
        Assert.Equal(28, FontServiceHelper.AlarmMeridianFontSize);
        Assert.Equal(99, FontServiceHelper.AlarmBellIconFontSize);
    }

    private sealed class FakeFontService : IFontService
    {
        public double StandardFontSize { get; init; }
        public double HeaderFontSize { get; init; }
        public double ButtonFontSize { get; init; } = 1;
        public double SmallFontSize { get; init; } = 2;
        public double SmallMediumFontSize { get; init; } = 3;
        public double MediumFontSize { get; init; } = 4;
        public double LargeFontSize { get; init; } = 5;
        public double TitleFontSize { get; init; } = 6;
        public double AlarmTimeFontSize { get; init; } = 7;
        public double AlarmMeridianFontSize { get; init; } = 8;
        public double AlarmBellIconFontSize { get; init; }
        public double IconSmallFontSize { get; init; } = 9;
        public double IconStandardFontSize { get; init; } = 10;
        public double IconLargeFontSize { get; init; } = 11;
        public double IconSmallContainerSize { get; init; } = 12;
        public double IconStandardContainerSize { get; init; } = 13;
        public double IconLargeContainerSize { get; init; } = 14;
        public double ContentWidthSmall { get; init; } = 15;
        public double ContentWidthMedium { get; init; } = 16;
        public double ContentWidthLarge { get; init; } = 17;
        public double SpinnerContainerSize { get; init; } = 18;
        public double SpinnerFontSize { get; init; } = 19;

        public double GetScaledFontSize(double baseSizeInPoints) => baseSizeInPoints;

        public void Dispose()
        {
        }
    }

    [Collection("MauiUi")]
    public sealed class MauiFontServiceHelperTests(MauiUiFixture fixture)
    {
        [Fact]
        public void GetFontService_creates_fallback_instance_when_not_initialized()
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            _ = fixture;

            var field = typeof(FontServiceHelper).GetField("fontService", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(null, null);

            var method = typeof(FontServiceHelper).GetMethod(
                "GetFontService",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var service = Assert.IsAssignableFrom<IFontService>(method!.Invoke(null, null));
            Assert.NotNull(field.GetValue(null));
            Assert.True(service.StandardFontSize > 0);
        }
    }
}
