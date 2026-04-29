namespace CommunityToolkit.Maui.Extensions;

// Since MediaElement can't access .NET MAUI internals we have to copy this code here
// https://github.com/dotnet/maui/blob/main/src/Controls/src/Core/Platform/PageExtensions.cs
static class PageExtensions
{
    internal static Page GetCurrentPage(this Page currentPage)
    {
        var modalStack = currentPage.NavigationProxy.ModalStack;
        if (modalStack.Count > 0)
        {
            return modalStack[modalStack.Count - 1];
        }

        return currentPage switch
        {
            FlyoutPage fp => GetCurrentPage(fp.Detail),
            Shell shell when (shell.CurrentItem?.CurrentItem is IShellSectionController ssc) => ssc.PresentedPage,
            IPageContainer<Page> pc => GetCurrentPage(pc.CurrentPage),
            _ => currentPage
        };
    }

    internal record struct ParentWindow
    {
        static Page CurrentPage => GetCurrentPage(Application.Current?.Windows[^1].Page ?? throw new InvalidOperationException($"{nameof(Page)} cannot be null."));
        /// <summary>
        /// Checks if the parent window is null.
        /// </summary>
        public static bool Exists
        {
            get
            {
                if (CurrentPage.GetParentWindow() is null)
                {
                    return false;
                }
                if (CurrentPage.GetParentWindow().Handler is null)
                {
                    return false;
                }

                return CurrentPage.GetParentWindow().Handler?.PlatformView is not null;
            }
        }
    }
}