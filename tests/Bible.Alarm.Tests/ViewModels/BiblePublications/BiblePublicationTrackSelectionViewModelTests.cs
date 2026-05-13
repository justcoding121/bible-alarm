#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationTrackSelectionViewModelTests
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
    public void Ctor_builds_with_minimal_dependencies()
    {
        BiblePublicationTrackSelectionViewModel? sut = null;
        try
        {
            sut = new BiblePublicationTrackSelectionViewModel(
                TestLogging.CreateLogger(),
                new IdleCatalogMediaService(),
                null!,
                new FakeAppState(new ApplicationState()),
                new NopDispatcher());
            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
