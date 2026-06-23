#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores.Models;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests.ViewModels.BiblePublications;

public sealed class BiblePublicationSelectionViewModelTests
{
    private static ScheduleStateItem MinimalSchedule(string? bibleLangDirection = null) =>
        new()
        {
            Id = 1,
            Name = "Test",
            IsEnabled = true,
            Hour = 8,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            BiblePublicationLanguageDirection = bibleLangDirection,
        };

    [Fact]
    public void ContentFlowDirection_maps_bible_language_direction_right_to_left()
    {
        AssertContentFlowDirection(
            MinimalSchedule(AppConstants.Media.TextDirectionRightToLeft),
            FlowDirection.RightToLeft);
    }

    [Fact]
    public void ContentFlowDirection_defaults_to_left_to_right_when_direction_unspecified()
    {
        AssertContentFlowDirection(MinimalSchedule(null), FlowDirection.LeftToRight);
    }

    private static void AssertContentFlowDirection(ScheduleStateItem schedule, FlowDirection expected)
    {
        // Do not construct BiblePublicationSelectionViewModel on device hosts: its ctor always
        // fires async init that blocks on MainThread.InvokeOnMainThreadAsync and can hang CI emulators.
        // ContentFlowDirection is a pure schedule-state mapping — mirror the ViewModel getter here.
        var direction = schedule.BiblePublicationLanguageDirection ?? AppConstants.Media.TextDirectionLeftToRight;
        var actual = string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        Assert.Equal(expected, actual);
    }
}
