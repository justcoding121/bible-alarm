#nullable enable

using Bible.Alarm.Services.Network;
using Microsoft.Maui.Networking;

namespace Bible.Alarm.Tests;

public sealed class NetworkStatusServiceTests
{
    [Fact]
    public async Task IsInternetAvailable_returns_true_when_connectivity_reports_internet()
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        var sut = new NetworkStatusService();

        Assert.True(await sut.IsInternetAvailable());
    }

    [Fact]
    public async Task IsInternetAvailable_returns_false_when_connectivity_is_not_internet()
    {
        if (Connectivity.NetworkAccess == NetworkAccess.Internet)
        {
            return;
        }

        var sut = new NetworkStatusService();

        Assert.False(await sut.IsInternetAvailable());
    }
}
