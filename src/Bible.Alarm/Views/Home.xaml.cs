#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels;
using Serilog;
using Syncfusion.Maui.Buttons;

namespace Bible.Alarm.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public sealed partial class Home : BaseContentPage, IDisposable
{
    private bool isDisposed;
    private bool hasHandledFirstLoad;
    private bool itemsSourceClearedOnDisappearing;
    private bool disappearedForModal;
    private readonly HomeViewModel viewModel;

    public Home(HomeViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);

        InitializeComponent();
        BindingContext = vm;
        viewModel = vm;

        // Hide the non-effects button on WinUI (use SfEffectsView version instead)
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            AlarmSettingsFloatingButtonNoEffects.IsVisible = false;
            ((INotifyPropertyChanged)viewModel).PropertyChanged += OnViewModelPropertyChanged;
        }

        // Use Loaded event which fires after the page is in the visual tree
        Loaded += OnPageLoaded;
    }

    /// <summary>
    /// On Windows we set ItemsSource in OnAppearing when returning from Schedule page.
    /// HandleStateChangedAsync is async and may replace Schedules with a new collection after
    /// OnAppearing runs, so the CollectionView can be left showing the old reference. When
    /// Schedules property changes, re-apply ItemsSource so the list shows the updated collection.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.Schedules) || isDisposed || Handler == null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (isDisposed || SchedulesCollectionView == null || viewModel?.Schedules == null)
            {
                return;
            }

            SchedulesCollectionView.ItemsSource = viewModel.Schedules;
        });
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // Only handle once per page instance
        if (hasHandledFirstLoad)
        {
            return;
        }

        hasHandledFirstLoad = true;

        // Unsubscribe to avoid multiple calls
        Loaded -= OnPageLoaded;

        // Wait a bit to ensure the page is fully rendered and visible
        await Task.Delay(100);

        // NOTE: Don't call HideSchedulePageOverlay() here!
        // This was causing a race condition: if user navigates to schedule page while this delay runs,
        // the overlay would be hidden prematurely. The overlay is now properly managed by:
        // - ResetScheduleStateAction (when Cancel is clicked)
        // - BackAction (when navigating back)
        // - ScheduleViewModel's OnContentLoaded (when containers are ready)

        // Check and show alarm settings modal on first app launch
        await viewModel.CheckAndShowAlarmSettingsOnFirstLaunchAsync();

#if DEBUG
        // Log that home page is fully loaded with data
        var totalBootstrapTime = BootstrapTimingHelper.GetElapsedMilliseconds();
        Log.Information(AppConstants.Logging.HomePageDiagnosticsLog.BootstrapHomeFullyLoadedWithData, totalBootstrapTime);
