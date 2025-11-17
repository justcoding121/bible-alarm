using Bible.Alarm.Views;

namespace Bible.Alarm.Views.General;

public partial class BatteryOptimizationExclusionModal : ContentPage, IDisposable
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
            _isDisposed = true;
        }
    }
}