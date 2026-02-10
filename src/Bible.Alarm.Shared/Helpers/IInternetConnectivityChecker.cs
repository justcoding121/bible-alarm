#nullable enable

using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Checks if internet is available before starting a network fetch.
/// Implement in the app layer (e.g., delegates to INetworkStatusService).
/// When null/not provided, the check is skipped (e.g., in tests).
/// </summary>
public interface IInternetConnectivityChecker
{
    Task<bool> IsInternetAvailableAsync();
}
