#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;

// MauiUi tests live in nested class below.
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class HomeViewModelNotificationPermissionHandlerTests
{
    private sealed class RecordingState(ApplicationState snapshot) : IState<ApplicationState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public ApplicationState Value => snapshot;
    }

    [Fact]
    public void UpdateVisibility_non_mobile_forces_hide_and_invokes_visibility_callback()
    {
        var sut = new HomeViewModelNotificationPermissionHandler(
            TestLogging.CreateLogger(),
            new RecordingState(new ApplicationState()));

        bool? lastVisible = null;

        sut.UpdateVisibility(
            v => lastVisible = v,
            _ => { },
            _ => { },
            () => false,
            () => true,
            () => { });

        Assert.False(lastVisible);
    }

    [Collection("MauiUi")]
    public sealed class MauiHomeViewModelNotificationPermissionHandlerTests(MauiUiFixture fixture)
    {
        [Fact]
        public void UpdateVisibility_notifies_margin_change_on_main_thread_when_maui_ready()
        {
            _ = fixture;
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            var sut = new HomeViewModelNotificationPermissionHandler(
                TestLogging.CreateLogger(),
                new RecordingState(new ApplicationState()));
            var marginNotified = new ManualResetEventSlim(false);

            sut.UpdateVisibility(
                _ => { },
                _ => { },
                _ => { },
                () => false,
                () => false,
                () => marginNotified.Set());

            Assert.True(marginNotified.Wait(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public void UpdateVisibility_returns_false_visibility_after_exception_when_setting_visible_throws_once()
    {
        var sut = new HomeViewModelNotificationPermissionHandler(
            TestLogging.CreateLogger(),
            new RecordingState(new ApplicationState()));

        var visibleCalls = 0;
        var bottomMargins = new List<double>();

        sut.UpdateVisibility(
            visible =>
            {
                visibleCalls++;
                if (visibleCalls == 1)
                {
                    throw new InvalidOperationException("simulate UI failure");
                }
            },
            m => bottomMargins.Add(m),
            _ => { },
            () => false,
            () => false,
            () => { });

        Assert.Equal(2, visibleCalls);
        Assert.NotEmpty(bottomMargins);
        Assert.All(bottomMargins, margin => Assert.Equal(0, margin));
    }
}
