﻿using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI;
using Bible.Alarm.ViewModels;
using MediaManager;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bible.Alarm
{
    public partial class App : Application
    {
        private readonly IContainer container;

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

                container.Register<TaskScheduler>(x => taskScheduler);
                container.RegisterSingleton(x => navigationPage);
                container.RegisterSingleton(x => navigationPage.Navigation);
                container.RegisterSingleton<INavigationService>(x => new NavigationService(container, navigationPage.Navigation));

                MainPage = navigationPage;

                MainPage.SetValue(NavigationPage.BarBackgroundColorProperty, Color.SlateBlue);
                MainPage.SetValue(NavigationPage.BarTextColorProperty, Color.White);

                Func<Task> homePageSetter = async () =>
                {
                    var homePage = new Home();
                    homePage.BindingContext = container.Resolve<HomeViewModel>();
                    await navigationPage.Navigation.PushAsync(homePage);
                };

                if (Device.RuntimePlatform == Device.iOS)
                {
                    homePageSetter().Wait();
                }

                Task.Delay(100).ContinueWith(async (a) =>
                {
                    if (Device.RuntimePlatform == Device.Android)
                    {
                        await homePageSetter();
                    }

                }, taskScheduler)
                .ContinueWith(x =>
                {
                    var mediaManager = container.Resolve<IMediaManager>();
                    if (mediaManager.IsPrepared())
                    {
                        Messenger<object>.Publish(MvvmMessages.ShowAlarmModal, container.Resolve<AlarmViewModal>());
                    }
                });
            }
        }

        protected override void OnStart()
        {
            Task.Run(async () =>
            {
                var navigationService = container.Resolve<INavigationService>();
                // Handle when your app starts  
                await navigationService.NavigateToHome();

                var mediaManager = container.Resolve<IMediaManager>();
                if (mediaManager.IsPrepared())
                {
                    Messenger<object>.Publish(MvvmMessages.ShowAlarmModal, container.Resolve<AlarmViewModal>());
                }

            });
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            Task.Run(() =>
            {
                var mediaManager = container.Resolve<IMediaManager>();
                // Handle when your app resumes
                if (mediaManager.IsPrepared()
                    && (Device.RuntimePlatform != Device.iOS
                        || mediaManager.Duration.TotalMilliseconds > 0))
                {
                    Messenger<object>.Publish(MvvmMessages.ShowAlarmModal, container.Resolve<AlarmViewModal>());
                }

            });
        }

    }
}
