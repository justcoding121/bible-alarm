#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class ExceptionHandlingServiceTests
{
    [Fact]
    public void Dispose_without_setup_is_idempotent()
    {
        var sut = new ExceptionHandlingService(TestLogging.CreateLogger());

        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void Setup_then_dispose_unregisters_handlers_without_throw()
    {
        var sut = new ExceptionHandlingService(TestLogging.CreateLogger());

        sut.SetupGlobalExceptionHandlers();
        sut.Dispose();
    }
}
