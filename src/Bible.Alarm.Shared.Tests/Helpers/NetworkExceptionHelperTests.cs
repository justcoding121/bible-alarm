using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class NetworkExceptionHelperTests
{
    [Fact]
    public void IsNetworkFailure_ReturnsFalse_ForUnrelatedExceptions()
    {
        Assert.False(NetworkExceptionHelper.IsNetworkFailure(new InvalidOperationException()));
    }

    [Fact]
    public void IsNetworkFailure_ReturnsTrue_ForHttpRequestException()
    {
        Assert.True(NetworkExceptionHelper.IsNetworkFailure(new HttpRequestException()));
    }

    [Fact]
    public void IsNetworkFailure_ReturnsTrue_ForSocketException()
    {
        Assert.True(NetworkExceptionHelper.IsNetworkFailure(new SocketException((int)SocketError.HostUnreachable)));
    }

    [Fact]
    public void IsNetworkFailure_ReturnsTrue_ForTimeoutException()
    {
        Assert.True(NetworkExceptionHelper.IsNetworkFailure(new TimeoutException()));
    }
    [Fact]
    public void IsNetworkFailure_UnwrapsInnerExceptions()
    {
        var inner = new HttpRequestException();
        var outer = new InvalidOperationException("wrap", inner);
        Assert.True(NetworkExceptionHelper.IsNetworkFailure(outer));
    }

    [Fact]
    public void IsNetworkFailure_ReturnsTrue_ForWebException()
    {
        Assert.True(NetworkExceptionHelper.IsNetworkFailure(new WebException()));
    }

    [Fact]
    public void IsNetworkFailure_UnwrapsInnerWebException()
    {
        var inner = new WebException();
        var outer = new InvalidOperationException("wrap", inner);
        Assert.True(NetworkExceptionHelper.IsNetworkFailure(outer));
    }

    [Fact]
    public void IsRetryableForNetworkOperation_ReturnsFalse_ForWebException()
    {
        Assert.False(NetworkExceptionHelper.IsRetryableForNetworkOperation(new WebException()));
    }

    [Fact]
    public void ShouldRethrowFromCatalogRetryLoop_ReturnsTrue_ForWebException()
    {
        Assert.True(NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(new WebException()));
    }

    [Fact]
    public void IsNetworkFailure_True_WhenTaskCanceledWrapsTimeout()
    {
        var inner = new TimeoutException();
        var tce = new TaskCanceledException("timeout", inner);
        Assert.True(NetworkExceptionHelper.IsNetworkFailure(tce));
    }

    [Fact]
    public void IsRetryableForNetworkOperation_ReturnsFalse_ForCancellationAndNetworkFailures()
    {
        Assert.False(NetworkExceptionHelper.IsRetryableForNetworkOperation(new OperationCanceledException()));
        Assert.False(NetworkExceptionHelper.IsRetryableForNetworkOperation(new HttpRequestException()));
    }

    [Fact]
    public void IsRetryableForNetworkOperation_ReturnsTrue_WhenRetryingOtherFailures()
    {
        Assert.True(NetworkExceptionHelper.IsRetryableForNetworkOperation(new InvalidOperationException()));
    }

    [Fact]
    public void ShouldRethrowFromCatalogRetryLoop_ReturnsTrue_ForExplicitNetworkAndCancellationTypes()
    {
        Assert.True(NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(new OperationCanceledException()));
        Assert.True(NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(new HttpRequestException()));
        Assert.True(NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(
            new SocketException((int)SocketError.ConnectionRefused)));
    }

    [Fact]
    public void ShouldRethrowFromCatalogRetryLoop_ReturnsFalse_ForGardenVarietyFailures()
    {
        Assert.False(NetworkExceptionHelper.ShouldRethrowFromCatalogRetryLoop(new InvalidOperationException()));
    }

    [Fact]
    public async Task ThrowIfNoInternetAsync_DoesNothingWhenCheckerIsNull()
    {
        var task = NetworkExceptionHelper.ThrowIfNoInternetAsync(null);
        Assert.NotNull(task);
        await task;
        Assert.Equal(TaskStatus.RanToCompletion, task.Status);
    }

    [Fact]
    public async Task ThrowIfNoInternetAsync_NoThrowWhenAvailable()
    {
        var checker = new FakeConnectivityChecker(available: true);
        await NetworkExceptionHelper.ThrowIfNoInternetAsync(checker);
    }

    [Fact]
    public async Task ThrowIfNoInternetAsync_ThrowsHttpRequestWhenOffline()
    {
        var checker = new FakeConnectivityChecker(available: false);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            NetworkExceptionHelper.ThrowIfNoInternetAsync(checker));
    }

    private sealed class FakeConnectivityChecker(bool available) : IInternetConnectivityChecker
    {
        public Task<bool> IsInternetAvailableAsync() => Task.FromResult(available);
    }
}
