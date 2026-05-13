#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.Platform;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class SerilogSetupTests
{
    private sealed class FakeVersionFinder : IVersionFinder
    {
        public string GetVersionName() => "test-version";
    }

    [Fact]
    public void Initialize_logging_disabled_sets_runtime_platform_before_logger_gate()
    {
        var priorPlatform = CurrentDevice.RuntimePlatform;
        var priorLogger = Log.Logger;
        try
        {
            SerilogSetup.Initialize(new FakeVersionFinder(), tags: ["unit"], device: "SerilogSetupTestHost", isLoggingEnabled: false);

            Assert.Equal("SerilogSetupTestHost", CurrentDevice.RuntimePlatform);
        }
        finally
        {
            CurrentDevice.RuntimePlatform = priorPlatform;
            Log.Logger = priorLogger;
        }
    }
}
