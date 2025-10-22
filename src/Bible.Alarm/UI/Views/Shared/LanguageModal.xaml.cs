using Bible.Alarm.Contracts.UI;

namespace Bible.Alarm.UI.Views.Shared;

public partial class LanguageModal : ContentPage
{
    public IListViewModel ViewModel => BindingContext as IListViewModel;

    public LanguageModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private void OnAppearing(object sender, EventArgs e)
    {
        var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        Task.Delay(100).ContinueWith(x =>
        {
            LanguageListView.ScrollTo(ViewModel.SelectedItem, ScrollToPosition.Center, true);
            Appearing -= OnAppearing;
        }, scheduler);
    }
}