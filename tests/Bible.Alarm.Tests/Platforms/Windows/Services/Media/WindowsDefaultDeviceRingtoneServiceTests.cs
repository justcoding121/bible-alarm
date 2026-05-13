#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Media;
using Serilog;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsDefaultDeviceRingtoneServiceTests
{
    private static ILogger SilentLogger() =>
        new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public void Stop_without_prior_start_completes_without_throw()
    {
        var sut = new WindowsDefaultDeviceRingtoneService(SilentLogger());

        sut.Stop();
    }
}
