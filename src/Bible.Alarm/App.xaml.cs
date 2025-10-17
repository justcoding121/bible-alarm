using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI;
using Bible.Alarm.ViewModels;
using NLog;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm
{
    public partial class App : Application
    {
        private readonly IContainer _container;

        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        public static bool IsInForeground { get; set; } = false;

        public App(IContainer container)
        {
            _container = container;
            Init();
        }

        private void Init()
        {
            InitializeComponent();

            if (_container.RegisteredTypes.Any(x => x == typeof(NavigationPage)))
            {
                Windows[0].Page = _container.Resolve<NavigationPage>();
            }
            else
            {
                var navigationPage = new NavigationPage();

                var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

                _container.Register(x => taskScheduler);
                _container.RegisterSingleton(x => navigationPage);
                _container.RegisterSingleton(x => navigationPage.Navigation);
                _container.RegisterSingleton<INavigationService>(x => new NavigationService(_container, navigationPage.Navigation));

                Windows[0].Page = navigationPage;

                Windows[0].Page.SetValue(NavigationPage.BarBackgroundColorProperty, Colors.SlateBlue);
                Windows[0].Page.SetValue(NavigationPage.BarTextColorProperty, Colors.White);

                Func<Task> homePageSetter = async () =>
                {
                    var homePage = new Home();
                    homePage.BindingContext = _container.Resolve<HomeViewModel>();
                    await navigationPage.Navigation.PushAsync(homePage);
                };

                if (CurrentDevice.RuntimePlatform != DevicePlatform.Android.ToString())
                {
                    homePageSetter().Wait();
                }

                Task.Delay(100).ContinueWith(async (a) =>
                {
                    if (CurrentDevice.RuntimePlatform == DevicePlatform.Android.ToString())
                    {
                        await homePageSetter();
                    }

                }, taskScheduler)
                .ContinueWith(x =>
                {
                    var playbackService = _container.Resolve<IPlaybackService>();

                    if (playbackService.IsPrepared)
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    }
                });
            }

        }

        protected override void OnStart()
        {
            IsInForeground = true;

            base.OnStart();
            Task.Run(async () =>
            {
                try
                {
                    var navigationService = _container.Resolve<INavigationService>();
                    // Handle when your app starts  
                    await navigationService.NavigateToHome();

                    var playbackService = _container.Resolve<IPlaybackService>();

                    if (playbackService.IsPrepared)
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    }

                    await Task.Delay(1000);

                    using var mediaIndexService = _container.Resolve<MediaIndexService>();
                    await mediaIndexService.UpdateIndexIfAvailable();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "An error happened inside OnStart task.");
                }
            });
        }

        protected override void OnSleep()
        {
            IsInForeground = false;

            base.OnSleep();
        }

        protected override void OnResume()
        {
            IsInForeground = true;

            base.OnResume();
            Task.Run(async () =>
            {
                try
                {
                    var playbackService = _container.Resolve<IPlaybackService>();
                    // Handle when your app resumes
                    if (playbackService.IsPrepared)
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    }

                    await Task.Delay(1000);

                    using var mediaIndexService = _container.Resolve<MediaIndexService>();
                    await mediaIndexService.UpdateIndexIfAvailable();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "An error happened inside OnResume task.");
                }      
            });

        }

    }
}
