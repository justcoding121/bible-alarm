#nullable enable
using Serilog;
using Application = Android.App.Application;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Queries the CarConnection content provider (hosted by Google's Android Auto app)
/// for real-time car connection state. This reflects actual physical connection,
/// independent of MediaBrowserService bind/unbind lifecycle (which can be delayed).
/// </summary>
internal static class CarConnectionHelper
{
    private static readonly ILogger logger = Log.ForContext(typeof(CarConnectionHelper));

    private static readonly global::Android.Net.Uri CarConnectionUri =
        global::Android.Net.Uri.Parse("content://androidx.car.app.connection")!;

    private const string CarConnectionStateColumn = "CarConnectionState";

    // Connection type constants (from androidx.car.app.connection.CarConnection)
    private const int ConnectionTypeNotConnected = 0;

    /// <summary>
    /// Returns true if the phone is currently connected to a car (Android Auto projection or native Automotive OS).
    /// Uses a synchronous content provider query (fast, local IPC).
    /// Returns false if the provider is unavailable (e.g. Android Auto app not installed).
    /// </summary>
    public static bool IsCarConnected()
    {
        try
        {
            var context = Application.Context;
            if (context?.ContentResolver == null)
            {
                logger.Debug("CarConnectionHelper: Context or ContentResolver is null");
                return false;
            }

            using var cursor = context.ContentResolver.Query(
                CarConnectionUri,
                new[] { CarConnectionStateColumn },
                null,
                null,
                null);

            if (cursor == null || !cursor.MoveToFirst())
            {
                logger.Debug("CarConnectionHelper: No response from CarConnection provider (Android Auto app may not be installed)");
                return false;
            }

            var columnIndex = cursor.GetColumnIndex(CarConnectionStateColumn);
            if (columnIndex < 0)
            {
                logger.Debug("CarConnectionHelper: CarConnectionState column not found in provider response");
                return false;
            }

            var connectionState = cursor.GetInt(columnIndex);
            var isConnected = connectionState != ConnectionTypeNotConnected;

            logger.Debug("CarConnectionHelper: ConnectionState={ConnectionState}, IsConnected={IsConnected}",
                connectionState, isConnected);

            return isConnected;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "CarConnectionHelper: Error querying CarConnection provider, treating as not connected");
            return false;
        }
    }
}
