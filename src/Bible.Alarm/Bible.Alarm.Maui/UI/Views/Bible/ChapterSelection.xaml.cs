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
    public partial class ChapterSelection : ContentPage
    {
        private readonly IContainer container;
        public ChapterSelectionViewModel ViewModel => BindingContext as ChapterSelectionViewModel;

        public ChapterSelection(IContainer container)
        {
            this.container = container;

            InitializeComponent();

            BackButton.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => AnimateUtils.FlickUponTouched(BackButton, 1500,
                ColorUtils.ToHexString(Colors.LightGray), ColorUtils.ToHexString(Colors.WhiteSmoke), 1))
            });

            this.Appearing += onAppearing;
        }

        private void onAppearing(object sender, EventArgs e)
        {
            Task.Delay(100).ContinueWith(x =>
            {
                chapterListView.ScrollTo(ViewModel.SelectedChapter, ScrollToPosition.Center, true);
                this.Appearing -= onAppearing;

            }, container.Resolve<TaskScheduler>());
        }


        protected override bool OnBackButtonPressed()
        {
            ViewModel.BackCommand.Execute(null);
            return true;
        }
    }
}