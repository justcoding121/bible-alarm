namespace Bible.Alarm.Views.General;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class IOSNotificationPermissionModal : BaseContentPage, IDisposable
{
    private bool isDisposed;

    public IOSNotificationPermissionModal()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            isDisposed = true;
        }
    }
}