#endif
    }

    private void OnAddScheduleButtonClicked(object? sender, EventArgs e)
    {
        Log.Information(AppConstants.Logging.HomePageDiagnosticsLog.OnAddScheduleButtonClicked,
            HomeViewModel.IsBootstrapComplete,
            viewModel?.AddScheduleCommand?.CanExecute(null) ?? false);

        // Manually execute the command to test
        if (viewModel?.AddScheduleCommand != null && viewModel.AddScheduleCommand.CanExecute(null))
        {
            Log.Information(AppConstants.Logging.HomePageDiagnosticsLog.OnAddScheduleManuallyExecutingCommand);
            viewModel.AddScheduleCommand.Execute(null);
        }
        else
        {
            Log.Warning(AppConstants.Logging.HomePageDiagnosticsLog.OnAddScheduleCommandCannotExecute,
                HomeViewModel.IsBootstrapComplete);
        }
    }

    private void OnScheduleItemTapped(object? sender, TappedEventArgs e)
    {
        View? container = sender as View;
        if (container == null || container.BindingContext is not ScheduleListItemViewModel item)
        {
            return;
        }

        var tapPosition = e.GetPosition(container);
        if (!tapPosition.HasValue)
        {
            viewModel.ViewScheduleCommand.Execute(item);
            return;
        }

        // Don't navigate when the tap landed on a Button or Switch
        if (IsTapOnInteractiveControl(container, tapPosition.Value))
        {
            return;
        }

        viewModel.ViewScheduleCommand.Execute(item);
    }

    private bool IsTapOnInteractiveControl(View view, Point tapPosition)
    {
        if (IsViewInteractiveControl(view, tapPosition))
        {
            return true;
        }

        return IsTapOnChildControl(view, tapPosition);
    }

    private static bool IsViewInteractiveControl(View view, Point tapPosition)
    {
        if (view is Button or SfButton or Switch or SfSwitch)
        {
            // Only check bounds if they're valid (layout has been calculated)
            var bounds = view.Bounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                return bounds.Contains(tapPosition);
            }
        }
        return false;
    }

    private bool IsTapOnChildControl(View view, Point tapPosition)
    {
        return EnumerateChildViewsForTapHitTest(view)
            .Any(childView => IsTapOnChild(childView, tapPosition));
    }

    private static IEnumerable<View> EnumerateChildViewsForTapHitTest(View view)
    {
        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is View childView)
                {
                    yield return childView;
                }
            }

            yield break;
        }

        if (view is IContentView contentView && contentView.Content is View contentChild)
        {
            yield return contentChild;
            yield break;
        }

        if (view is IVisualTreeElement visualElement)
        {
            foreach (var child in visualElement.GetVisualChildren())
            {
                if (child is View childView)
                {
                    yield return childView;
                }
            }
        }
    }

    private bool IsTapOnChild(View childView, Point tapPosition)
    {
        var childBounds = childView.Bounds;

        // Skip if bounds aren't valid (layout not yet calculated)
        if (childBounds.Width <= 0 || childBounds.Height <= 0)
        {
            return false;
        }

        if (!childBounds.Contains(tapPosition))
        {
            return false;
        }

        var relativePoint = new Point(
            tapPosition.X - childBounds.X,
            tapPosition.Y - childBounds.Y);

        return IsTapOnInteractiveControl(childView, relativePoint);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Restore CollectionView ItemsSource only when returning from a child page (after OnDisappearing cleared it).
        // On first appear we do not set ItemsSource so the XAML binding stays in effect; when bootstrap loads
        // schedules and replaces Schedules, the binding updates and the list shows. If we set ItemsSource here
        // on first load we overwrite the binding and the list never updates until the user navigates away and back.
        if (viewModel != null && itemsSourceClearedOnDisappearing)
        {
            SchedulesCollectionView.ItemsSource = viewModel.Schedules;
            itemsSourceClearedOnDisappearing = false;
        }

        // Only reset schedule state and re-run bootstrap logic when returning from
        // actual page navigation (schedule page), not when a modal is popped.
        // Modals (playback, battery optimization, etc.) don't modify schedule state,
        // and resetting here causes an unnecessary blank-then-reload flicker on the list.
        if (!disappearedForModal)
        {
            viewModel?.ResetScheduleState();
            hasHandledFirstLoad = false;
            Loaded += OnPageLoaded;
        }

        disappearedForModal = false;

        viewModel?.UpdateFloatingButtonVisibility();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // PlaybackModal uses PushAsync (navigation stack), not PushModalAsync (modal stack),
        // so we must also check the navigation stack to avoid ResetScheduleState on return.
        disappearedForModal = Navigation.ModalStack.Count > 0
            || (Navigation.NavigationStack is { Count: > 0 } navStack && navStack[^1] is General.PlaybackModal);

        // On Windows, clear ItemsSource to prevent InvalidOperationException from
        // delayed OnItemsVectorChanged callbacks running with VirtualView == null.
        // On Android/iOS this is unnecessary and causes a visible blank-then-reload
        // flicker when the page reappears (e.g., after closing the playback modal).
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            SchedulesCollectionView.ItemsSource = null;
            itemsSourceClearedOnDisappearing = true;
        }
    }

    protected override bool OnBackButtonPressed() =>
        // Prevent back navigation on Home page - it's the root page
        true;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI && viewModel is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged -= OnViewModelPropertyChanged;
            }

            // ViewModel was injected via constructor, so dispose it
            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
        }

        isDisposed = true;
    }
}
