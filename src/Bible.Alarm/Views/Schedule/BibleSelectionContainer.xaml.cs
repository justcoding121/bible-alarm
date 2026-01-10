#nullable enable
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;
using Microsoft.Maui.Controls.Xaml;
using System.ComponentModel;
using Serilog;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BibleSelectionContainer : ContentView
{
    private BibleSelectionContainerViewModel? viewModel;
    private ScrollManager? scrollManager;

    public BibleSelectionContainer()
    {
        InitializeComponent();
    }

    public BibleSelectionContainer(BibleSelectionContainerViewModel viewModel) : this()
    {
        BindingContext = viewModel;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler != null && scrollManager == null)
        {
            scrollManager = new ScrollManager(this);
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Unsubscribe from old view model
        if (viewModel != null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new view model
        viewModel = BindingContext as BibleSelectionContainerViewModel;
        
        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (viewModel == null) return;

        // Scroll when ViewModel signals it (after section/chapter selection)
        if (e.PropertyName == nameof(BibleSelectionContainerViewModel.ShouldScrollToContainer) && viewModel.ShouldScrollToContainer)
        {
#if DEBUG
            Log.Debug("[BibleSelectionContainer] ShouldScrollToContainer property changed, scrolling to container");
#endif
            // Small delay to ensure UI is updated after modal closes
            Dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(200); // Delay to allow UI to update after modal closes
                if (scrollManager != null)
                {
                    // Scroll to this container (not to bottom)
                    scrollManager.ScrollToElement(this, scrollToBottom: false);
                }
                // Reset the flag after scrolling
                if (viewModel != null)
                {
                    viewModel.ShouldScrollToContainer = false;
                }
            });
        }
    }
}

