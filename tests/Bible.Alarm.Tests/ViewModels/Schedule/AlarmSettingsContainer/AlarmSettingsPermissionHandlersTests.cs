#nullable enable

using System.Collections.Generic;
using Bible.Alarm.ViewModels.Schedule.AlarmSettingsContainer;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class AlarmSettingsPermissionHandlersTests
{
    private static ILogger SilentLogger() =>
        new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public void HandlePermissionGranted_returns_early_without_touching_toggle_when_not_waiting_for_permission_response()
    {
        var setOnCalls = 0;
        var updatingCalls = new List<bool>();
        var waitingCalls = new List<bool>();

        AlarmSettingsPermissionHandlers.HandlePermissionGranted(
            isWaitingForPermissionResponse: false,
            SilentLogger(),
            () => setOnCalls++,
            updatingCalls.Add,
            waitingCalls.Add);

        Assert.Equal(0, setOnCalls);
        Assert.Empty(updatingCalls);
        Assert.Empty(waitingCalls);
    }

    [Fact]
    public void HandlePermissionGranted_sets_toggle_on_when_waiting_then_finally_resets_guard_flags()
    {
        var setOnCalls = 0;
        var updatingCalls = new List<bool>();
        var waitingCalls = new List<bool>();

        AlarmSettingsPermissionHandlers.HandlePermissionGranted(
            isWaitingForPermissionResponse: true,
            SilentLogger(),
            () => setOnCalls++,
            updatingCalls.Add,
            waitingCalls.Add);

        Assert.Equal(1, setOnCalls);
        Assert.Equal(new[] { true, false }, updatingCalls);
        Assert.Single(waitingCalls);
        Assert.False(waitingCalls[0]);
    }
}
