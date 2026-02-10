#nullable enable

using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Services.Network;

/// <summary>
/// Adapts INetworkStatusService to IInternetConnectivityChecker for Shared layer.
/// Used when fetch has been decided, right before starting the network call.
/// </summary>
public sealed class InternetConnectivityCheckerAdapter : IInternetConnectivityChecker
{
    private readonly INetworkStatusService networkStatusService;

    public InternetConnectivityCheckerAdapter(INetworkStatusService networkStatusService)
    {
        this.networkStatusService = networkStatusService ?? throw new System.ArgumentNullException(nameof(networkStatusService));
    }

    public Task<bool> IsInternetAvailableAsync() => networkStatusService.IsInternetAvailable();
}
