#nullable enable

using System.ComponentModel;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

/// <summary>
/// Handles property change events from MusicSelectionContainerViewModel.
/// </summary>
public sealed class PropertyChangeHandler : IDisposable
{
    private readonly MusicSelectionContainer container;
    private readonly Action<bool, bool> updateVisibility;
    private readonly Action scrollToBottom;
    private CancellationTokenSource? debounceTokenSource;
    private bool disposed;

    public bool IsInitialLoad { get; set; } = true;

    public bool LastMusicEnabledState { get; set; }

    public PropertyChangeHandler(
        MusicSelectionContainer container,
        Action<bool, bool> updateVisibility,
        Action scrollToBottom)
    {
        this.container = container;
        this.updateVisibility = updateVisibility;
        this.scrollToBottom = scrollToBottom;
    }

    public bool ShouldScrollOnExpand { get; set; }

    public void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e, MusicSelectionContainerViewModel? viewModel)
    {
        if (sender is not MusicSelectionContainerViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.MusicEnabled))
        {
            HandleMusicEnabledPropertyChanged(vm);
            return;
        }

        if (e.PropertyName == nameof(MusicSelectionContainerViewModel.ShouldScrollToBottom) && vm.ShouldScrollToBottom)
        {
            HandleShouldScrollToBottomPropertyChanged(viewModel);
        }
    }

    private void HandleMusicEnabledPropertyChanged(MusicSelectionContainerViewModel vm)
    {
        var newState = vm.MusicEnabled;

#if DEBUG
        Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.PropertyChangedMusicEnabledState, newState, LastMusicEnabledState, IsInitialLoad);
#endif

        if (!IsInitialLoad && newState == LastMusicEnabledState)
        {
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.StateUnchangedIgnoring);
#endif
            return;
        }

        if (IsInitialLoad)
        {
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.PropertyChangeDuringInitialLoadUpdatingVisibility, newState, LastMusicEnabledState);
#endif
            LastMusicEnabledState = newState;
            container.Dispatcher.Dispatch(() =>
            {
                if (container.Handler != null)
                {
                    updateVisibility(newState, false);
                }
            });
            return;
        }

        ShouldScrollOnExpand = !LastMusicEnabledState && newState;
        LastMusicEnabledState = newState;

#if DEBUG
        Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.TriggeringAnimationMusicEnabled, newState, ShouldScrollOnExpand);
#endif

        debounceTokenSource?.Cancel();
        debounceTokenSource?.Dispose();
        debounceTokenSource = new CancellationTokenSource();
        var token = debounceTokenSource.Token;
        const bool shouldAnimate = true;

        container.Dispatcher.Dispatch(() =>
        {
            if (!token.IsCancellationRequested && container.Handler != null)
            {
#if DEBUG
                Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.CallingUpdateCollapsibleContentVisibility, shouldAnimate, newState);
#endif
                updateVisibility(newState, shouldAnimate);
            }
        });
    }

    private void HandleShouldScrollToBottomPropertyChanged(MusicSelectionContainerViewModel? viewModel)
    {
#if DEBUG
        Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ShouldScrollToBottomScrolling);
#endif

        container.Dispatcher.DispatchAsync(async () =>
        {
            await Task.Delay(200);
            scrollToBottom();

            if (viewModel != null)
            {
                viewModel.ShouldScrollToBottom = false;
            }
        });
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed || !disposing)
        {
            return;
        }

        try
        {
            debounceTokenSource?.Cancel();
            debounceTokenSource?.Dispose();
        }
        catch (Exception)
        {
            // Ignore errors during cancellation/disposal
        }

        disposed = true;
    }
}

