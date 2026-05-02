#nullable enable

using Bible.Alarm.Services.Network;
using Bible.Alarm.Services.Network.Interfaces;

namespace Bible.Alarm.Tests;

public sealed class InternetConnectivityCheckerAdapterTests
{
    private sealed class StubNetworkStatus : INetworkStatusService
    {
        public bool Result { get; set; }

        public Task<bool> IsInternetAvailable() => Task.FromResult(Result);
    }

    [Fact]
    public void Constructor_throws_when_service_null()
    {
        Assert.Throws<ArgumentNullException>(() => new InternetConnectivityCheckerAdapter(null!));
    }

    [Fact]
    public async Task IsInternetAvailableAsync_delegates()
    {
        var stub = new StubNetworkStatus { Result = true };
        var sut = new InternetConnectivityCheckerAdapter(stub);

        Assert.True(await sut.IsInternetAvailableAsync());

        stub.Result = false;

        Assert.False(await sut.IsInternetAvailableAsync());
    }
}
