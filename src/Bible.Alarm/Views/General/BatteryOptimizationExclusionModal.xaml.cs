namespace Bible.Alarm.Views.General;

public partial class BatteryOptimizationExclusionModal : BaseContentPage, IDisposable
{
    private bool _isDisposed;

    public BatteryOptimizationExclusionModal()
    {
        InitializeComponent();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // This modal uses parent page view model, so do NOT dispose it
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            _isDisposed = true;
        }
    }
}