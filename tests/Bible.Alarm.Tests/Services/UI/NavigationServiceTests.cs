#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class NavigationServiceTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        NavigationService sut = null!;
        try
        {
            sut = new NavigationService(
                null!,
                TestLogging.CreateLogger(),
                new NopDispatcher());

            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
