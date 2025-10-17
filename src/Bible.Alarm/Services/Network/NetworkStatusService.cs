using Bible.Alarm.Contracts.Network;
using System.Threading.Tasks;
using Microsoft.Maui.Networking;

namespace Bible.Alarm.Services.Network
{
    public class NetworkStatusService : INetworkStatusService
    {
        public IContainer container { get; set; }
        public NetworkStatusService(IContainer container)
        {
            this.container = container;
        }

        public Task<bool> IsInternetAvailable()
        {
            var current = Connectivity.NetworkAccess;

            if (current == NetworkAccess.Internet)
            {
                return Task.FromResult(true);
            }

            return Task.FromResult(false);

        }

        public void Dispose()
        {

        }


    }
}