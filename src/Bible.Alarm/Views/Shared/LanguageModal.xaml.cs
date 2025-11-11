using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Views.Shared;

public partial class LanguageModal : ContentPage
{
    public IListViewModel ViewModel => BindingContext as IListViewModel;

    public LanguageModal()
    {
        InitializeComponent();
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;
        
        if (ViewModel?.SelectedItem != null && LanguageListView != null)
        {
            await ListViewHelper.ScrollToWhenReadyAsync(LanguageListView, ViewModel.SelectedItem);
        }
    }
}