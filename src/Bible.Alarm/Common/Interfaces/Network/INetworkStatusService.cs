namespace Bible.Alarm.Common.Interfaces.Network;

public interface INetworkStatusService : IDisposable
{
    Task<bool> IsInternetAvailable();
}