#nullable enable
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.ViewModels.Bible;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Views.Bible;

public partial class BibleSelection : ContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly BibleSelectionViewModel _viewModel;

    public BibleSelectionViewModel ViewModel => BindingContext as BibleSelectionViewModel;

    public BibleSelection(BibleSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Note: We don't clear selection here because this page navigates away when an item is selected
        // The page will be disposed, so clearing selection is unnecessary and can interfere with navigation on iOS
    }

    protected override bool OnBackButtonPressed()
    {
        ViewModel.BackCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (_viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _isDisposed = true;
        }
    }
}