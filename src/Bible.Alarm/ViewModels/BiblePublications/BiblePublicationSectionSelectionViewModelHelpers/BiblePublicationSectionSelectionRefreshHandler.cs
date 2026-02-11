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
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetIsBusy(true);
                    ctx.SetScreenOn(true);
                }
            });

            if (ctx.IsDisposed() || ctx.IsSelectingSection())
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetCanCancelFetch(true);
                    ctx.SetShowProgress(true);
                    ctx.SetProgressText("0%");
                    ctx.SetProgressPercent(0);
                }
            });

            await ctx.Initialize(languageCode, publicationCode, progressReporter);

            if (ctx.IsSelectingSection())
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
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetSelectedSection();
                }
            });

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
        catch (OperationCanceledException)
        {
            logger.Debug("BiblePublicationSectionSelectionViewModel: Fetch cancelled by user");
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetCanCancelFetch(false);
                    ctx.SetShowProgress(false);
                }
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            ctx.SetInitCompleteFalse();
            logger.Warning(ex, "BiblePublicationSectionSelectionViewModel: Fetch failed with network error");
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetCanCancelFetch(false);
                    ctx.SetShowProgress(false);
                }
            });
            throw;
        }
        catch (Exception ex)
        {
            ctx.SetInitCompleteFalse();
            logger.Error(ex, "BiblePublicationSectionSelectionViewModel: RefreshFromStateInternal - Error during repopulation");
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetCanCancelFetch(false);
                    ctx.SetShowProgress(false);
                }
            });
            throw;
        }
    }
}
