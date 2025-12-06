using Bible.Alarm.Services.Network.Interfaces;

namespace Bible.Alarm.Services.Network;

public class NetworkStatusService : INetworkStatusService, IDisposable
{

    public Task<bool> IsInternetAvailable()
    {
        var current = Connectivity.NetworkAccess;

        if (current == NetworkAccess.Internet) return Task.FromResult(true);

        return Task.FromResult(false);
    }

    private bool _isDisposed;
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // No resources to dispose, no injected services (this service has no dependencies)
    }
}