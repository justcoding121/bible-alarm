#nullable enable
namespace Bible.Alarm.ViewModels.Interfaces;

/// <summary>
/// Optional interface for list ViewModels that support fetch error state with retry.
/// When a fetch fails, the ViewModel sets HasFetchError = true; ModalScrollHelper
/// keeps the modal open and the overlay shows Retry + Cancel.
/// </summary>
public interface IHasFetchErrorListViewModel : IListViewModel
{
    /// <summary>
    /// True when a network fetch failed; overlay shows retry button.
    /// </summary>
    bool HasFetchError { get; }
}
