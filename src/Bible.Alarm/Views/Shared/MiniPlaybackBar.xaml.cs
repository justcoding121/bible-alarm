#nullable enable
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

        IsVisible = false;
        SizeChanged += OnSizeChanged;

        var vm = MiniPlaybackBarViewModel.Instance;
        if (vm != null)
        {
            BindingContext = vm;
            SetBinding(IsVisibleProperty,
                new Binding(nameof(MiniPlaybackBarViewModel.IsVisible), source: vm));
        }
    }

    private static void OnSizeChanged(object? sender, EventArgs e)
    {
        if (sender is MiniPlaybackBar bar && bar.IsVisible && bar.Height > 0)
        {
            LastMeasuredHeight = bar.Height;
        }
    }
}
