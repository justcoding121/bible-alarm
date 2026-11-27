#nullable enable

using Bible.Alarm.UITests.Fixtures;
using Bible.Alarm.UITests.Infrastructure;
using Bible.Alarm.UITests.Pages;
using OpenQA.Selenium.Appium.Android;

namespace Bible.Alarm.UITests.Tests.Alarm;

[Trait("UI", "Android")]
[Trait("Category", "Alarm")]
public sealed class AndroidScheduleEditorUiTests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture fixture;

    public AndroidScheduleEditorUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Music_category_hides_start_with_music()
    {
        Run(nameof(Music_category_hides_start_with_music), () =>
        {
            var schedule = OpenFirstSchedule();
            schedule.ScrollTo("MusicEnabledRow");
            Assert.True(fixture.IsShown("MusicEnabledRow"), "A Bible schedule shows Start with music.");
            schedule.TapCategory();
            var modal = new SelectionModal(fixture);
            modal.WaitUntilReady();
            modal.TapText("Music");
            Waits.Until(
                () => fixture.IsShown("SchedulePage") && !fixture.IsShown("MusicEnabledRow"),
                TimeSpan.FromSeconds(30),
                "Start with music hidden for Music");
            schedule.TapCancel();
            new HomePage(fixture).WaitUntilReady();
        });
    }

    [Fact]
    public void Music_category_stays_without_start_music_after_relaunch()
    {
        Run(nameof(Music_category_stays_without_start_music_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            var modal = new SelectionModal(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                var original = schedule.RowLabel("CategoryRow");
                Assert.False(string.IsNullOrWhiteSpace(original));
                Assert.NotEqual("Music", original);
                Assert.True(StartWithMusicShown(schedule), "A Bible schedule shows Start with music.");
                schedule.TapCategory();
                modal.WaitUntilReady();
                modal.TapExactInList("Music");
                Waits.Until(
                    () => !fixture.IsShown("SelectionList") && schedule.RowLabel("CategoryRow") == "Music",
                    TimeSpan.FromSeconds(90),
                    "music category");
                Assert.False(StartWithMusicShown(schedule));
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.Equal("Music", schedule.RowLabel("CategoryRow"));
                Assert.False(StartWithMusicShown(schedule));
                schedule.TapCategory();
                modal.WaitUntilReady();
                modal.TapExactInList(original);
                Waits.Until(
                    () => fixture.IsShown("SchedulePage") && schedule.RowLabel("CategoryRow") == original,
                    TimeSpan.FromSeconds(45),
                    "restored category");
                schedule.TapSave();
                home.WaitUntilReady();
            }
            finally
            {
                if (fixture.IsShown("SelectionList"))
                {
                    modal.Close();
                }

                if (fixture.IsShown("SchedulePage"))
                {
                    schedule.TapCancel();
                }
            }
        });
    }

    [Fact]
    public void Start_with_music_persists_after_relaunch()
    {
        Run(nameof(Start_with_music_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.False(MusicChoicesShown(schedule), "The sample schedule does not start with music.");
                SetStartWithMusic(schedule, enabled: true);
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.True(MusicChoicesShown(schedule));
                SetStartWithMusic(schedule, enabled: false);
                schedule.TapSave();
                home.WaitUntilReady();
            }
            finally
            {
                if (fixture.IsShown("SchedulePage"))
                {
                    schedule.TapCancel();
                }
            }
        });
    }

    [Fact]
    public void Indefinite_track_count_hides_the_finite_row()
    {
        Run(nameof(Indefinite_track_count_hides_the_finite_row), () =>
        {
            var schedule = OpenFirstSchedule();
            var labels = new SelectionModal(fixture);
            labels.TapText("Play indefinitely");
            if (!fixture.IsShown("NumberOfTracksRow"))
            {
                Waits.Until(
                    () => fixture.IsShown("NumberOfTracksRow"),
                    TimeSpan.FromSeconds(10),
                    "finite track row shown");
            }

            labels.TapText("Play indefinitely");
            Waits.Until(
                () => !fixture.IsShown("NumberOfTracksRow"),
                TimeSpan.FromSeconds(10),
                "finite track row hidden");
            schedule.TapCancel();
            new HomePage(fixture).WaitUntilReady();
        });
    }

    [Fact]
    public void Finite_track_count_persists_after_relaunch()
    {
        Run(nameof(Finite_track_count_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.False(FiniteTrackRowShown(schedule), "The sample schedule plays indefinitely.");
                SetFiniteTrackCount(schedule, finite: true);
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.True(FiniteTrackRowShown(schedule));
                SetFiniteTrackCount(schedule, finite: false);
                schedule.TapSave();
                home.WaitUntilReady();
            }
            finally
            {
                if (fixture.IsShown("SchedulePage"))
                {
                    schedule.TapCancel();
                }
            }
        });
    }

    [Fact]
    public void Chapter_count_persists_after_relaunch()
    {
        Run(nameof(Chapter_count_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            var modal = new SelectionModal(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                SetFiniteTrackCount(schedule, finite: true);
                schedule.TapRow("NumberOfTracksRow");
                modal.WaitUntilReady();
                modal.TapExactInList("2 chapters");
                Waits.Until(
                    () => !fixture.IsShown("SelectionList") && schedule.RowValue("NumberOfTracksRow") == "2",
                    TimeSpan.FromSeconds(20),
                    "two chapters");
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                schedule.ScrollTo("NumberOfTracksRow");
                Assert.Equal("2", schedule.RowValue("NumberOfTracksRow"));
                SetFiniteTrackCount(schedule, finite: false);
                schedule.TapSave();
                home.WaitUntilReady();
            }
            finally
            {
                if (fixture.IsShown("SelectionList"))
                {
                    modal.Close();
                }

                if (fixture.IsShown("SchedulePage"))
                {
                    schedule.TapCancel();
                }
            }
        });
    }

    [Fact]
    public void Cleared_weekday_stays_cleared_after_relaunch()
    {
        Run(nameof(Cleared_weekday_stays_cleared_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                SetAlarmEnabled(enabled: true);
                Assert.False(schedule.DayLooksDifferent("DayButton_Mon", "DayButton_Sun"));
                schedule.TapDay("DayButton_Mon");
                Waits.Until(
                    () => schedule.DayLooksDifferent("DayButton_Mon", "DayButton_Sun"),
                    TimeSpan.FromSeconds(10),
                    "Monday cleared");
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.True(schedule.DayLooksDifferent("DayButton_Mon", "DayButton_Sun"));
                schedule.TapDay("DayButton_Mon");
                Waits.Until(
                    () => !schedule.DayLooksDifferent("DayButton_Mon", "DayButton_Sun"),
                    TimeSpan.FromSeconds(10),
                    "Monday restored");
                SetAlarmEnabled(enabled: false);
                schedule.TapSave();
                home.WaitUntilReady();
            }
            finally
            {
                if (fixture.IsShown("SchedulePage"))
                {
                    schedule.TapCancel();
                }
            }
        });
    }

    [Fact]
    public void Language_search_filters_the_list()
    {
        Run(nameof(Language_search_filters_the_list), () =>
        {
            var schedule = OpenFirstSchedule();
            schedule.TapRow("BibleLanguageRow");
            var modal = new SelectionModal(fixture);
            modal.WaitUntilReady();
            modal.RevealText("English");
            Assert.True(modal.ShowsText("English"), "English is in the language list before search.");
            modal.TypeLanguageSearch("zzzqqq");
            Waits.Until(
                () => !modal.ShowsText("English"),
                TimeSpan.FromSeconds(15),
                "language search hid English");
            modal.Close();
            schedule.TapCancel();
            new HomePage(fixture).WaitUntilReady();
        });
    }

    [Fact]
    public void Last_schedule_cannot_be_deleted()
    {
        Run(nameof(Last_schedule_cannot_be_deleted), () =>
        {
            var home = new HomePage(fixture);
            home.WaitUntilReady();
            var name = home.FirstName();
            Assert.False(string.IsNullOrWhiteSpace(name));
            home.TapFirstCard();
            var schedule = new SchedulePage(fixture);
            schedule.WaitUntilReady();
            schedule.TapDelete();
            Thread.Sleep(1000);
            if (fixture.IsShown("SchedulePage"))
            {
                schedule.TapCancel();
            }

            home.WaitUntilReady();
            Assert.Contains(name, home.Names(), StringComparer.Ordinal);
            SwipeCardLeft();
            home.WaitUntilReady();
            Assert.Contains(name, home.Names(), StringComparer.Ordinal);
        });
    }

    [Fact]
    public void Battery_and_dnd_modal_opens_and_dismisses()
    {
        Run(nameof(Battery_and_dnd_modal_opens_and_dismisses), () =>
        {
            new HomePage(fixture).WaitUntilReady();
            Waits.Until(
                () => fixture.IsShown("AlarmSettingsFloatingButton"),
                TimeSpan.FromSeconds(20),
                "battery warning button");
            fixture.Tap(fixture.WaitForAccessibilityId("AlarmSettingsFloatingButton"));
            fixture.WaitForAccessibilityId("AndroidAlarmPermissionModal", TimeSpan.FromSeconds(15));
            Assert.True(
                fixture.IsShown("BatteryOptimizationButton") || fixture.IsShown("DndPermissionButton"),
                "The modal offers battery or Do Not Disturb.");
            fixture.Tap(fixture.WaitForAccessibilityId("BatteryOptimizationDismissButton"));
            Waits.Until(
                () => !fixture.IsShown("AndroidAlarmPermissionModal"),
                TimeSpan.FromSeconds(15),
                "battery modal dismissed");
        });
    }

    private void Run(string name, Action body)
    {
        fixture.Run(name, () =>
        {
            DismissToHome();
            body();
        });
    }

    private void DismissToHome()
    {
        DeviceControl.BringAndroidAppToFront(AndroidAppiumFixture.DefaultPackageName);
        if (!fixture.IsShown("HomePage")
            && !fixture.IsShown("SchedulePage")
            && !fixture.IsShown("SelectionList")
            && !fixture.IsShown("ModalCloseButton")
            && !fixture.IsShown("AndroidAlarmPermissionModal")
            && !fixture.IsShown("PlaybackModal")
            && !fixture.IsShown("MiniBar"))
        {
            new HomePage(fixture).WaitUntilReady();
            return;
        }

        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (fixture.IsShown("HomePage")
                && !fixture.IsShown("SchedulePage")
                && !fixture.IsShown("SelectionList")
                && !fixture.IsShown("AndroidAlarmPermissionModal")
                && !fixture.IsShown("PlaybackModal")
                && !fixture.IsShown("MiniBar"))
            {
                return;
            }

            if (fixture.IsShown("BatteryOptimizationDismissButton"))
            {
                fixture.Tap(fixture.WaitForAccessibilityId("BatteryOptimizationDismissButton"));
            }
            else if (fixture.IsShown("ModalCloseButton"))
            {
                fixture.Tap(fixture.WaitForAccessibilityId("ModalCloseButton"));
            }
            else if (fixture.IsShown("AlarmDismissButton") || fixture.IsShown("AlarmDismissButtonLandscape"))
            {
                new PlaybackPage(fixture).TapDismiss();
            }
            else if (fixture.IsShown("MiniBarStop") || fixture.IsShown("MiniBar"))
            {
                new MiniBarPage(fixture).TapStop();
            }
            else if (fixture.IsShown("SchedulePage"))
            {
                new SchedulePage(fixture).TapCancel();
            }
            else
            {
                DeviceControl.BringAndroidAppToFront(AndroidAppiumFixture.DefaultPackageName);
            }

            Thread.Sleep(400);
        }

        new HomePage(fixture).WaitUntilReady();
    }

    private bool StartWithMusicShown(SchedulePage schedule)
    {
        schedule.ScrollTo("AlwaysFromStartRow");
        if (fixture.IsShown("MusicEnabledRow"))
        {
            return true;
        }

        if (fixture.Driver is AndroidDriver driver)
        {
            driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
            {
                ["left"] = 100,
                ["top"] = 700,
                ["width"] = 800,
                ["height"] = 900,
                ["direction"] = "up",
                ["percent"] = 0.35
            });
        }

        return fixture.IsShown("MusicEnabledRow");
    }

    private bool MusicChoicesShown(SchedulePage schedule)
    {
        schedule.ScrollTo("MusicEnabledRow");
        if (fixture.IsShown("MusicLanguageRow"))
        {
            return true;
        }

        if (fixture.Driver is AndroidDriver driver)
        {
            driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
            {
                ["left"] = 100,
                ["top"] = 700,
                ["width"] = 800,
                ["height"] = 900,
                ["direction"] = "up",
                ["percent"] = 0.35
            });
        }

        return fixture.IsShown("MusicLanguageRow");
    }

    private void SetStartWithMusic(SchedulePage schedule, bool enabled)
    {
        if (MusicChoicesShown(schedule) == enabled)
        {
            return;
        }

        schedule.TapRow("MusicEnabledRow");
        Waits.Until(
            () => fixture.IsShown("MusicLanguageRow") == enabled,
            TimeSpan.FromSeconds(15),
            enabled ? "music choices" : "music choices hidden");
    }

    private bool FiniteTrackRowShown(SchedulePage schedule)
    {
        schedule.ScrollTo("IndefiniteRow");
        if (fixture.IsShown("NumberOfTracksRow"))
        {
            return true;
        }

        if (fixture.Driver is AndroidDriver driver)
        {
            driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
            {
                ["left"] = 100,
                ["top"] = 700,
                ["width"] = 800,
                ["height"] = 900,
                ["direction"] = "up",
                ["percent"] = 0.35
            });
        }

        return fixture.IsShown("NumberOfTracksRow");
    }

    private void SetAlarmEnabled(bool enabled)
    {
        var editor = new SelectionModal(fixture);
        if (editor.ShowsText("Require tap to start playback") == enabled)
        {
            return;
        }

        editor.TapText("Enable Alarm");
        Waits.Until(
            () => editor.ShowsText("Require tap to start playback") == enabled,
            TimeSpan.FromSeconds(10),
            enabled ? "alarm enabled" : "alarm disabled");
    }

    private void SetFiniteTrackCount(SchedulePage schedule, bool finite)
    {
        if (FiniteTrackRowShown(schedule) == finite)
        {
            return;
        }

        new SelectionModal(fixture).TapText("Play indefinitely");
        Waits.Until(
            () => fixture.IsShown("NumberOfTracksRow") == finite,
            TimeSpan.FromSeconds(15),
            finite ? "finite track row" : "indefinite track row");
    }

    private void Relaunch()
    {
        var driver = (AndroidDriver)(fixture.Driver ?? throw new InvalidOperationException("Driver not initialised."));
        driver.TerminateApp(AndroidAppiumFixture.DefaultPackageName);
        driver.ActivateApp(AndroidAppiumFixture.DefaultPackageName);
    }

    private SchedulePage OpenFirstSchedule()
    {
        var home = new HomePage(fixture);
        home.WaitUntilReady();
        home.TapFirstCard();
        var schedule = new SchedulePage(fixture);
        schedule.WaitUntilReady();
        return schedule;
    }

    private void SwipeCardLeft()
    {
        var driver = (AndroidDriver)(fixture.Driver ?? throw new InvalidOperationException("Driver not initialised."));
        var card = fixture.WaitForAccessibilityId("ScheduleCard");
        var location = card.Location;
        var size = card.Size;
        driver.ExecuteScript("mobile: swipeGesture", new Dictionary<string, object>
        {
            ["left"] = location.X,
            ["top"] = location.Y,
            ["width"] = Math.Max(size.Width, 1),
            ["height"] = Math.Max(size.Height, 1),
            ["direction"] = "left",
            ["percent"] = 0.75
        });
    }

}
