using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.ViewModels.Shared;
using Serilog;

namespace Bible.Alarm.Services;

public class NavigationService : INavigationService, 
    IRecipient<ShowAlarmModalMessage>,
    IRecipient<HideAlarmModalMessage>,
    IRecipient<ShowMediaProgressModalMessage>,
    IRecipient<HideMediaProgressModalMessage>,
    IRecipient<ShowToastMessage>,
    IRecipient<ClearToastsMessage>
{
    private readonly ILogger _logger;
    private INavigation _navigater;
    private readonly IServiceScopeFactory _scopeFactory;

    public event Action<object> NavigatedBack;
    private bool _disposed = false;

    private readonly AsyncQueue<(MvvmMessages, object)> _queue = new();

    public NavigationService(ILogger logger, INavigation navigater, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _navigater = navigater;
        _scopeFactory = scopeFactory;

        // Register for all messages
        WeakReferenceMessenger.Default.Register<ShowAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<HideAlarmModalMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowMediaProgressModalMessage>(this);
        WeakReferenceMessenger.Default.Register<HideMediaProgressModalMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowToastMessage>(this);
        WeakReferenceMessenger.Default.Register<ClearToastsMessage>(this);

        Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            
            while (!_disposed)
            {
                var item = await _queue.DequeueAsync();
                var message = item.Item1;
                var @object = item.Item2;

                switch (message)
                {
                    case MvvmMessages.ShowAlarmModal:
                    {
                        // Prevent showing alarm modal when playback is not active
                        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
                        if (!playbackService.IsPlaying) break;

                        var vm = scope.ServiceProvider.GetRequiredService<AlarmViewModal>();
                        await MainThread.InvokeOnMainThreadAsync(async () => { await ShowModal("AlarmModal", vm); });
                    }
                        break;

                    case MvvmMessages.HideAlarmModal:
                    case MvvmMessages.HideMediaProgressModal:
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () => { await CloseModal(); });
                    }
                        break;
                    case MvvmMessages.ShowToast:
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            using var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
                            await toastService.ShowMessage(@object as string);
                        });
                    }
                        break;

                    case MvvmMessages.ClearToasts:
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            using var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();
                            await toastService.Clear();
                        });
                    }
                        break;
                    case MvvmMessages.ShowMediaProgessModal:
                    {
                        var vm = scope.ServiceProvider.GetRequiredService<MediaProgressViewModal>();
                        await MainThread.InvokeOnMainThreadAsync(async () => { await ShowModal("MediaProgressModal", vm); });
                    }
                        break;
                }

                await Task.Delay(1000);
            }
        });
    }

    public void Receive(ShowAlarmModalMessage message)
    {
        _ = _queue.EnqueueAsync((MvvmMessages.ShowAlarmModal, message.Value));
    }

    public void Receive(HideAlarmModalMessage message)
    {
        _ = _queue.EnqueueAsync((MvvmMessages.HideAlarmModal, message.Value));
    }

    public void Receive(ShowMediaProgressModalMessage message)
    {
        _ = _queue.EnqueueAsync((MvvmMessages.ShowMediaProgessModal, message.Value));
    }

    public void Receive(HideMediaProgressModalMessage message)
    {
        _ = _queue.EnqueueAsync((MvvmMessages.HideMediaProgressModal, message.Value));
    }

    public void Receive(ShowToastMessage message)
    {
        _ = _queue.EnqueueAsync((MvvmMessages.ShowToast, message.Value));
    }

    public void Receive(ClearToastsMessage message)
    {
        _ = _queue.EnqueueAsync((MvvmMessages.ClearToasts, message.Value));
    }

    public async Task ShowModal(string name, object viewModel)
    {
        if (!CanNavigate())
        {
            _logger.Warning("Navigation service is not available in current context. Cannot show modal.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        
        switch (name)
        {
            case "LanguageModal":
            {
                var modal = scope.ServiceProvider.GetRequiredService<LanguageModal>();
                modal.BindingContext = viewModel;
                await _navigater.PushModalAsync(modal);
                break;
            }

            case "AlarmModal":
            {
                if (_navigater.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal)) return;

                var modal = scope.ServiceProvider.GetRequiredService<AlarmModal>();
                modal.BindingContext = viewModel;
                await _navigater.PushModalAsync(modal);
                break;
            }

            case "BatteryOptimizationExclusionModal":
            {
                var modal = scope.ServiceProvider.GetRequiredService<BatteryOptimizationExclusionModal>();
                modal.BindingContext = viewModel;
                await _navigater.PushModalAsync(modal);
                break;
            }

            case "NumberOfChaptersModal":
            {
                var modal = scope.ServiceProvider.GetRequiredService<NumberOfChaptersModal>();
                modal.BindingContext = viewModel;
                await _navigater.PushModalAsync(modal);
                break;
            }

            case "MediaProgressModal":
            {
                if (_navigater.ModalStack.LastOrDefault()?.GetType() == typeof(MediaProgressModal)) return;

                var modal = scope.ServiceProvider.GetRequiredService<MediaProgressModal>();
                modal.BindingContext = viewModel;
                await _navigater.PushModalAsync(modal);
                break;
            }

            default:
                throw new ArgumentException("Modal not defined.", name);
        }
    }

    public async Task Navigate(object viewModel)
    {
        if (!CanNavigate())
        {
            _logger.Warning("Navigation service is not available in current context. Cannot navigate.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        
        var vmName = viewModel.GetType().Name;

        var top = _navigater.NavigationStack.LastOrDefault();

        if (top != null && top.BindingContext.GetType().Name == vmName)
        {
            var disposable = viewModel as IDisposable;
            disposable?.Dispose();
            return;
        }

        switch (vmName)
        {
            case "ScheduleViewModel":
            {
                var view = scope.ServiceProvider.GetRequiredService<Schedule>();
                view.BindingContext = viewModel;
                await _navigater.PushAsync(view);
                break;
            }

            case "MusicSelectionViewModel":
            {
                var view = scope.ServiceProvider.GetRequiredService<MusicSelection>();
                view.BindingContext = viewModel;
                await _navigater.PushAsync(view);
                break;
            }
            case "SongBookSelectionViewModel":
            {
                var view = scope.ServiceProvider.GetRequiredService<SongBookSelection>();
                view.BindingContext = viewModel;
                await _navigater.PushAsync(view);
                break;
            }
            case "TrackSelectionViewModel":
            {
                var view = scope.ServiceProvider.GetRequiredService<TrackSelection>();
                view.BindingContext = viewModel;
                await _navigater.PushAsync(view);
                break;
            }
            case "BibleSelectionViewModel":
            {
                var view = scope.ServiceProvider.GetRequiredService<BibleSelection>();
                view.BindingContext = viewModel;
                await _navigater.PushAsync(view);
                break;
            }
            case "BookSelectionViewModel":
            {
                var view = scope.ServiceProvider.GetRequiredService<BookSelection>();
                view.BindingContext = viewModel;
                await _navigater.PushAsync(view);
                break;
            }
            case "ChapterSelectionViewModel":
            {
                var view = scope.ServiceProvider.GetRequiredService<ChapterSelection>();
                view.BindingContext = viewModel;
                await _navigater.PushAsync(view);
                break;
            }
            default:
                throw new ArgumentException("Invalid View Model name", vmName);
        }
    }

    public async Task GoBack()
    {
        try
        {
            if (_navigater == null)
            {
                _logger.Warning("Navigation service is not available. Cannot go back.");
                return;
            }

            if (_navigater.ModalStack.Count > 0)
            {
                await CloseModal();
                return;
            }

            if (_navigater.NavigationStack.Count > 1)
            {
                var top = _navigater.NavigationStack.Last();
                ReduxContainer.Store.Dispatch(new BackAction(top.BindingContext as IDisposable));
                await _navigater.PopAsync();
            }

            var currentPage = _navigater.NavigationStack.LastOrDefault();

            if (currentPage != null) NavigatedBack?.Invoke(currentPage.BindingContext);
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened when navigating back.");
        }
    }

    public async Task CloseModal()
    {
        try
        {
            if (!CanNavigate())
            {
                _logger.Warning("Navigation service is not available in current context. Cannot close modal.");
                return;
            }

            if (_navigater.ModalStack.Count > 0)
            {
                var modal = await _navigater.PopModalAsync();
                if (modal.BindingContext is IDisposable disposable) disposable.Dispose();
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened when closing Modal.");
        }
    }

    /// <summary>
    /// Checks if navigation is available in the current context
    /// </summary>
    private bool CanNavigate()
    {
        return _navigater != null && IsForegroundContext();
    }

    /// <summary>
    /// Determines if we're in a foreground context where UI operations are safe
    /// </summary>
    private bool IsForegroundContext()
    {
        try
        {
            // Check if we're on the main thread and have a valid application context
            return MainThread.IsMainThread && Application.Current != null;
        }
        catch
        {
            // If we can't determine the context, assume we're in background
            return false;
        }
    }

    public async Task NavigateToHome()
    {
        try
        {
            if (!CanNavigate())
            {
                _logger.Warning("Navigation service is not available in current context. Cannot navigate to home.");
                return;
            }

            while (_navigater.ModalStack.Count > 0) await _navigater.PopModalAsync();

            while (_navigater.NavigationStack.Count > 1)
            {
                var top = _navigater.NavigationStack.Last();
                ReduxContainer.Store.Dispatch(new BackAction(top.BindingContext as IDisposable));
                await _navigater.PopAsync();
            }

            var currentPage = _navigater.NavigationStack.LastOrDefault();

            if (currentPage != null) NavigatedBack?.Invoke(currentPage.BindingContext);
        }
        catch (Exception e)
        {
            _logger.Error(e, "An error happened when navigating to home.");
        }
    }

    public void SetNavigation(INavigation navigation)
    {
        _navigater = navigation;
    }

    public void Dispose()
    {
        _disposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}