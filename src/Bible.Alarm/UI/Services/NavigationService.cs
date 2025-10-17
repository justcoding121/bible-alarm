using Advanced.Algorithms.Distributed;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI.Views;
using Bible.Alarm.UI.Views.Bible;
using Bible.Alarm.UI.Views.General;
using Bible.Alarm.UI.Views.Music;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions;
using Bible.Alarm.ViewModels.Shared;
using MediaManager;
using NLog;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.UI
{
    public class NavigationService : INavigationService
    {
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;


        private readonly IContainer _container;

        private readonly INavigation _navigater;

        public event Action<object> NavigatedBack;
        private bool _disposed = false;

        private AsyncQueue<(MvvmMessages, object)> _queue = new AsyncQueue<(MvvmMessages, object)>();

        public NavigationService(IContainer container, INavigation navigater)
        {
            this._container = container;
            this._navigater = navigater;

            Messenger<object>.Subscribe(MvvmMessages.ShowAlarmModal, async @param =>
            {
                await _queue.EnqueueAsync((MvvmMessages.ShowAlarmModal, @param));
            });

            Messenger<object>.Subscribe(MvvmMessages.HideAlarmModal, async @param =>
            {
                await _queue.EnqueueAsync((MvvmMessages.HideAlarmModal, @param));
            });

            Messenger<object>.Subscribe(MvvmMessages.ShowMediaProgessModal, async @param =>
            {
                await _queue.EnqueueAsync((MvvmMessages.ShowMediaProgessModal, @param));
            });

            Messenger<object>.Subscribe(MvvmMessages.HideMediaProgressModal, async @param =>
            {
                await _queue.EnqueueAsync((MvvmMessages.HideMediaProgressModal, @param));
            });

            Messenger<object>.Subscribe(MvvmMessages.ShowToast, async @param =>
            {
                await _queue.EnqueueAsync((MvvmMessages.ShowToast, @param));
            });

            Messenger<object>.Subscribe(MvvmMessages.ClearToasts, async @param =>
            {
                await _queue.EnqueueAsync((MvvmMessages.ClearToasts, @param));
            });

            var syncContext = this._container.Resolve<TaskScheduler>();

            Task.Run(async () =>
            {
                while (!_disposed)
                {
                    var item = await _queue.DequeueAsync();
                    var message = item.Item1;
                    var @object = item.Item2;

                    switch (message)
                    {
                        case MvvmMessages.ShowAlarmModal:
                            {
                                //hack to prevent pop-ups in quick succession
                                var mediaManager = container.Resolve<IMediaManager>();
                                if (!mediaManager.IsPlaying())
                                {
                                    break;
                                }

                                var vm = container.Resolve<AlarmViewModal>();
                                await Task.Delay(0).ContinueWith(async (x) =>
                                {
                                    await ShowModal("AlarmModal", vm);

                                }, syncContext);
                            }
                            break;

                        case MvvmMessages.HideAlarmModal:
                        case MvvmMessages.HideMediaProgressModal:
                            {
                                await Task.Delay(0).ContinueWith(async (x) =>
                                {
                                    await CloseModal();

                                }, syncContext);
                            }
                            break;
                        case MvvmMessages.ShowToast:
                            {
                                await Task.Delay(0).ContinueWith(async (x) =>
                                {
                                    using var toastService = this._container.Resolve<IToastService>();
                                    await toastService.ShowMessage(@object as string);

                                }, syncContext);
                            }
                            break;

                        case MvvmMessages.ClearToasts:
                            {
                                await Task.Delay(0).ContinueWith(async (x) =>
                                {
                                    using var toastService = this._container.Resolve<IToastService>();
                                    await toastService.Clear();

                                }, syncContext);
                            }
                            break;
                        case MvvmMessages.ShowMediaProgessModal:
                            {
                                var vm = this._container.Resolve<MediaProgressViewModal>();
                                await Task.Delay(0).ContinueWith(async (x) =>
                                {
                                    await ShowModal("MediaProgressModal", vm);

                                }, syncContext);
                            }
                            break;

                    }

                    await Task.Delay(1000);
                }

            });
        }

        public async Task ShowModal(string name, object viewModel)
        {
            switch (name)
            {
                case "LanguageModal":
                    {
                        var modal = _container.Resolve<LanguageModal>();
                        modal.BindingContext = viewModel;
                        await _navigater.PushModalAsync(modal);
                        break;
                    }

                case "AlarmModal":
                    {
                        if (_navigater.ModalStack.LastOrDefault()?.GetType() == typeof(AlarmModal))
                        {
                            return;
                        }

                        var modal = _container.Resolve<AlarmModal>();
                        modal.BindingContext = viewModel;
                        await _navigater.PushModalAsync(modal);
                        break;
                    }

                case "BatteryOptimizationExclusionModal":
                    {
                        var modal = _container.Resolve<BatteryOptimizationExclusionModal>();
                        modal.BindingContext = viewModel;
                        await _navigater.PushModalAsync(modal);
                        break;
                    }

                case "NumberOfChaptersModal":
                    {
                        var modal = _container.Resolve<NumberOfChaptersModal>();
                        modal.BindingContext = viewModel;
                        await _navigater.PushModalAsync(modal);
                        break;
                    }

                case "MediaProgressModal":
                    {
                        if (_navigater.ModalStack.LastOrDefault()?.GetType() == typeof(MediaProgressModal))
                        {
                            return;
                        }

                        var modal = _container.Resolve<MediaProgressModal>();
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
                        var view = _container.Resolve<Schedule>();
                        view.BindingContext = viewModel;
                        await _navigater.PushAsync(view);
                        break;
                    }

                case "MusicSelectionViewModel":
                    {
                        var view = _container.Resolve<MusicSelection>();
                        view.BindingContext = viewModel;
                        await _navigater.PushAsync(view);
                        break;
                    }
                case "SongBookSelectionViewModel":
                    {
                        var view = _container.Resolve<SongBookSelection>();
                        view.BindingContext = viewModel;
                        await _navigater.PushAsync(view);
                        break;
                    }
                case "TrackSelectionViewModel":
                    {
                        var view = _container.Resolve<TrackSelection>();
                        view.BindingContext = viewModel;
                        await _navigater.PushAsync(view);
                        break;
                    }
                case "BibleSelectionViewModel":
                    {
                        var view = _container.Resolve<BibleSelection>();
                        view.BindingContext = viewModel;
                        await _navigater.PushAsync(view);
                        break;
                    }
                case "BookSelectionViewModel":
                    {
                        var view = _container.Resolve<BookSelection>();
                        view.BindingContext = viewModel;
                        await _navigater.PushAsync(view);
                        break;
                    }
                case "ChapterSelectionViewModel":
                    {
                        var view = _container.Resolve<ChapterSelection>();
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
                if (_navigater.ModalStack.Count > 0)
                {
                    await CloseModal();
                    return;
                }

                if (_navigater.NavigationStack.Count > 1)
                {
                    var top = _navigater.NavigationStack.Last();
                    ReduxContainer.Store.Dispatch(new BackAction((top.BindingContext as IDisposable)));
                    await _navigater.PopAsync();
                }

                var currentPage = _navigater.NavigationStack.LastOrDefault();

                if (currentPage != null)
                {
                    NavigatedBack?.Invoke(currentPage.BindingContext);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when navigating back.");
            }
        }

        public async Task CloseModal()
        {
            try
            {
                if (_navigater.ModalStack.Count > 0)
                {
                    var modal = await _navigater.PopModalAsync();
                    if (modal.BindingContext is IDisposableModal)
                    {
                        (modal.BindingContext as IDisposableModal).Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when closing Modal.");
            }
        }

        public async Task NavigateToHome()
        {
            try
            {
                while (_navigater.ModalStack.Count > 0)
                {
                    await _navigater.PopModalAsync();
                }

                while (_navigater.NavigationStack.Count > 1)
                {
                    var top = _navigater.NavigationStack.Last();
                    ReduxContainer.Store.Dispatch(new BackAction((top.BindingContext as IDisposable)));
                    await _navigater.PopAsync();
                }

                var currentPage = _navigater.NavigationStack.LastOrDefault();

                if (currentPage != null)
                {
                    NavigatedBack?.Invoke(currentPage.BindingContext);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "An error happened when navigating to home.");
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
