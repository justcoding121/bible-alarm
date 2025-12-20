namespace Bible.Alarm.Services.Network.Interfaces;

public interface INetworkStatusService
{
    Task<bool> IsInternetAvailable();
}

