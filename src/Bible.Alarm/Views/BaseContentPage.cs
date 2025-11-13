namespace Bible.Alarm.Views;

/// <summary>
/// Base class for ContentPage that automatically disposes IDisposable ViewModels
/// when the page is navigated away from.
/// </summary>
public class BaseContentPage : ContentPage
{
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        
        // Dispose ViewModel if it implements IDisposable
        // This ensures proper cleanup of resources like event subscriptions, timers, etc.
        if (BindingContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

