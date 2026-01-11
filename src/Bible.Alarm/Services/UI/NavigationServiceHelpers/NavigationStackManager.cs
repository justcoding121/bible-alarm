#nullable enable
namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

/// <summary>
/// Handles navigation stack operations like pop.
/// </summary>
public sealed class NavigationStackManager
{
    /// <summary>
    /// Pops the current modal page from the navigation stack.
    /// </summary>
    public async Task PopModalAsync(INavigation navigation)
    {
        if (navigation.ModalStack.Count == 0)
        {
            return;
        }

        var modal = navigation.ModalStack.LastOrDefault();
        await navigation.PopModalAsync(animated: false);

        // Dispose the modal if it implements IDisposable
        if (modal is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    public async Task PopAsync(INavigation navigation)
    {
        if (navigation.NavigationStack.Count <= 1)
        {
            return;
        }

        var page = navigation.NavigationStack.LastOrDefault();
        await navigation.PopAsync(animated: true);

        // Dispose the page if it implements IDisposable
        if (page is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
