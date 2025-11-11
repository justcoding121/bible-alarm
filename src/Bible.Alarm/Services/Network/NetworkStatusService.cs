using Bible.Alarm.Common.Interfaces.Network;

namespace Bible.Alarm.Services.Network;

public class NetworkStatusService : INetworkStatusService
{

    public Task<bool> IsInternetAvailable()
    {
        var current = Connectivity.NetworkAccess;

        if (current == NetworkAccess.Internet) return Task.FromResult(true);

        return Task.FromResult(false);
    }

    public void Dispose()
    {
    }
}