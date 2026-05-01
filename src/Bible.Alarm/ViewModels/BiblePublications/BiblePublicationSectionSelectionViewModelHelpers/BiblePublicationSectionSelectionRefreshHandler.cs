#nullable enable

using System.Net.Http;
using System.Net.Sockets;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionViewModelHelpers;

/// <summary>
/// Runs the repopulation flow for Bible publication section list: progress overlay, Initialize, selected section, and error handling.
/// </summary>
public sealed class BiblePublicationSectionSelectionRefreshHandler
{
    private readonly ILogger logger;

    public BiblePublicationSectionSelectionRefreshHandler(ILogger logger)
    {
        this.logger = logger;
    }

    public async Task RunRepopulationAsync(
        BiblePublicationSectionRefreshContext ctx,
        string languageCode,
        string publicationCode,
        IFetchProgress progressReporter)
    {
        try
        {
            await InvokeWhenActiveAsync(ctx, () => ctx.SetScreenOn(true));

            if (ctx.IsDisposed() || ctx.IsSelectingSection())
            {
                return;
            }

            await InvokeWhenActiveAsync(ctx, () => ctx.SetCanCancelFetch(true));

            await ctx.Initialize(languageCode, publicationCode, progressReporter);

            if (ctx.IsSelectingSection())
            {
                await ClearFetchChromeWhileSelectingAsync(ctx);
                return;
            }

            await InvokeWhenActiveAsync(ctx, () => ctx.SetSelectedSection());

            await ClearFetchChromeAfterNormalFlowAsync(ctx);
        }
        catch (OperationCanceledException ex)
        {
            logger.Debug(ex, "BiblePublicationSectionSelectionViewModel: Fetch cancelled by user");
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await HideProgressChromeAsync(ctx);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            ctx.SetInitCompleteFalse();
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await HideProgressChromeAsync(ctx);
            throw new InvalidOperationException(
                "BiblePublicationSectionSelectionViewModel: Fetch failed with network error",
                ex);
        }
        catch (Exception ex)
        {
            ctx.SetInitCompleteFalse();
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await HideProgressChromeAsync(ctx);
            throw new InvalidOperationException(
                "BiblePublicationSectionSelectionViewModel: RefreshFromStateInternal - Error during repopulation",
                ex);
        }
    }

    private static async Task InvokeWhenActiveAsync(BiblePublicationSectionRefreshContext ctx, Action action)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
            {
                action();
            }
        });
    }

    private static async Task ClearFetchChromeWhileSelectingAsync(BiblePublicationSectionRefreshContext ctx)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!ctx.IsDisposed())
            {
                ctx.SetCanCancelFetch(false);
                ctx.SetShowProgress(false);
                ctx.SetScreenOn(false);
            }
        });
    }

    private static async Task ClearFetchChromeAfterNormalFlowAsync(BiblePublicationSectionRefreshContext ctx)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
            {
                ctx.SetCanCancelFetch(false);
                ctx.SetShowProgress(false);
                ctx.SetScreenOn(false);
            }
        });
    }

    private static async Task HideProgressChromeAsync(BiblePublicationSectionRefreshContext ctx)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
            {
                ctx.SetCanCancelFetch(false);
                ctx.SetShowProgress(false);
            }
        });
    }
}
