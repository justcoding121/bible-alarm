#nullable enable
namespace Bible.Alarm.ViewModels.Interfaces;

/// <summary>
/// Interface for ViewModels used in list modals.
/// IsBusy should default to true. The modal code-behind (via ModalScrollHelper)
/// is responsible for setting IsBusy = false after the list is rendered.
/// ViewModels should NOT set IsBusy = false in their data loading methods.
/// </summary>
public interface IListViewModel
{
    /// <summary>
    /// The currently selected item in the list.
    /// </summary>
    object? SelectedItem { get; }

    /// <summary>
    /// Whether the ViewModel is busy loading data.
    /// Must be settable so ModalScrollHelper can clear it after rendering.
    /// ViewModels should default this to true and NOT set it to false.
    /// </summary>
    bool IsBusy { get; set; }
}

