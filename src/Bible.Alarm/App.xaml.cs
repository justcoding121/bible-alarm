using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI;
using Bible.Alarm.ViewModels;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Devices;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm
{
    public partial class App : Application
    {
        private static readonly ILogger Logger = Log.ForContext<App>();

        public static bool IsInForeground { get; set; } = false;

        public App()
        {
            Init();
        }

        private void Init()
        {
            InitializeComponent();

            var navigationPage = new NavigationPage();
            var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var navigationService = new NavigationService(navigationPage.Navigation);

            Windows[0].Page = navigationPage;

            Windows[0].Page.SetValue(NavigationPage.BarBackgroundColorProperty, Colors.SlateBlue);
            Windows[0].Page.SetValue(NavigationPage.BarTextColorProperty, Colors.White);

            Func<Task> homePageSetter = async () =>
            {
                var homePage = new Home();
                homePage.BindingContext = ServiceProviderManager.GetService<HomeViewModel>();
                await navigationPage.Navigation.PushAsync(homePage);
            };

            if (DeviceInfo.Platform != DevicePlatform.Android)
            {
                homePageSetter().Wait();
            }

            Task.Delay(100).ContinueWith(async (a) =>
            {
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    await homePageSetter();
                }

            }, taskScheduler)
            .ContinueWith(x =>
            {
                var playbackService = ServiceProviderManager.GetService<IPlaybackService>();

                if (playbackService.IsPrepared)
                {
                    Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                }
            });
        }

        protected override void OnStart()
        {
            IsInForeground = true;

            base.OnStart();
            Task.Run(async () =>
            {
                try
                {
                    var navigationService = ServiceProviderManager.GetService<INavigationService>();
                    // Handle when your app starts  
                    await navigationService.NavigateToHome();

                    var playbackService = ServiceProviderManager.GetService<IPlaybackService>();

                    if (playbackService.IsPrepared)
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    }

                    await Task.Delay(1000);

                    using var mediaIndexService = ServiceProviderManager.GetService<MediaIndexService>();
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
                    var playbackService = ServiceProviderManager.GetService<IPlaybackService>();
                    // Handle when your app resumes
                    if (playbackService.IsPrepared)
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal);
                    }

                    await Task.Delay(1000);

                    using var mediaIndexService = ServiceProviderManager.GetService<MediaIndexService>();
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
