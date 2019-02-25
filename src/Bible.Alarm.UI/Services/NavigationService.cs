﻿using System;
using System.Threading.Tasks;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.UI.Views;
using Bible.Alarm.UI.Views.Bible;
using Bible.Alarm.UI.Views.Music;
using JW.Alarm.ViewModels;
using Mvvmicro;
using Xamarin.Forms;

namespace Bible.Alarm.UI
{
    public class NavigationService : INavigationService
    {
        private readonly INavigation navigater;
        public NavigationService(INavigation navigation)
        {
            navigater = navigation;
        }

        public async Task CancelModal()
        {
            await navigater.PopModalAsync();
        }

        public async Task ShowModal(string name, object viewModel)
        {
            switch (name)
            {
                case "language":
                    await navigater.PushModalAsync(new LanguageModal());
                    break;
                default:
                    throw new ArgumentException("Modal not defined.", name);
            }
        }

        public async Task GoBack()
        {
            await navigater.PopAsync();
        }

        public async Task Navigate(object viewModel)
        {
            var vmName = viewModel.GetType().Name;
            switch (vmName)
            {
                case "ScheduleViewModel":
                    await navigater.PushAsync(new Schedule()
                    {
                        BindingContext = viewModel
                    });
                    break;
                case "MusicSelectionViewModel":
                    await navigater.PushAsync(new MusicSelection()
                    {
                        BindingContext = viewModel
                    });
                    break;
                case "SongBookSelectionViewModel":
                    await navigater.PushAsync(new SongBookSelection()
                    {
                        BindingContext = viewModel
                    });
                    break;
                case "TrackSelectionViewModel":
                    await navigater.PushAsync(new TrackSelection()
                    {
                        BindingContext = viewModel
                    });
                    break;
                case "BibleSelectionViewModel":
                    await navigater.PushAsync(new BibleSelection()
                    {
                        BindingContext = viewModel
                    });
                    break;
                case "BookSelectionViewModel":
                    await navigater.PushAsync(new BookSelection()
                    {
                        BindingContext = viewModel
                    });
                    break;
                case "ChapterSelectionViewModel":
                    await navigater.PushAsync(new ChapterSelection()
                    {
                        BindingContext = viewModel
                    });
                    break;
                default:
                    throw new ArgumentException("Invalid View Model name", vmName);
            }

        }

        public async Task NavigateToHome()
        {
            var home = IocSetup.Container.Resolve<Home>();
            await navigater.PushAsync(home);
        }
    }
}
