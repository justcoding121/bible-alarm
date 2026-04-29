#nullable enable

using System;
using System.ComponentModel;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;
using Serilog;
using Syncfusion.Maui.Core;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class BibleSelectionContainer : ContentView
{
    private BiblePublicationSelectionContainerViewModel? viewModel;
    private ScrollManager? scrollManager;

    public BibleSelectionContainer()
    {
        InitializeComponent();
        WireUpTapGestureHandlers();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler != null && scrollManager == null)
        {
            scrollManager = new ScrollManager(this);
        }

        // Wire up handlers after visual tree is ready
        if (Handler != null)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                WireUpTapGestureHandlers();
            });
        }
    }

    private void WireUpTapGestureHandlers()
    {
        // Start traversal from Content property, not the ContentView itself
        if (Content is View content)
        {
            FindAndWireTapGestures(content);
        }
    }

    private void FindAndWireTapGestures(View view)
    {
        if (view == null)
        {
            return;
        }

        // Check gesture recognizers on this view
        foreach (var gesture in view.GestureRecognizers)
        {
            if (gesture is TapGestureRecognizer tapGesture)
            {
                tapGesture.Tapped -= OnTapGestureTapped; // Remove first to avoid duplicates
                tapGesture.Tapped += OnTapGestureTapped;
            }
        }
        
        // Handle SfEffectsView which also has GestureRecognizers
        if (view is SfEffectsView sfEffectsView)
        {
            foreach (var gesture in sfEffectsView.GestureRecognizers)
            {
                if (gesture is TapGestureRecognizer tapGesture)
                {
                    tapGesture.Tapped -= OnTapGestureTapped; // Remove first to avoid duplicates
                    tapGesture.Tapped += OnTapGestureTapped;
                }
            }
        }
        
        // Recursively check children
        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is View childView)
                {
                    FindAndWireTapGestures(childView);
                }
            }
        }
        else if (view is ContentView contentView && contentView.Content is View content)
        {
            FindAndWireTapGestures(content);
        }
        else if (view is SfEffectsView sfView && sfView.Content is View sfContent)
        {
            FindAndWireTapGestures(sfContent);
        }
    }

    private void OnTapGestureTapped(object? sender, TappedEventArgs e)
    {
        UnfocusScheduleNameEntry();
    }

    private void UnfocusScheduleNameEntry()
    {
        // Find parent ScheduleContent and call its unfocus method
        var parent = Parent;
        while (parent != null)
        {
            if (parent is ScheduleContent scheduleContent)
            {
                scheduleContent.UnfocusScheduleNameEntry();
                break;
            }
            parent = parent.Parent;
        }
    }

    public BibleSelectionContainer(BiblePublicationSelectionContainerViewModel viewModel) : this()
    {
        BindingContext = viewModel;
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
        viewModel = BindingContext as BiblePublicationSelectionContainerViewModel;

        if (viewModel != null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (viewModel == null) return;

        // Scroll when ViewModel signals it (after section/track selection)
        if (e.PropertyName == nameof(BiblePublicationSelectionContainerViewModel.ShouldScrollToContainer) && viewModel.ShouldScrollToContainer)
        {
#if DEBUG
            Log.Debug(AppConstants.Logging.MauiPlatformUiDiagnosticsLog.BibleSelectionContainerShouldScrollToContainerDebug);
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

