using Bible.Alarm.Views;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class NumberOfChaptersModal : ContentPage, IDisposable
{
    private bool _isDisposed;

    public NumberOfChaptersModal()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // This modal uses parent page view model, so do NOT dispose it
            _isDisposed = true;
        }
    }
}