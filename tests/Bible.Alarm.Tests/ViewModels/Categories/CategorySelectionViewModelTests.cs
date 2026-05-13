#nullable enable

using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Categories;
using Fluxor;

namespace Bible.Alarm.Tests.ViewModels.Categories;

public sealed class CategorySelectionViewModelTests
{
    private sealed class MutableApplicationState : IState<ApplicationState>
    {
        public MutableApplicationState(ApplicationState value) => Value = value;

        public ApplicationState Value { get; set; }

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : Fluxor.IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class StubCategoryService : ICategoryService
    {
        public Task<List<Category>> GetAllCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Category>());

        public void Dispose()
        {
        }
    }

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => $"name-{categoryCode}";
    }

    [Fact]
    public void ShowCancelButton_follows_IsBusy_changes()
    {
        var fluxorState = new MutableApplicationState(new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), null));
        using var sut = new CategorySelectionViewModel(
            new StubCategoryService(),
            new StubCategoryNameService(),
            new UnusedNavigationServiceStub(),
            new RecordingDispatcher(),
            fluxorState);

        Assert.True(sut.ShowCancelButton);

        sut.IsBusy = false;

        Assert.False(sut.ShowCancelButton);
    }
}
