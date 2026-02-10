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
}
