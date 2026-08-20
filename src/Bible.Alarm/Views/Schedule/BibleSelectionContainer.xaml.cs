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

    public BibleSelectionContainer(BiblePublicationSelectionContainerViewModel viewModel) : this()
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

        AttachTapHandlers(view.GestureRecognizers);

        if (view is SfEffectsView sfEffectsView)
        {
            AttachTapHandlers(sfEffectsView.GestureRecognizers);
        }

        TraverseChildrenForTapGestures(view);
    }

    private void AttachTapHandlers(IEnumerable<IGestureRecognizer> gestures)
    {
        foreach (var gesture in gestures)
        {
            if (gesture is TapGestureRecognizer tapGesture)
            {
                tapGesture.Tapped -= OnTapGestureTapped;
                tapGesture.Tapped += OnTapGestureTapped;
            }
        }
    }

    private void TraverseChildrenForTapGestures(View view)
    {
        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is View childView)
                {
                    FindAndWireTapGestures(childView);
                }
            }

            return;
        }

        if (view is ContentView contentView && contentView.Content is View nestedContent)
        {
            FindAndWireTapGestures(nestedContent);
            return;
        }

        if (view is SfEffectsView sfView && sfView.Content is View sfContent)
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

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (viewModel != null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

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

