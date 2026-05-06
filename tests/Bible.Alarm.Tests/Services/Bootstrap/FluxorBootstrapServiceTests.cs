#nullable enable

using Bible.Alarm.Services.Bootstrap;
using Bible.Alarm.Tests.Support;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class FluxorBootstrapServiceTests
{
    [Fact]
    public async Task InitializeAsync_no_op_when_store_missing()
    {
        var prior = Log.Logger;
        try
        {
            Log.Logger = TestLogging.CreateLogger();
            var sut = new FluxorBootstrapService(store: null);

            await sut.InitializeAsync();
        }
        finally
        {
            Log.Logger = prior;
        }
    }
}
