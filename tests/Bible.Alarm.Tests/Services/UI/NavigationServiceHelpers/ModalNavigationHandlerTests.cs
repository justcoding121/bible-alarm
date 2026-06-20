#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;
using Bible.Alarm.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

public sealed class ModalNavigationHandlerTests
{
    private sealed class UnusedNavigation : INavigation
    {
        IReadOnlyList<Page> INavigation.ModalStack => Array.Empty<Page>();

        IReadOnlyList<Page> INavigation.NavigationStack => Array.Empty<Page>();

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
    public async Task OpenLanguageModalAsync_throws_when_binding_context_type_is_unsupported()
    {
        var sut = new ModalNavigationHandler(TestLogging.CreateLogger(), new ServiceCollection().BuildServiceProvider());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.OpenLanguageModalAsync(new UnusedNavigation(), new object()));

        Assert.Contains("Unsupported ViewModel type", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsPlaybackModalAlreadyShown_returns_false_for_empty_stack()
    {
        var nav = new UnusedNavigation();

        Assert.False(ModalNavigationHandler.IsPlaybackModalAlreadyShown(nav));
    }
}
