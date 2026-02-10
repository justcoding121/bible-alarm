#nullable enable
namespace Bible.Alarm.ViewModels.Interfaces;

/// <summary>
/// Optional interface for list ViewModels that expose HasFetchError (e.g. for overlay visibility).
/// On fetch failure, modal is closed and toast is shown; state is retained.
/// </summary>
public interface IHasFetchErrorListViewModel : IListViewModel
{
    bool HasFetchError { get; }
}
