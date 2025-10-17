using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI;
using Bible.Alarm.ViewModels;
using MediaManager;
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
        private readonly IContainer container;

        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        public static bool IsInForeground { get; set; } = false;

        public App(IContainer container)
        {
            this.container = container;
            init();
        }

        private void init()
        {
            InitializeComponent();

            if (container.RegisteredTypes.Any(x => x == typeof(NavigationPage)))
            {
                MainPage = container.Resolve<NavigationPage>();
            }
            else
            {
                var navigationPage = new NavigationPage();

                var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

                container.Register(x => taskScheduler);
                container.RegisterSingleton(x => navigationPage);
                container.RegisterSingleton(x => navigationPage.Navigation);
                container.RegisterSingleton<INavigationService>(x => new NavigationService(container, navigationPage.Navigation));

                MainPage = navigationPage;

                MainPage.SetValue(NavigationPage.BarBackgroundColorProperty, Colors.SlateBlue);
                MainPage.SetValue(NavigationPage.BarTextColorProperty, Colors.White);

                Func<Task> homePageSetter = async () =>
                {
                    var homePage = new Home();
                    homePage.BindingContext = container.Resolve<HomeViewModel>();
                    await navigationPage.Navigation.PushAsync(homePage);
                };

                if (CurrentDevice.RuntimePlatform != Device.Android)
                {
                    homePageSetter().Wait();
                }

                Task.Delay(100).ContinueWith(async (a) =>
                {
                    if (CurrentDevice.RuntimePlatform == Device.Android)
                    {
                        await homePageSetter();
                    }

                }, taskScheduler)
                .ContinueWith(x =>
                {
                    var mediaManager = container.Resolve<IMediaManager>();

                    if (mediaManager.IsPreparedEx())
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
                    var navigationService = container.Resolve<INavigationService>();
                    // Handle when your app starts  
                    await navigationService.NavigateToHome();

                    var mediaManager = container.Resolve<IMediaManager>();

                    if (mediaManager.IsPreparedEx())
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    }

                    await Task.Delay(1000);

                    using var mediaIndexService = container.Resolve<MediaIndexService>();
                    await mediaIndexService.UpdateIndexIfAvailable();
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened inside OnStart task.");
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
                    var mediaManager = container.Resolve<IMediaManager>();
                    // Handle when your app resumes
                    if (mediaManager.IsPreparedEx())
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    }

                    await Task.Delay(1000);

                    using var mediaIndexService = container.Resolve<MediaIndexService>();
                    await mediaIndexService.UpdateIndexIfAvailable();
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened inside OnResume task.");
                }      
            });

        }

    }
}
