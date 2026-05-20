#nullable enable

using Bible.Alarm.Services.Network;
using Microsoft.Maui.Networking;

namespace Bible.Alarm.Tests;

public sealed class NetworkStatusServiceTests
{
    [Fact]
    public async Task IsInternetAvailable_returns_resolved_boolean()
    {
        var sut = new NetworkStatusService();
        var task = sut.IsInternetAvailable();
        Assert.True(task.IsCompletedSuccessfully);
        _ = await task;
    }

    [Fact]
    public async Task IsInternetAvailable_matches_connectivity_network_access()
    {
        var sut = new NetworkStatusService();

        var result = await sut.IsInternetAvailable();

        var expected = Connectivity.NetworkAccess == NetworkAccess.Internet;
        Assert.Equal(expected, result);
    }
}
