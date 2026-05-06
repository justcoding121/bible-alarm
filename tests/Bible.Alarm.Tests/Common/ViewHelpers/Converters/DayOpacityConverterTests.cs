#nullable enable

using System.Globalization;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Bible.Alarm.ViewModels;

namespace Bible.Alarm.Tests;

public sealed class DayOpacityConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_null_returns_full_opacity()
    {
        var sut = new DayOpacityConverter();

        Assert.Equal(1.0, sut.Convert(null!, typeof(double), null!, Cul));
    }

    [Fact]
    public void Convert_non_schedule_returns_full_opacity()
    {
        var sut = new DayOpacityConverter();

        Assert.Equal(1.0, sut.Convert("x", typeof(double), null!, Cul));
    }

    [Fact]
    public void Convert_schedule_disabled_uses_dimmed_opacity()
    {
        var sut = new DayOpacityConverter();
        var vm = new ScheduleListItemViewModel(new ScheduleListItemViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!));

        Assert.False(vm.IsEnabled);
        Assert.Equal(0.6, sut.Convert(vm, typeof(double), null!, Cul));
    }

    [Fact]
    public void Convert_schedule_enabled_uses_full_opacity()
    {
        var sut = new DayOpacityConverter();
        var vm = new ScheduleListItemViewModel(new ScheduleListItemViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!));
        vm.IsEnabled = true;

        Assert.True(vm.IsEnabled);
        Assert.Equal(1.0, sut.Convert(vm, typeof(double), null!, Cul));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new DayOpacityConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(1.0, typeof(object), null!, Cul));
    }
}
