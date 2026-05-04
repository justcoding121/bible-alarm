#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Fluxor;

namespace Bible.Alarm.Tests;

public sealed class HomeViewModelFocusWarningHandlerTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    [Fact]
    public void ComputeShouldShow_is_false_on_non_iOS_targets()
    {
        var logger = TestLogging.CreateLogger();
        var state = new FakeApplicationState(new ApplicationState());
        var sut = new HomeViewModelFocusWarningHandler(logger, state);

        Assert.False(sut.ComputeShouldShow());
    }
}
