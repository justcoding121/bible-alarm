namespace Bible.Alarm.Services.Network.Interfaces;

public interface INetworkStatusService : IDisposable
{
    Task<bool> IsInternetAvailable();
}

