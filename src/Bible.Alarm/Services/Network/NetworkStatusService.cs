using Bible.Alarm.Services.Network.Interfaces;

namespace Bible.Alarm.Services.Network;

public sealed class NetworkStatusService : INetworkStatusService
{
    public Task<bool> IsInternetAvailable()
    {
        var current = Connectivity.NetworkAccess;

        if (current == NetworkAccess.Internet)
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

}
