using Bible.Alarm.ViewModels.Interfaces;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Views;

namespace Bible.Alarm.Views.Shared;

public partial class BibleLanguageModal : ContentPage, IDisposable
{
    private bool _isDisposed;

    public IListViewModel ViewModel => BindingContext as IListViewModel;

    private bool _isClearingSelection;

    public BibleLanguageModal()
    {
        InitializeComponent();
        
        // Clear selection after SelectionChanged fires to allow command to execute first
        LanguageCollectionView.SelectionChanged += (sender, e) =>
        {
            // Don't clear if we're already clearing or if selection is being cleared (CurrentSelection is empty or null)
            if (_isClearingSelection || e?.CurrentSelection == null || e.CurrentSelection.Count == 0)
            {
                return;
            }
            
            // Clear selection after a short delay to allow command to execute
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_isDisposed && LanguageCollectionView != null)
                    {
                        _isClearingSelection = true;
                        LanguageCollectionView.SelectedItem = null;
                        _isClearingSelection = false;
                    }
                });
            });
        };
        
        Appearing += OnAppearing;
    }

    private async void OnAppearing(object sender, EventArgs e)
    {
        Appearing -= OnAppearing;
        
        if (ViewModel?.SelectedItem != null && LanguageCollectionView != null)
        {
            await CollectionViewHelper.ScrollToWhenReadyAsync(LanguageCollectionView, ViewModel.SelectedItem);
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

