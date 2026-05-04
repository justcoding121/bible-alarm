#nullable enable

using Bible.Alarm.Stores.Actions;

namespace Bible.Alarm.Tests;

public sealed class BackActionTests
{
    private sealed class TrackDispose : IDisposable
    {
        public bool WasDisposed { get; private set; }

        public void Dispose() =>
            WasDisposed = true;
    }

    [Fact]
    public void CurrentViewModel_exposes_constructor_argument_without_disposing()
    {
        var vm = new TrackDispose();

        var action = new BackAction(vm);

        Assert.Same(vm, action.CurrentViewModel);
        Assert.False(vm.WasDisposed);
    }
}
