using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;

namespace Bible.Alarm.Services.Database;

public sealed class ScheduleMigrationService(
    ILogger logger)
    : IScheduleMigrationService
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task MigrateBibleGatewaySchedulesAsync()
    {
        // BibleGateway is no longer used - migration no longer needed
        // This method is kept for interface compatibility but does nothing
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
        }

        // IServiceScopeFactory is a singleton, so don't dispose it
        // No event handlers to unsubscribe
    }
}

