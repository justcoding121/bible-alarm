#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.Views;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class NavigationServiceTests
{
    private sealed class SyncNavigationUiThreadInvoker : INavigationUiThreadInvoker
    {
        public Task InvokeOnUiThreadAsync(Func<Task> work) => work();
    }

#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private static NavigationService CreateSut(IServiceProvider? serviceProvider = null) =>
        new(
            serviceProvider ?? new ServiceCollection().BuildServiceProvider(),
            TestLogging.CreateLogger(),
            new NopDispatcher(),
            new SyncNavigationUiThreadInvoker());

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        NavigationService sut = null!;
        try
        {
            sut = CreateSut();

            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void GetCurrentPage_returns_null_when_navigation_unavailable()
    {
        NavigationService sut = null!;
        try
        {
            sut = CreateSut();

            Assert.Null(sut.GetCurrentPage());
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void GetCurrentHomePage_returns_null_when_navigation_unavailable()
    {
        NavigationService sut = null!;
        try
        {
            sut = CreateSut();

            Assert.Null(sut.GetCurrentHomePage());
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void IsPlaybackModalOnScreen_returns_false_when_navigation_unavailable()
    {
        NavigationService sut = null!;
        try
        {
            sut = CreateSut();

            Assert.False(sut.IsPlaybackModalOnScreen());
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void ClearCache_does_not_throw_when_navigation_unavailable()
    {
        NavigationService sut = null!;
        try
        {
            sut = CreateSut();

            var ex = Record.Exception(() => sut.ClearCache());

            Assert.Null(ex);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void PopAllModalsAndPages_does_not_throw_when_navigation_unavailable()
    {
        NavigationService sut = null!;
        try
        {
            sut = CreateSut();

            var ex = Record.Exception(() => sut.PopAllModalsAndPages());

            Assert.Null(ex);
        }
        finally
        {
            sut?.Dispose();
        }
    }

    [Fact]
    public void Dispose_does_not_throw_when_navigation_unavailable()
    {
        var sut = CreateSut();

        var ex = Record.Exception(() => sut.Dispose());

        Assert.Null(ex);
    }

    [Collection("MauiUi")]
    public sealed class MauiNavigationServiceTests(MauiUiFixture fixture)
    {
        private static NavigationService CreateMauiSut() =>
            new(
                new ServiceCollection().BuildServiceProvider(),
                TestLogging.CreateLogger(),
                new NopDispatcher(),
                new SyncNavigationUiThreadInvoker());

        [Fact]
        public async Task GetCurrentPage_returns_top_page_when_navigation_stack_configured()
        {
            _ = fixture;
            if (!MauiUiTestHostHelper.CanUseVisualTree)
            {
                return;
            }

            NavigationService sut = null!;
            try
            {
                sut = CreateMauiSut();
                var topPage = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!await MauiUiTestHostHelper.EnsurePrimaryWindowAsync())
                    {
                        return null;
                    }

                    var window = Application.Current!.Windows[0];
                    var root = new ContentPage();
                    var navPage = new NavigationPage(root);
                    var child = new ContentPage();
                    await navPage.Navigation.PushAsync(child, false);
                    window.Page = navPage;
                    return child;
                });

                Assert.Same(topPage, sut.GetCurrentPage());
            }
            finally
            {
                sut?.Dispose();
            }
        }

        [Fact]
        public async Task PopAsync_leaves_single_page_stack_unchanged()
        {
            _ = fixture;
            if (!MauiUiTestHostHelper.CanUseVisualTree)
            {
                return;
            }

            NavigationService sut = null!;
            try
            {
                sut = CreateMauiSut();
                var initialCount = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!await MauiUiTestHostHelper.EnsurePrimaryWindowAsync())
                    {
                        return 0;
                    }

                    var window = Application.Current!.Windows[0];
                    var navPage = new NavigationPage(new ContentPage());
                    window.Page = navPage;
                    await sut.PopAsync();
                    return navPage.Navigation.NavigationStack.Count;
                });

                Assert.Equal(1, initialCount);
            }
            finally
            {
                sut?.Dispose();
            }
        }

        [Fact]
        public async Task PopAsync_refuses_to_pop_when_top_page_is_home()
        {
            _ = fixture;
            if (!MauiUiTestHostHelper.CanUseVisualTree)
            {
                return;
            }

            NavigationService sut = null!;
            HomeViewModel? homeVm = null;
            try
            {
                sut = CreateMauiSut();
                homeVm = new HomeViewModel(ViewModelTestDoubles.CreateHomeDeps());
                var stackCount = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!await MauiUiTestHostHelper.EnsurePrimaryWindowAsync())
                    {
                        return 0;
                    }

                    var window = Application.Current!.Windows[0];
                    var navPage = new NavigationPage(new ContentPage());
                    await navPage.Navigation.PushAsync(new Home(homeVm!), false);
                    window.Page = navPage;
                    await sut.PopAsync();
                    return navPage.Navigation.NavigationStack.Count;
                });

                Assert.Equal(2, stackCount);
            }
            finally
            {
                homeVm?.Dispose();
                sut?.Dispose();
            }
        }

        [Fact]
        public async Task PopAsync_removes_top_page_when_stack_has_multiple_non_home_pages()
        {
            _ = fixture;
            if (!MauiUiTestHostHelper.CanUseVisualTree)
            {
                return;
            }

            NavigationService sut = null!;
            try
            {
                sut = CreateMauiSut();
                var stackCount = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!await MauiUiTestHostHelper.EnsurePrimaryWindowAsync())
                    {
                        return 0;
                    }

                    var window = Application.Current!.Windows[0];
                    var navPage = new NavigationPage(new ContentPage());
                    await navPage.Navigation.PushAsync(new ContentPage(), false);
                    await navPage.Navigation.PushAsync(new ContentPage(), false);
                    window.Page = navPage;
                    await sut.PopAsync();
                    return navPage.Navigation.NavigationStack.Count;
                });

                Assert.Equal(2, stackCount);
            }
            finally
            {
                sut?.Dispose();
            }
        }

        [Fact]
        public async Task ClearCache_allows_fresh_navigation_lookup_after_window_rebind()
        {
            _ = fixture;
            if (!MauiUiTestHostHelper.CanUseVisualTree)
            {
                return;
            }

            NavigationService sut = null!;
            try
            {
                sut = CreateMauiSut();
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (!await MauiUiTestHostHelper.EnsurePrimaryWindowAsync())
                    {
                        return;
                    }

                    var window = Application.Current!.Windows[0];
                    window.Page = new NavigationPage(new ContentPage());
                });

                sut.ClearCache();

                Assert.NotNull(sut.GetCurrentPage());
            }
            finally
            {
                sut?.Dispose();
            }
        }
    }
}
