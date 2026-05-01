#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper to detect network-related exceptions (for rethrowing vs swallowing).
/// </summary>
public static class NetworkExceptionHelper
{
    /// <summary>
    /// Returns true if the exception indicates a network failure (no internet, DNS failure, timeout, etc.).
    /// </summary>
    public static bool IsNetworkFailure(Exception ex)
    {
        if (ex is HttpRequestException
            or WebException
            or SocketException
            or TimeoutException)
        {
            return true;
        }

        if (ex is TaskCanceledException tce && tce.InnerException is TimeoutException)
        {
            return true;
        }

        if (ex.InnerException != null && IsNetworkFailure(ex.InnerException))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if a retry is appropriate for a network operation.
    /// Returns false for network connectivity failures and cancellation - do not retry in those cases.
    /// Use in Polly retry policies and manual retry loops to fail fast when network is unavailable.
    /// </summary>
    public static bool IsRetryableForNetworkOperation(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return false;
        }

        if (IsNetworkFailure(ex))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// True when a catalog/media retry loop should stop retrying and propagate the exception immediately.
    /// </summary>
    public static bool ShouldRethrowFromCatalogRetryLoop(Exception ex)
    {
        return ex is OperationCanceledException
            or HttpRequestException
            or SocketException
            || IsNetworkFailure(ex);
    }

    /// <summary>
    /// Throws HttpRequestException if checker indicates no internet.
    /// Use when fetch has been decided, right before starting the network call.
    /// </summary>
    public static async Task ThrowIfNoInternetAsync(IInternetConnectivityChecker? checker)
    {
        if (checker == null)
            return;
        if (!await checker.IsInternetAvailableAsync())
            throw new HttpRequestException("No internet connection available.");
    }
}
