#nullable enable

using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class SystemControlsHandlerTests
{
    private static Task InstantDelay(TimeSpan _) => Task.CompletedTask;

    [Fact]
    public void HandleNextButton_runs_callback_on_main_thread_scheduler()
    {
        using var done = new ManualResetEventSlim(false);
        var mainThread = new SyncMainThreadScheduler();
        var sut = new SystemControlsHandler(TestLogging.CreateLogger(), mainThread, InstantDelay);

        sut.HandleNextButton(async () =>
        {
            await Task.CompletedTask;
            done.Set();
        });

        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void HandlePlayButton_runs_callback()
    {
        using var done = new ManualResetEventSlim(false);
        var sut = new SystemControlsHandler(
            TestLogging.CreateLogger(),
            new SyncMainThreadScheduler(),
            InstantDelay);

        sut.HandlePlayButton(async () =>
        {
            await Task.CompletedTask;
            done.Set();
        });

        Assert.True(done.Wait(TimeSpan.FromSeconds(5)));
    }
}
