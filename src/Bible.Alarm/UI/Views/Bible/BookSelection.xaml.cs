using Bible.Alarm.UI.ViewHelpers;
using Bible.Alarm.ViewModels;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.UI.Views.Bible
{
    public partial class BookSelection : ContentPage
    {
        public BookSelectionViewModel ViewModel => BindingContext as BookSelectionViewModel;

        public BookSelection()
        {

            InitializeComponent();

            BackButton.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => AnimateUtils.FlickUponTouched(BackButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
            });

            Appearing += OnAppearing;
        }

        private void OnAppearing(object sender, EventArgs e)
        {
            Task.Delay(100).ContinueWith(x =>
            {
                bookListView.ScrollTo(ViewModel.SelectedBook, ScrollToPosition.Center, true);
                Appearing -= OnAppearing;

            }, ServiceProviderManager.GetService<TaskScheduler>());
        }

        protected override bool OnBackButtonPressed()
        {
            ViewModel.BackCommand.Execute(null);
            return true;
        }
    }
}