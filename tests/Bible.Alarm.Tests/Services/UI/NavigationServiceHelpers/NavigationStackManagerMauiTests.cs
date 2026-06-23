#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class NavigationStackManagerMauiTests(MauiUiFixture fixture)
{
    private sealed class DisposableModalPage : ContentPage, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeNavigation : INavigation
    {
        public List<Page> Modals { get; } = [];
        public List<Page> Stack { get; } = [];

        public int PopModalAsyncCalls { get; private set; }

        public int PopAsyncCalls { get; private set; }

        IReadOnlyList<Page> INavigation.ModalStack => Modals;

        IReadOnlyList<Page> INavigation.NavigationStack => Stack;

        public void InsertPageBefore(Page page, Page before) => throw new NotImplementedException();

        public Task<Page> PopAsync()
        {
            PopAsyncCalls++;
            var p = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            return Task.FromResult(p);
        }

        public Task<Page> PopAsync(bool animated) => PopAsync();

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
    public async Task PopModalAsync_pops_top_modal_when_stack_has_items()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var nav = new FakeNavigation();
        nav.Modals.Add(new ContentPage());
        nav.Modals.Add(new ContentPage());

        await NavigationStackManager.PopModalAsync(nav);

        Assert.Equal(1, nav.PopModalAsyncCalls);
        Assert.Single(nav.Modals);
    }

    [Fact]
    public async Task PopModalAsync_disposes_modal_when_it_implements_IDisposable()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var nav = new FakeNavigation();
        var modal = new DisposableModalPage();
        nav.Modals.Add(modal);

        await NavigationStackManager.PopModalAsync(nav);

        Assert.True(modal.Disposed);
        Assert.Empty(nav.Modals);
    }

    [Fact]
    public async Task PopAllModalsAsync_pops_every_modal()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var nav = new FakeNavigation();
        nav.Modals.Add(new ContentPage());
        nav.Modals.Add(new ContentPage());

        await NavigationStackManager.PopAllModalsAsync(nav);

        Assert.Empty(nav.Modals);
        Assert.Equal(2, nav.PopModalAsyncCalls);
    }

    [Fact]
    public async Task PopAsync_does_not_pop_when_navigation_stack_has_one_page()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var nav = new FakeNavigation();
        nav.Stack.Add(new ContentPage());

        await NavigationStackManager.PopAsync(nav);

        Assert.Equal(0, nav.PopAsyncCalls);
        Assert.Single(nav.Stack);
    }

    [Fact]
    public async Task PopAsync_refuses_to_pop_home_page()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var nav = new FakeNavigation();
        using var homeVm = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());
        var homePage = await MauiUiTestHostHelper.TryCreateHomePageAsync(homeVm);
        if (homePage is null)
        {
            return;
        }

        if (!await MauiUiTestHostHelper.TryInvokeOnMainThreadAsync(() =>
            {
                nav.Stack.Add(new ContentPage());
                nav.Stack.Add(homePage);
            }))
        {
            return;
        }

        await NavigationStackManager.PopAsync(nav);

        Assert.Equal(0, nav.PopAsyncCalls);
        Assert.Equal(2, nav.Stack.Count);
    }

    [Fact]
    public async Task PopAsync_pops_non_home_page_and_disposes_when_IDisposable()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var nav = new FakeNavigation();
        var disposable = new DisposableModalPage();
        nav.Stack.Add(new ContentPage());
        nav.Stack.Add(disposable);

        await NavigationStackManager.PopAsync(nav);

        Assert.Equal(1, nav.PopAsyncCalls);
        Assert.Single(nav.Stack);
        Assert.True(disposable.Disposed);
    }
}
