#nullable enable

using Bible.Alarm.UITests.Fixtures;
using Bible.Alarm.UITests.Infrastructure;
using Bible.Alarm.UITests.Pages;
using Bible.Alarm.UITests.Visual;

namespace Bible.Alarm.UITests.Tests.Alarm;

[Trait("UI", "Android")]
[Trait("Category", "Alarm")]
public sealed class AndroidAlarmFlowUiTests : IClassFixture<AndroidAppiumFixture>
{
    private readonly AndroidAppiumFixture fixture;
    private readonly IVisualCheck visual = VisualCheckFactory.Create("android");

    public AndroidAlarmFlowUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Home_shows_the_default_schedule()
    {
        fixture.Run(nameof(Home_shows_the_default_schedule), () =>
        {
            var home = new HomePage(fixture);
            home.WaitUntilReady();
            Assert.False(string.IsNullOrWhiteSpace(home.FirstName()));
            visual.CheckAsync(fixture.Driver!, "home", UiFlow.DynamicMasks).GetAwaiter().GetResult();
        });
    }

    [Fact]
    public void Create_edit_cancel_and_delete_a_schedule()
    {
        fixture.Run(nameof(Create_edit_cancel_and_delete_a_schedule), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            home.WaitUntilReady();
            _ = home.FirstName();
            home.TapAdd();
            schedule.WaitUntilReady();
            schedule.SetName("Morning reading");
            schedule.TapDay("DayButton_Mon");
            schedule.TapSave();
            home.WaitUntilReady();
            // The seeded schedule stays first. The created schedule is appended.
            Waits.Until(
                () => home.Names().Any(name => name.Contains("Morning", StringComparison.OrdinalIgnoreCase)),
                TimeSpan.FromSeconds(20),
                "created schedule name");

            home.TapCardNamed("Morning");
            schedule.WaitUntilReady();
            schedule.SetName("Morning reading edited");
            schedule.TapCancel();
            home.WaitUntilReady();
            Waits.Until(
                () => home.Names().Any(name => name.Contains("Morning reading", StringComparison.OrdinalIgnoreCase))
                    && home.Names().All(name => !name.Contains("edited", StringComparison.OrdinalIgnoreCase)),
                TimeSpan.FromSeconds(20),
                "cancelled edit discarded");

            home.TapCardNamed("Morning");
            schedule.WaitUntilReady();
            schedule.SetName("Morning reading edited");
            schedule.TapSave();
            home.WaitUntilReady();
            Relaunch();
            home.WaitUntilReady();
            Waits.Until(
                () => home.Names().Any(name => name.Contains("edited", StringComparison.OrdinalIgnoreCase)),
                TimeSpan.FromSeconds(20),
                "edited schedule name");

            home.TapCardNamed("edited");
            schedule.WaitUntilReady();
            schedule.TapDelete();
            home.WaitUntilReady();
            Waits.Until(
                () => home.Names().All(name => !name.Contains("edited", StringComparison.OrdinalIgnoreCase)),
                TimeSpan.FromSeconds(20),
                "deleted schedule name");
        });
    }

    [Fact]
    public void Schedule_time_persists_after_relaunch()
    {
        fixture.Run(nameof(Schedule_time_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                schedule.SetTime(7, 30);
                schedule.TapSave();
                home.WaitUntilReady();
                Waits.Until(
                    () => home.FirstTime().Contains("07:30", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(15),
                    "updated time");
                Relaunch();
                home.WaitUntilReady();
                Assert.Contains("07:30", home.FirstTime(), StringComparison.Ordinal);
                home.TapFirstCard();
                schedule.WaitUntilReady();
                schedule.SetTime(6, 0);
                schedule.TapSave();
                home.WaitUntilReady();
            }
            finally
            {
                schedule.DismissTimeDialog();
                if (fixture.IsShown("SchedulePage"))
                {
                    schedule.TapCancel();
                }
            }
        });
    }

    [Fact]
    public void Bible_section_change_persists_after_relaunch()
    {
        fixture.Run(nameof(Bible_section_change_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            var modal = new SelectionModal(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                var original = schedule.RowLabel("BibleSectionRow");
                schedule.TapRow("BibleSectionRow");
                modal.WaitUntilReady();
                var chosen = modal.TapDifferentVisibleText(original);
                Waits.Until(
                    () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BibleSectionRow") == chosen,
                    TimeSpan.FromSeconds(20),
                    "selected book");
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.Equal(chosen, schedule.RowLabel("BibleSectionRow"));
                schedule.TapRow("BibleSectionRow");
                modal.WaitUntilReady();
                modal.TapExactInList(original);
                Waits.Until(
                    () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BibleSectionRow") == original,
                    TimeSpan.FromSeconds(20),
                    "restored book");
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
    public void Bible_chapter_change_persists_after_relaunch()
    {
        fixture.Run(nameof(Bible_chapter_change_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            var modal = new SelectionModal(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                var originalBook = schedule.RowLabel("BibleSectionRow");
                var original = schedule.RowLabel("BibleTrackRow");
                schedule.TapRow("BibleTrackRow");
                if (!ChapterListOpened())
                {
                    schedule.TapRow("BibleSectionRow");
                    modal.WaitUntilReady();
                    modal.TapExactInList("Matthew");
                    Waits.Until(
                        () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BibleSectionRow") == "Matthew",
                        TimeSpan.FromSeconds(20),
                        "book with chapters");
                    original = schedule.RowLabel("BibleTrackRow");
                    schedule.TapRow("BibleTrackRow");
                }

                modal.WaitUntilReady();
                var chosen = modal.TapDifferentVisibleText(original);
                Waits.Until(
                    () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BibleTrackRow") == chosen,
                    TimeSpan.FromSeconds(20),
                    "selected chapter");
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.Equal(chosen, schedule.RowLabel("BibleTrackRow"));
                schedule.TapRow("BibleTrackRow");
                modal.WaitUntilReady();
                modal.TapExactInList(original);
                Waits.Until(
                    () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BibleTrackRow") == original,
                    TimeSpan.FromSeconds(20),
                    "restored chapter");
                if (schedule.RowLabel("BibleSectionRow") != originalBook)
                {
                    schedule.TapRow("BibleSectionRow");
                    modal.WaitUntilReady();
                    modal.TapExactInList(originalBook);
                    Waits.Until(
                        () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BibleSectionRow") == originalBook,
                        TimeSpan.FromSeconds(20),
                        "restored book");
                }

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
    public void Bible_publication_change_persists_after_relaunch()
    {
        fixture.Run(nameof(Bible_publication_change_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            var modal = new SelectionModal(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                var original = schedule.RowLabel("BiblePublicationRow");
                schedule.TapRow("BiblePublicationRow");
                modal.WaitUntilReady();
                var chosen = modal.TapDifferentVisibleText(original);
                Waits.Until(
                    () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BiblePublicationRow") == chosen,
                    TimeSpan.FromSeconds(45),
                    "selected publication");
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.Equal(chosen, schedule.RowLabel("BiblePublicationRow"));
                schedule.TapRow("BiblePublicationRow");
                modal.WaitUntilReady();
                modal.TapExactInList(original);
                Waits.Until(
                    () => fixture.IsShown("SchedulePage") && schedule.RowLabel("BiblePublicationRow") == original,
                    TimeSpan.FromSeconds(45),
                    "restored publication");
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
    public void Bible_language_change_persists_after_relaunch()
    {
        fixture.Run(nameof(Bible_language_change_persists_after_relaunch), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            var modal = new SelectionModal(fixture);
            try
            {
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                var original = schedule.RowLabel("BibleLanguageRow");
                schedule.TapRow("BibleLanguageRow");
                modal.WaitUntilReady();
                var chosen = modal.TapDifferentVisibleText(original);
                Waits.Until(
                    () => !fixture.IsShown("SelectionList")
                        && fixture.IsShown("SchedulePage")
                        && schedule.RowLabel("BibleLanguageRow") == chosen,
                    TimeSpan.FromSeconds(90),
                    "selected language");
                schedule.TapSave();
                home.WaitUntilReady();
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.Equal(chosen, schedule.RowLabel("BibleLanguageRow"));
                schedule.TapRow("BibleLanguageRow");
                modal.WaitUntilReady();
                modal.TapExactInList(original);
                Waits.Until(
                    () => !fixture.IsShown("SelectionList")
                        && fixture.IsShown("SchedulePage")
                        && schedule.RowLabel("BibleLanguageRow") == original,
                    TimeSpan.FromSeconds(90),
                    "restored language");
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
    public void Category_row_opens_the_selection_list()
    {
        fixture.Run(nameof(Category_row_opens_the_selection_list), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            var modal = new SelectionModal(fixture);
            home.WaitUntilReady();
            home.TapFirstCard();
            schedule.WaitUntilReady();
            schedule.TapCategory();
            modal.WaitUntilReady();
            visual.CheckAsync(fixture.Driver!, "category-modal", UiFlow.DynamicMasks).GetAwaiter().GetResult();
            modal.Close();
            schedule.TapCancel();
        });
    }

    [Fact]
    public void Toggle_persists_after_relaunch_is_visible_on_the_card()
    {
        fixture.Run(nameof(Toggle_persists_after_relaunch_is_visible_on_the_card), () =>
        {
            var home = new HomePage(fixture);
            var schedule = new SchedulePage(fixture);
            try
            {
                home.WaitUntilReady();
                SetAlarmEnabled(home, schedule, enabled: true);
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.True(new SelectionModal(fixture).ShowsText("Require tap to start playback"));
                schedule.TapCancel();
                home.WaitUntilReady();
                SetAlarmEnabled(home, schedule, enabled: false);
                Relaunch();
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                Assert.False(new SelectionModal(fixture).ShowsText("Require tap to start playback"));
                schedule.TapCancel();
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

    private bool ChapterListOpened()
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < deadline)
        {
            if (fixture.IsShown("SelectionList"))
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private void SetAlarmEnabled(HomePage home, SchedulePage schedule, bool enabled)
    {
        home.TapFirstCard();
        schedule.WaitUntilReady();
        var editor = new SelectionModal(fixture);
        var shown = editor.ShowsText("Require tap to start playback");
        if (shown != enabled)
        {
            editor.TapText("Enable Alarm");
        }

        schedule.TapSave();
        home.WaitUntilReady();
    }

    private void Relaunch()
    {
        var driver = (OpenQA.Selenium.Appium.Android.AndroidDriver)fixture.Driver!;
        driver.TerminateApp(AndroidAppiumFixture.DefaultPackageName);
        driver.ActivateApp(AndroidAppiumFixture.DefaultPackageName);
    }

    [Fact]
    [Trait("Category", "Visual")]
    public void Dark_theme_home_snapshot()
    {
        fixture.Run(nameof(Dark_theme_home_snapshot), () =>
        {
            try
            {
                Bible.Alarm.UITests.Infrastructure.DeviceControl.AdbOrEmpty("shell cmd uimode night yes");
                Environment.SetEnvironmentVariable("UI_THEME", "dark");
                var home = new HomePage(fixture);
                home.WaitUntilReady();
                visual.CheckAsync(fixture.Driver!, "home-dark", UiFlow.DynamicMasks).GetAwaiter().GetResult();
            }
            finally
            {
                Bible.Alarm.UITests.Infrastructure.DeviceControl.AdbOrEmpty("shell cmd uimode night no");
                Environment.SetEnvironmentVariable("UI_THEME", "light");
            }
        });
    }

    [Fact]
    [Trait("Category", "Visual")]
    public void Dark_theme_covers_editor_modal_and_playback()
    {
        fixture.Run(nameof(Dark_theme_covers_editor_modal_and_playback), () =>
        {
            try
            {
                DeviceControl.AdbOrEmpty("shell cmd uimode night yes");
                DeviceControl.AdbOrEmpty("shell settings put system accelerometer_rotation 0");
                DeviceControl.AdbOrEmpty("shell settings put system user_rotation 0");
                Environment.SetEnvironmentVariable("UI_THEME", "dark");
                var home = new HomePage(fixture);
                var schedule = new SchedulePage(fixture);
                var playback = new PlaybackPage(fixture);
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                visual.CheckAsync(fixture.Driver!, "schedule-dark", UiFlow.DynamicMasks).GetAwaiter().GetResult();
                schedule.TapCategory();
                var modal = new SelectionModal(fixture);
                modal.WaitUntilReady();
                visual.CheckAsync(fixture.Driver!, "category-dark", UiFlow.DynamicMasks).GetAwaiter().GetResult();
                modal.Close();
                schedule.TapCancel();
                home.WaitUntilReady();
                home.TapFirstPlay();
                playback.WaitUntilReady();
                visual.CheckAsync(fixture.Driver!, "playback-dark", UiFlow.DynamicMasks).GetAwaiter().GetResult();
                DeviceControl.AdbOrEmpty("shell settings put system user_rotation 1");
                fixture.WaitForAccessibilityId("AlarmDismissButtonLandscape", TimeSpan.FromSeconds(20));
                visual.CheckAsync(fixture.Driver!, "playback-landscape-dark", UiFlow.DynamicMasks).GetAwaiter().GetResult();
                fixture.WaitForAccessibilityId("AlarmDismissButtonLandscape").Click();
                home.WaitUntilReady();
            }
            finally
            {
                DeviceControl.AdbOrEmpty("shell settings put system user_rotation 0");
                DeviceControl.AdbOrEmpty("shell cmd uimode night no");
                Environment.SetEnvironmentVariable("UI_THEME", "light");
            }
        });
    }
}

[Trait("UI", "Android")]
[Trait("Category", "Alarm")]
public sealed class AndroidPermissionUiTests : IClassFixture<AndroidDeniedPermissionsFixture>
{
    private readonly AndroidDeniedPermissionsFixture fixture;

    public AndroidPermissionUiTests(AndroidDeniedPermissionsFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Notification_permission_modal_can_be_dismissed()
    {
        fixture.Run(nameof(Notification_permission_modal_can_be_dismissed), () =>
        {
            new HomePage(fixture).WaitUntilReady();
            try
            {
                fixture.WaitForAccessibilityId("NotificationPermissionFloatingButton", TimeSpan.FromSeconds(8)).Click();
                fixture.WaitForAccessibilityId("NotificationPermissionModal");
                fixture.WaitForAccessibilityId("NotificationPermissionDismissButton").Click();
            }
            catch (OpenQA.Selenium.WebDriverTimeoutException)
            {
                // The image already granted notifications. The modal is conditional.
            }
        });
    }
}

public sealed class AndroidDeniedPermissionsFixture : AndroidAppiumFixture
{
    protected override bool GrantPermissions => false;
}

[Trait("UI", "Android")]
[Trait("Category", "Alarm")]
public sealed class AndroidNotificationFabUiTests : IClassFixture<AndroidAppiumFixture>
{
    private const string PostNotifications = "android.permission.POST_NOTIFICATIONS";
    private readonly AndroidAppiumFixture fixture;

    public AndroidNotificationFabUiTests(AndroidAppiumFixture fixture) => this.fixture = fixture;

    [Fact]
    public void Notification_fab_shows_when_denied_and_hides_when_granted()
    {
        fixture.Run(nameof(Notification_fab_shows_when_denied_and_hides_when_granted), () =>
        {
            var packageName = AndroidAppiumFixture.DefaultPackageName;
            try
            {
                var home = new HomePage(fixture);
                var schedule = new SchedulePage(fixture);
                DeviceControl.AdbOrEmpty($"shell pm grant {packageName} {PostNotifications}");
                home.WaitUntilReady();
                home.TapFirstCard();
                schedule.WaitUntilReady();
                var editor = new SelectionModal(fixture);
                if (!editor.ShowsText("Require tap to start playback"))
                {
                    editor.TapText("Enable Alarm");
                }

                editor.TapText("Require tap to start playback");
                schedule.TapSave();
                home.WaitUntilReady();

                DeviceControl.AdbOrEmpty($"shell pm revoke {packageName} {PostNotifications}");
                Relaunch(packageName);
                home.WaitUntilReady();
                fixture.Tap(fixture.WaitForAccessibilityId("NotificationPermissionFloatingButton", TimeSpan.FromSeconds(20)));
                fixture.WaitForAccessibilityId("NotificationPermissionModal");
                fixture.Tap(fixture.WaitForAccessibilityId("NotificationPermissionDismissButton"));
                home.WaitUntilReady();

                DeviceControl.AdbOrEmpty($"shell pm grant {packageName} {PostNotifications}");
                Relaunch(packageName);
                home.WaitUntilReady();
                Waits.Until(
                    () => !fixture.IsShown("NotificationPermissionFloatingButton"),
                    TimeSpan.FromSeconds(15),
                    "notification button hidden");
            }
            finally
            {
                DeviceControl.AdbOrEmpty($"shell pm grant {packageName} {PostNotifications}");
            }
        });
    }

    private void Relaunch(string packageName)
    {
        var driver = (OpenQA.Selenium.Appium.Android.AndroidDriver)fixture.Driver!;
        driver.TerminateApp(packageName);
        driver.ActivateApp(packageName);
    }
}
