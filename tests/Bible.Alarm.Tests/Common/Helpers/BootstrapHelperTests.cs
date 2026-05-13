#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Tests.Support;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class BootstrapHelperTests
{
    [Fact]
    public void IsBootstrapCompleted_is_deterministic_for_consecutive_reads()
    {
        var prior = Log.Logger;
        try
        {
            Log.Logger = TestLogging.CreateLogger();
            var first = BootstrapHelper.IsBootstrapCompleted();
            var second = BootstrapHelper.IsBootstrapCompleted();
            Assert.Equal(first, second);
        }
        finally
        {
            Log.Logger = prior;
        }
    }
}
