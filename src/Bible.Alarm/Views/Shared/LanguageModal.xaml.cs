using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Views;

namespace Bible.Alarm.Views.Shared;

public partial class LanguageModal : ContentPage, IDisposable
{
    private bool _isDisposed;

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

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // This modal uses parent page view model, so do NOT dispose it
            _isDisposed = true;
        }
    }
}