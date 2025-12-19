#nullable enable
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Views.Music;

public partial class SongBookSelection : BaseContentPage, IDisposable
{
    private bool _isDisposed;
    private readonly SongBookSelectionViewModel viewModel;

    public SongBookSelectionViewModel? ViewModel => BindingContext as SongBookSelectionViewModel;

    public SongBookSelection(SongBookSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.viewModel = viewModel;

        // Note: We don't clear selection here because this page navigates away when an item is selected
        // The page will be disposed, so clearing selection is unnecessary and can interfere with navigation on iOS
    }

    protected override bool OnBackButtonPressed()
    {
        viewModel.BackCommand.Execute(null);
        return true;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // ViewModel was injected via constructor, so dispose it
            if (viewModel is IDisposable disposable)
            {
                disposable.Dispose();
            }
            // Clear BindingContext to break reference and allow garbage collection
            BindingContext = null;
            _isDisposed = true;
        }
    }
}
