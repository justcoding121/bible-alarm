#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionViewModelTests
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

    private sealed class FakeAppState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public void Ctor_registers_progress_messenger()
    {
        MusicSectionSelectionViewModel? sut = null;
        try
        {
            sut = new MusicSectionSelectionViewModel(
                TestLogging.CreateLogger(),
                new IdleCatalogMediaService(),
                new FakeAppState(new ApplicationState()),
                new NopDispatcher(),
                null!);
            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
