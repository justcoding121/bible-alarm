#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MiniPlaybackBar : ContentView
{
    /// <summary>
    /// Last measured height of the mini bar (device-independent pixels).
    /// Updated on every layout pass so toast services can position above it.
    /// </summary>
    public static double LastMeasuredHeight { get; private set; }

    public MiniPlaybackBar()
    {
        InitializeComponent();

        SizeChanged += OnSizeChanged;

        var vm = MiniPlaybackBarViewModel.Instance;
        if (vm != null)
        {
            BindingContext = vm;
            IsVisible = vm.IsVisible;

            if (vm.IsVisible && LastMeasuredHeight > 0)
            {
                HeightRequest = LastMeasuredHeight;
            }

            SetBinding(IsVisibleProperty,
                new Binding(nameof(MiniPlaybackBarViewModel.IsVisible), source: vm));
        }
        else
        {
            IsVisible = false;
        }
    }

    private static void OnSizeChanged(object? sender, EventArgs e)
    {
        if (sender is MiniPlaybackBar bar)
        {
            var recorded = LastMeasuredHeight;
            if (MiniPlaybackBarMeasurementRecorder.TryRecordVisibleHeight(bar.IsVisible, bar.Height, ref recorded))
            {
                LastMeasuredHeight = recorded;
            }
        }
    }
}
