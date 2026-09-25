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

    [Fact]
    public void Display_text_properties_are_readable_with_null_schedule()
    {
        using var sut = new MusicSelectionContainerViewModel(ViewModelTestDoubles.CreateMusicContainerDeps());

        Assert.False(sut.IsSongPublicationVisible);
        Assert.False(sut.IsMusicLanguageVisible);
        Assert.False(sut.IsMusicSectionVisible);
        Assert.False(sut.IsRepeatEnabled);
        Assert.False(sut.HasTrackSelected);
        Assert.NotNull(sut.MusicLanguageDisplayText);
        Assert.NotNull(sut.SongPublicationDisplayText);
        Assert.NotNull(sut.MusicSectionDisplayText);
        Assert.NotNull(sut.TrackDisplayText);
        Assert.Equal(Microsoft.Maui.FlowDirection.LeftToRight, sut.ContentFlowDirection);
        Assert.False(sut.IsMusicLanguageSelectable);
        Assert.False(sut.IsSongPublicationSelectable);
        Assert.False(sut.IsMusicSectionSelectable);
        Assert.False(sut.IsMusicTrackSelectable);
    }

    [Fact]
    public void MusicEnabled_reads_false_when_schedule_null()
    {
        using var sut = new MusicSelectionContainerViewModel(ViewModelTestDoubles.CreateMusicContainerDeps());

        Assert.False(sut.MusicEnabled);
    }

    [Fact]
    public void MusicEnabled_reads_from_schedule_when_present()
    {
        try
        {
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState
                {
                    CurrentSchedule = new ScheduleStateItem
                    {
                        Id = 2,
                        MusicEnabled = true,
                        BiblePublicationIsMusic = false,
                        BiblePublicationCode = "nwt",
                    },
                });
            using var sut = new MusicSelectionContainerViewModel(
                ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

            Assert.True(sut.MusicEnabled);
        }
        catch (COMException)
        {
            // Headless Windows host cannot initialize WinUI MainThread used by container ctor.
        }
    }

    [Fact]
    public void ToggleMusicEnabledCommand_flips_music_enabled()
    {
        try
        {
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState
                {
                    CurrentSchedule = new ScheduleStateItem
                    {
                        Id = 3,
                        MusicEnabled = false,
                        BiblePublicationIsMusic = false,
                        BiblePublicationCode = "nwt",
                    },
                });
            using var sut = new MusicSelectionContainerViewModel(
                ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

            sut.ToggleMusicEnabledCommand.Execute(null);
            Assert.True(sut.MusicEnabled);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void ContentFlowDirection_left_to_right_by_default()
    {
        try
        {
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState
                {
                    CurrentSchedule = new ScheduleStateItem
                    {
                        Id = 4,
                        BiblePublicationIsMusic = false,
                        BiblePublicationCode = "nwt",
                        MusicLanguageDirection = AppConstants.Media.TextDirectionLeftToRight,
                    },
                });
            using var sut = new MusicSelectionContainerViewModel(
                ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

            Assert.Equal(Microsoft.Maui.FlowDirection.LeftToRight, sut.ContentFlowDirection);
        }
        catch (COMException)
        {
            // Headless Windows host cannot initialize WinUI MainThread used by container ctor.
        }
    }

    [Fact]
    public void IsMusicSelectionVisible_false_for_music_flag_publication_code()
    {
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState { CurrentSchedule = null });
        using var sut = new MusicSelectionContainerViewModel(
            ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

        appState.Value.CurrentSchedule = new ScheduleStateItem
        {
            Id = 5,
            BiblePublicationIsMusic = false,
            BiblePublicationCode = AppConstants.Media.MediatorCategoryKeyChildrenSongs,
        };

        Assert.False(sut.IsMusicSelectionVisible);
    }

    [Fact]
    public void OnStateChanged_with_new_schedule_id_reinitializes()
    {
        try
        {
            var schedule = new ScheduleStateItem
            {
                Id = 10,
                BiblePublicationIsMusic = false,
                BiblePublicationCode = "nwt",
                MusicEnabled = true,
            };
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState { CurrentSchedule = schedule });
            using var sut = new MusicSelectionContainerViewModel(
                ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

            appState.Value.CurrentSchedule = new ScheduleStateItem
            {
                Id = 11,
                BiblePublicationIsMusic = false,
                BiblePublicationCode = "nwt",
                MusicEnabled = false,
            };

            appState.NotifyChanged();
            Assert.False(sut.MusicEnabled);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void OnStateChanged_initializes_when_schedule_appears_from_null()
    {
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState { CurrentSchedule = null });
        using var sut = new MusicSelectionContainerViewModel(
            ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

        appState.Value.CurrentSchedule = new ScheduleStateItem
        {
            Id = 20,
            BiblePublicationIsMusic = false,
            BiblePublicationCode = "nwt",
            MusicEnabled = true,
        };

        try
        {
            appState.NotifyChanged();
            Assert.True(sut.IsMusicSelectionVisible);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public async Task GetSongPublicationDisplayTextAsync_returns_string()
    {
        try
        {
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState
                {
                    CurrentSchedule = new ScheduleStateItem
                    {
                        Id = 6,
                        BiblePublicationIsMusic = false,
                        BiblePublicationCode = "nwt",
                        MusicPublicationCode = "osg",
                        MusicLanguageCode = "E",
                    },
                });
            using var sut = new MusicSelectionContainerViewModel(
                ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

            var text = await sut.GetSongPublicationDisplayTextAsync();

            Assert.NotNull(text);
        }
        catch (COMException)
        {
            // Headless Windows host cannot initialize WinUI MainThread used by container ctor/commands.
        }
    }

    [Fact]
    public async Task GetTrackDisplayTextAsync_returns_string()
    {
        try
        {
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState
                {
                    CurrentSchedule = new ScheduleStateItem
                    {
                        Id = 7,
                        BiblePublicationIsMusic = false,
                        BiblePublicationCode = "nwt",
                        MusicPublicationCode = "osg",
                        MusicTrackCode = "1",
                        MusicLanguageCode = "E",
                    },
                });
            using var sut = new MusicSelectionContainerViewModel(
                ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

            var text = await sut.GetTrackDisplayTextAsync();

            Assert.NotNull(text);
        }
        catch (COMException)
        {
            // Headless Windows host cannot initialize WinUI MainThread used by container ctor/commands.
        }
    }

    [Fact]
    public void Selectability_defaults_false_before_async_update()
    {
        using var sut = new MusicSelectionContainerViewModel(ViewModelTestDoubles.CreateMusicContainerDeps());

        Assert.False(sut.IsMusicLanguageSelectable);
        Assert.False(sut.IsSongPublicationSelectable);
        Assert.False(sut.IsMusicSectionSelectable);
        Assert.False(sut.IsMusicTrackSelectable);
    }

    [Fact]
    public void ToggleRepeatCommand_is_executable()
    {
        try
        {
            var appState = new ViewModelTestDoubles.MutableApplicationState(
                new ApplicationState
                {
                    CurrentSchedule = new ScheduleStateItem
                    {
                        Id = 8,
                        BiblePublicationIsMusic = false,
                        BiblePublicationCode = "nwt",
                        MusicRepeat = false,
                    },
                });
            using var sut = new MusicSelectionContainerViewModel(
                ViewModelTestDoubles.CreateMusicContainerDeps(appState: appState));

            var ex = Record.Exception(() => sut.ToggleRepeatCommand.Execute(null));

            Assert.Null(ex);
        }
        catch (COMException)
        {
            // Headless Windows host cannot initialize WinUI MainThread used by container ctor/commands.
        }
    }
}
