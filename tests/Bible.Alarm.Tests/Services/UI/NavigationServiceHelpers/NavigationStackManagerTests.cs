#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Microsoft.Maui.Controls;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class NavigationStackManagerTests
{
    private sealed class FakeNavigation : INavigation
    {
        public List<Page> Modals { get; } = [];
        public List<Page> Stack { get; } = [];

        public int PopModalAsyncCalls { get; private set; }

        IReadOnlyList<Page> INavigation.ModalStack => Modals;

        IReadOnlyList<Page> INavigation.NavigationStack => Stack;

        public void InsertPageBefore(Page page, Page before) => throw new NotImplementedException();

        public Task<Page> PopAsync() => throw new NotImplementedException();

        public Task<Page> PopAsync(bool animated) => throw new NotImplementedException();

        public Task<Page> PopModalAsync()
        {
            PopModalAsyncCalls++;
            var p = Modals[^1];
            Modals.RemoveAt(Modals.Count - 1);
            return Task.FromResult(p);
        }

        public Task<Page> PopModalAsync(bool animated) => PopModalAsync();

        public Task PopToRootAsync() => throw new NotImplementedException();

        public Task PopToRootAsync(bool animated) => throw new NotImplementedException();

        public void RemovePage(Page page) => throw new NotImplementedException();

        public Task PushAsync(Page page) => throw new NotImplementedException();

        public Task PushAsync(Page page, bool animated) => throw new NotImplementedException();

        public Task PushModalAsync(Page page) => throw new NotImplementedException();

        public Task PushModalAsync(Page page, bool animated) => throw new NotImplementedException();
    }

    [Fact]
    public async Task PopModalAsync_does_not_pop_when_modal_stack_empty()
    {
        var nav = new FakeNavigation();

        await NavigationStackManager.PopModalAsync(nav);

        Assert.Equal(0, nav.PopModalAsyncCalls);
    }

    [Fact]
    public void IsAndroidNavControllerError_returns_false_on_windows_test_host()
    {
        var ex = new InvalidOperationException(
            "NavController's back stack an error /FragmentActivity...");

        Assert.False(NavigationStackManager.IsAndroidNavControllerError(ex));
    }
}
