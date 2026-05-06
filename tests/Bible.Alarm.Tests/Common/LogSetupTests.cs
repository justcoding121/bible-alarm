#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.Platform;

namespace Bible.Alarm.Tests;

public sealed class LogSetupTests
{
    private sealed class FakeVersionFinder : IVersionFinder
    {
        public string GetVersionName() => "test-version";
    }

    [Fact]
    public void Initialize_logging_disabled_sets_runtime_platform_only()
    {
        var priorPlatform = CurrentDevice.RuntimePlatform;
        try
        {
            LogSetup.Initialize(new FakeVersionFinder(), tags: ["unit"], device: "TestHost", isLoggingEnabled: false);

            Assert.Equal("TestHost", CurrentDevice.RuntimePlatform);
        }
        finally
        {
            CurrentDevice.RuntimePlatform = priorPlatform;
        }
    }
}
