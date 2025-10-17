
using Bible.Alarm.Contracts.UI;
using Mvvmicro;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.UI.Views
{
    public partial class LanguageModal : ContentPage
    {
        public IListViewModel ViewModel => BindingContext as IListViewModel;

        public LanguageModal()
        {
            InitializeComponent();
            this.Appearing += OnAppearing;
        }

        private void OnAppearing(object sender, EventArgs e)
        {
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Delay(100).ContinueWith(x =>
            {
                LanguageListView.ScrollTo(ViewModel.SelectedItem, ScrollToPosition.Center, true);
                this.Appearing -= OnAppearing;

            }, scheduler);
        }
    }
}