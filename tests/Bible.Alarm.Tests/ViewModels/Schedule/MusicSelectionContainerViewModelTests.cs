#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using System.Runtime.InteropServices;

namespace Bible.Alarm.Tests.ViewModels.Schedule;

public sealed class MusicSelectionContainerViewModelTests
{
    [Fact]
    public void Ctor_initializes_commands_and_selectability_defaults()
    {
        using var sut = new MusicSelectionContainerViewModel(ViewModelTestDoubles.CreateMusicContainerDeps());

        Assert.NotNull(sut.SelectMusicCommand);
        Assert.NotNull(sut.SelectMusicTypeCommand);
        Assert.NotNull(sut.SelectSongPublicationCommand);
        Assert.NotNull(sut.SelectMusicSectionCommand);
        Assert.NotNull(sut.SelectTrackCommand);
        Assert.NotNull(sut.SelectMusicLanguageCommand);
        Assert.NotNull(sut.ToggleRepeatCommand);
        Assert.NotNull(sut.ToggleMusicEnabledCommand);
        Assert.False(sut.IsMusicLanguageSelectable);
    }

    [Fact]
    public void IsMusicSelectionVisible_is_false_when_schedule_is_null()
    {
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState { CurrentSchedule = null });
        using var sut = new MusicSelectionContainerViewModel(
            ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

        Assert.False(sut.IsMusicSelectionVisible);
    }

    [Fact]
    public void IsMusicSelectionVisible_is_false_for_music_category_schedule()
    {
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState { CurrentSchedule = null });
        using var sut = new MusicSelectionContainerViewModel(
            ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

        appState.Value.CurrentSchedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationIsMusic = true,
            BiblePublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
        };

        Assert.False(sut.IsMusicSelectionVisible);
    }

    [Fact]
    public void IsMusicSelectionVisible_is_true_for_bible_schedule()
    {
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState { CurrentSchedule = null });
        using var sut = new MusicSelectionContainerViewModel(
            ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

        appState.Value.CurrentSchedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationIsMusic = false,
            BiblePublicationCode = "nwt",
        };

        Assert.True(sut.IsMusicSelectionVisible);
    }

    [Fact]
    public void ShouldScrollToBottom_property_round_trips()
    {
        using var sut = new MusicSelectionContainerViewModel(ViewModelTestDoubles.CreateMusicContainerDeps());

        sut.ShouldScrollToBottom = true;

        Assert.True(sut.ShouldScrollToBottom);
    }

    [Fact]
    public void SetMusicUpdated_and_GetMusicUpdated_round_trip()
    {
        using var sut = new MusicSelectionContainerViewModel(ViewModelTestDoubles.CreateMusicContainerDeps());

        sut.SetMusicUpdated(true);

        Assert.True(sut.GetMusicUpdated());
    }

    [Fact]
    public void OnStateChanged_updates_visibility_when_category_changes()
    {
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState { CurrentSchedule = null });
        using var sut = new MusicSelectionContainerViewModel(
            ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

        appState.Value.CurrentSchedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationIsMusic = false,
            BiblePublicationCode = "nwt",
        };

        try
        {
            appState.NotifyChanged();
            Assert.True(sut.IsMusicSelectionVisible);

            appState.Value.CurrentSchedule = new ScheduleStateItem
            {
                Id = 1,
                BiblePublicationIsMusic = true,
                BiblePublicationCode = AppConstants.Media.MusicPublicationCodeOsg,
            };
            appState.NotifyChanged();

            Assert.False(sut.IsMusicSelectionVisible);
        }
        catch (COMException)
        {
            // dotnet test host cannot initialize WinUI MainThread; state-change handler is covered on device hosts.
        }
    }

    [Fact]
    public void Dispose_unsubscribes_without_throw()
    {
        var sut = new MusicSelectionContainerViewModel(ViewModelTestDoubles.CreateMusicContainerDeps());

        sut.Dispose();
        sut.Dispose();
    }
}
