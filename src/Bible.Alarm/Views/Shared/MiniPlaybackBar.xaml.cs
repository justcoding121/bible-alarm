#nullable enable
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Views.Shared;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class MiniPlaybackBar : ContentView
{
    public MiniPlaybackBar()
    {
        InitializeComponent();

        IsVisible = false;

        var vm = MiniPlaybackBarViewModel.Instance;
        if (vm != null)
        {
            BindingContext = vm;
            SetBinding(IsVisibleProperty,
                new Binding(nameof(MiniPlaybackBarViewModel.IsVisible), source: vm));
        }
    }
}
