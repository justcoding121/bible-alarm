namespace Bible.Alarm.Views;

/// <summary>
/// Base class for ContentPage that automatically disposes IDisposable ViewModels
/// when the page is navigated away from and removed from the stack.
/// </summary>
public class BaseContentPage : ContentPage
{
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        if (BindingContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

