﻿using JW.Alarm.Core.UWP.Views.Bible;
using JW.Alarm.Core.UWP.Views.Music;
using JW.Alarm.ViewModels;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace JW.Alarm.Core.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScheduleView : Page
    {
        public ScheduleViewModel ViewModel => DataContext as ScheduleViewModel;

        public ScheduleView()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            DataContext = e.Parameter as ScheduleViewModel;
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private async void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            if (await ViewModel.SaveAsync())
            {
                Frame.GoBack();
            }
        }

        private async void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.DeleteAsync();
            Frame.GoBack();
        }

        private void Button_Day_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            //switch (button.Name)
            //{
            //    case "Button_Sunday":
            //        ViewModel.Toggle(DayOfWeek.Sunday);
            //        break;
            //    case "Button_Monday":
            //        ViewModel.Toggle(DayOfWeek.Monday);
            //        break;
            //    case "Button_Tuesday":
            //        ViewModel.Toggle(DayOfWeek.Tuesday);
            //        break;
            //    case "Button_Wednesday":
            //        ViewModel.Toggle(DayOfWeek.Wednesday);
            //        break;
            //    case "Button_Thursday":
            //        ViewModel.Toggle(DayOfWeek.Thursday);
            //        break;
            //    case "Button_Friday":
            //        ViewModel.Toggle(DayOfWeek.Friday);
            //        break;
            //    case "Button_Saturday":
            //        ViewModel.Toggle(DayOfWeek.Saturday);
            //        break;
            //}

            throw new NotImplementedException();

        }

        private void Music_Btn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MusicSelection), ViewModel.GetMusicSelectionViewModel());
        }

        private void Bible_Btn_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BibleSelection), ViewModel.GetBibleSelectionViewModel());
        }
    }
}
