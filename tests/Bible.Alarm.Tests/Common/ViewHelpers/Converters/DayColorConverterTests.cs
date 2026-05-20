#nullable enable

using System.Globalization;
using AutoMapper;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.ViewHelpers.Converters;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Fluxor;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Graphics;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class DayColorConverterTests
{
    private static readonly CultureInfo Cul = CultureInfo.InvariantCulture;

    private static Color EnabledText() =>
        ThemeColors.Day.EnabledText.Get(ThemeColors.GetCurrentTheme());

    private static Color MutedText() =>
        ThemeColors.Day.CalendarMutedText.Get(ThemeColors.GetCurrentTheme());

    [Fact]
    public void Convert_null_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, sut.Convert(null!, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void Convert_weekDays_day_match_uses_enabled_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(EnabledText(), sut.Convert(WeekDays.Monday, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void Convert_weekDays_day_off_uses_muted_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), sut.Convert(WeekDays.Monday, typeof(Color), WeekDays.Tuesday, Cul));
    }

    [Fact]
    public void Convert_parameter_string_parses_weekday()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), sut.Convert(WeekDays.Monday, typeof(Color), "Tuesday", Cul));
    }

    [Fact]
    public void Convert_parameter_branches_cover_parse_day_parameter()
    {
        var sut = new DayColorConverter();

        Assert.Equal(EnabledText(), sut.Convert(WeekDays.Monday, typeof(Color), null!, Cul));
        Assert.Equal(EnabledText(), sut.Convert(WeekDays.Monday, typeof(Color), WeekDays.Monday, Cul));
        Assert.Equal(MutedText(), sut.Convert(WeekDays.Monday, typeof(Color), "Tuesday", Cul));
        Assert.Equal(EnabledText(), sut.Convert(WeekDays.Monday, typeof(Color), 99, Cul));
    }

    [Fact]
    public void Convert_unrecognized_type_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, sut.Convert(12, typeof(Color), WeekDays.Monday, Cul));
    }

    [Fact]
    public void MultiConvert_too_few_or_null_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, sut.Convert((object[]?)null!, typeof(Color), WeekDays.Monday, Cul));
        Assert.Equal(Colors.White, Multi(sut, Array.Empty<object>()));
        Assert.Equal(Colors.White, Multi(sut, new object[] { WeekDays.Monday }));
    }

    [Fact]
    public void MultiConvert_wrong_element_types_returns_white()
    {
        var sut = new DayColorConverter();

        Assert.Equal(Colors.White, Multi(sut, new object[] { "x", true }));
        Assert.Equal(Colors.White, Multi(sut, new object[] { WeekDays.Monday, "x" }));
    }

    [Fact]
    public void MultiConvert_schedule_on_day_match_uses_enabled_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(EnabledText(), Multi(sut, new object[] { WeekDays.Monday, true }, WeekDays.Monday));
    }

    [Fact]
    public void MultiConvert_schedule_on_day_off_uses_muted_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), Multi(sut, new object[] { WeekDays.Monday, true }, WeekDays.Tuesday));
    }

    [Fact]
    public void MultiConvert_schedule_off_always_muted_text()
    {
        var sut = new DayColorConverter();

        Assert.Equal(MutedText(), Multi(sut, new object[] { WeekDays.Monday, false }, WeekDays.Monday));
        Assert.Equal(MutedText(), Multi(sut, new object[] { WeekDays.Monday, false }, WeekDays.Tuesday));
    }

    [Fact]
    public void ConvertBack_throws()
    {
        var sut = new DayColorConverter();

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.White, typeof(Color), null!, Cul));

        Assert.Throws<NotImplementedException>(() =>
            sut.ConvertBack(Colors.White, [typeof(WeekDays), typeof(bool)], null!, Cul));
    }

    [Fact]
    public void Convert_schedule_list_item_enabled_day_match_uses_enabled_text()
    {
        if (!TryCreateScheduleListItemViewModel(isEnabled: true, daysOfWeek: WeekDays.Monday, out var vm))
        {
            return;
        }

        using (vm)
        {
            var sut = new DayColorConverter();

            Assert.Equal(EnabledText(), sut.Convert(vm, typeof(Color), WeekDays.Monday, Cul));
        }
    }

    [Fact]
    public void Convert_schedule_list_item_enabled_day_off_uses_muted_text()
    {
        if (!TryCreateScheduleListItemViewModel(isEnabled: true, daysOfWeek: WeekDays.Monday, out var vm))
        {
            return;
        }

        using (vm)
        {
            var sut = new DayColorConverter();

            Assert.Equal(MutedText(), sut.Convert(vm, typeof(Color), WeekDays.Tuesday, Cul));
        }
    }

    [Fact]
    public void Convert_schedule_list_item_disabled_always_uses_muted_text()
    {
        if (!TryCreateScheduleListItemViewModel(isEnabled: false, daysOfWeek: WeekDays.Monday, out var vm))
        {
            return;
        }

        using (vm)
        {
            var sut = new DayColorConverter();

            Assert.Equal(MutedText(), sut.Convert(vm, typeof(Color), WeekDays.Monday, Cul));
            Assert.Equal(MutedText(), sut.Convert(vm, typeof(Color), WeekDays.Tuesday, Cul));
        }
    }

    private static bool TryCreateScheduleListItemViewModel(bool isEnabled, WeekDays daysOfWeek, out ScheduleListItemViewModel vm)
    {
        vm = null!;
        if (!TryBootstrapMauiApp())
        {
            return false;
        }

        vm = CreateScheduleListItemViewModel(isEnabled, daysOfWeek);
        return true;
    }

    private static bool TryBootstrapMauiApp()
    {
        MauiUiTestBootstrap.TryInitialize();
        return MauiUiTestBootstrap.IsReady;
    }

    private static ScheduleListItemViewModel CreateScheduleListItemViewModel(bool isEnabled, WeekDays daysOfWeek)
    {
#pragma warning disable CS0067
        var dispatcher = new NopDispatcher();
#pragma warning restore CS0067

        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        var mapper = cfg.CreateMapper();

        var deps = new ScheduleListItemViewModelDeps(
            TestLogging.CreateLogger(),
            PlaybackService: null!,
            StopPlaybackService: null!,
            ScheduleStateService: null!,
            ApplicationState: new FakeApplicationState(new ApplicationState()),
            PlaybackState: new FakePlaybackState(new PlaybackState()),
            Dispatcher: dispatcher,
            Mapper: mapper,
            CategoryNameService: null!);

        var vm = new ScheduleListItemViewModel(deps);
        vm.InitializeFromSchedule(new AlarmSchedule
        {
            Id = 1,
            Name = "Test",
            IsEnabled = isEnabled,
            DaysOfWeek = daysOfWeek,
            Hour = 7,
            Minute = 0,
        });
        return vm;
    }

    private sealed class NopDispatcher : IDispatcher
    {
#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action)
        {
        }
    }

    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class FakePlaybackState(PlaybackState value) : IState<PlaybackState>
    {
        public PlaybackState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private static object Multi(DayColorConverter sut, object[] values, object? parameter = null) =>
        sut.Convert(values, typeof(Color), parameter!, Cul);
}
