#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class HomeNavigationHandlerTests
{
    private sealed class FakeNavigation : INavigation
    {
        public List<Page> Stack { get; } = [];

        IReadOnlyList<Page> INavigation.ModalStack => Array.Empty<Page>();

        IReadOnlyList<Page> INavigation.NavigationStack => Stack;

        public int PopAsyncCalls { get; private set; }

        public int PushAsyncCalls { get; private set; }

        public Page? LastPushedPage { get; private set; }

        public void InsertPageBefore(Page page, Page before) => throw new NotImplementedException();

        public Task<Page> PopAsync()
        {
            PopAsyncCalls++;
            var p = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            return Task.FromResult(p);
        }

        public Task<Page> PopAsync(bool animated) => PopAsync();

        public Task<Page> PopModalAsync() => throw new NotImplementedException();

        public Task<Page> PopModalAsync(bool animated) => throw new NotImplementedException();

        public Task PopToRootAsync() => throw new NotImplementedException();

        public Task PopToRootAsync(bool animated) => throw new NotImplementedException();

        public void RemovePage(Page page) => throw new NotImplementedException();

        public Task PushAsync(Page page)
        {
            PushAsyncCalls++;
            LastPushedPage = page;
            Stack.Add(page);
            return Task.CompletedTask;
        }

        public Task PushAsync(Page page, bool animated) => PushAsync(page);

        public Task PushModalAsync(Page page) => throw new NotImplementedException();

        public Task PushModalAsync(Page page, bool animated) => throw new NotImplementedException();
    }

    private sealed class ThrowingStackNavigation : INavigation
    {
        IReadOnlyList<Page> INavigation.ModalStack => Array.Empty<Page>();

        IReadOnlyList<Page> INavigation.NavigationStack =>
            throw new InvalidOperationException("navigation stack unavailable");

        public void InsertPageBefore(Page page, Page before) => throw new NotImplementedException();

        public Task<Page> PopAsync() => throw new NotImplementedException();

        public Task<Page> PopAsync(bool animated) => throw new NotImplementedException();

        public Task<Page> PopModalAsync() => throw new NotImplementedException();

        public Task<Page> PopModalAsync(bool animated) => throw new NotImplementedException();

        public Task PopToRootAsync() => throw new NotImplementedException();

        public Task PopToRootAsync(bool animated) => throw new NotImplementedException();

        public void RemovePage(Page page) => throw new NotImplementedException();

        public Task PushAsync(Page page) => throw new NotImplementedException();

        public Task PushAsync(Page page, bool animated) => throw new NotImplementedException();

        public Task PushModalAsync(Page page) => throw new NotImplementedException();

        public Task PushModalAsync(Page page, bool animated) => throw new NotImplementedException();
    }

    [Fact]
    public void Ctor_accepts_logger_and_service_provider()
    {
        var sut = new HomeNavigationHandler(TestLogging.CreateLogger(), null!);
        Assert.NotNull(sut);
    }

    [Fact]
    public void GetCurrentHomePage_returns_null_when_stack_is_empty()
    {
        var sut = new HomeNavigationHandler(TestLogging.CreateLogger(), null!);
        var nav = new FakeNavigation();

        Assert.Null(sut.GetCurrentHomePage(nav));
    }

    [Fact]
    public void GetCurrentHomePage_returns_null_when_stack_access_throws()
    {
        var sut = new HomeNavigationHandler(TestLogging.CreateLogger(), null!);

        Assert.Null(sut.GetCurrentHomePage(new ThrowingStackNavigation()));
    }
}

[Collection("MauiUi")]
public sealed class HomeNavigationHandlerMauiTests(MauiUiFixture fixture)
{
    private sealed class FakeNavigation : INavigation
    {
        public List<Page> Stack { get; } = [];

        IReadOnlyList<Page> INavigation.ModalStack => Array.Empty<Page>();

        IReadOnlyList<Page> INavigation.NavigationStack => Stack;

        public int PopAsyncCalls { get; private set; }

        public int PushAsyncCalls { get; private set; }

        public void InsertPageBefore(Page page, Page before) => throw new NotImplementedException();

        public Task<Page> PopAsync()
        {
            PopAsyncCalls++;
            var p = Stack[^1];
            Stack.RemoveAt(Stack.Count - 1);
            return Task.FromResult(p);
        }

        public Task<Page> PopAsync(bool animated) => PopAsync();

        public Task<Page> PopModalAsync() => throw new NotImplementedException();

        public Task<Page> PopModalAsync(bool animated) => throw new NotImplementedException();

        public Task PopToRootAsync() => throw new NotImplementedException();

        public Task PopToRootAsync(bool animated) => throw new NotImplementedException();

        public void RemovePage(Page page) => throw new NotImplementedException();

        public Task PushAsync(Page page)
        {
            PushAsyncCalls++;
            Stack.Add(page);
            return Task.CompletedTask;
        }

        public Task PushAsync(Page page, bool animated) => PushAsync(page);

        public Task PushModalAsync(Page page) => throw new NotImplementedException();

        public Task PushModalAsync(Page page, bool animated) => throw new NotImplementedException();
    }

    private sealed class HomeServiceProvider(Home home) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(Home) ? home : null;
    }

    [Fact]
    public async Task NavigateToHomeAsync_returns_early_when_already_on_home()
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

        if (!await MauiUiTestHostHelper.TryInvokeOnMainThreadAsync(() => nav.Stack.Add(homePage)))
        {
            return;
        }

        var sut = new HomeNavigationHandler(TestLogging.CreateLogger(), null!);
        await sut.NavigateToHomeAsync(nav);

        Assert.Equal(0, nav.PopAsyncCalls);
        Assert.Equal(0, nav.PushAsyncCalls);
        Assert.Single(nav.Stack);
    }

    [Fact]
    public async Task NavigateToHomeAsync_pops_pages_above_existing_home()
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
                nav.Stack.Add(homePage);
                nav.Stack.Add(new ContentPage());
                nav.Stack.Add(new ContentPage());
            }))
        {
            return;
        }

        var sut = new HomeNavigationHandler(TestLogging.CreateLogger(), null!);
        await sut.NavigateToHomeAsync(nav);

        Assert.Equal(2, nav.PopAsyncCalls);
        Assert.Single(nav.Stack);
    }

    [Fact]
    public async Task NavigateToHomeAsync_pushes_new_home_when_none_exists()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var nav = new FakeNavigation();
        Home? homePage = null;
        using var homeVm = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());
        homePage = await MauiUiTestHostHelper.TryCreateHomePageAsync(homeVm);
        if (homePage is null)
        {
            return;
        }

        if (!await MauiUiTestHostHelper.TryInvokeOnMainThreadAsync(() =>
            {
                nav.Stack.Add(new ContentPage());
                nav.Stack.Add(new ContentPage());
            }))
        {
            return;
        }

        var sut = new HomeNavigationHandler(TestLogging.CreateLogger(), new HomeServiceProvider(homePage!));
        await sut.NavigateToHomeAsync(nav);

        Assert.Equal(1, nav.PushAsyncCalls);
        Assert.Equal(2, nav.Stack.Count);
        Assert.IsType<Home>(nav.Stack[^1]);
    }
}
