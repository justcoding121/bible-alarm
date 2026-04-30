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
        catch (OperationCanceledException ex)
        {
            logger.Debug(ex, "BiblePublicationSectionSelectionViewModel: Fetch cancelled by user");
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
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetCanCancelFetch(false);
                    ctx.SetShowProgress(false);
                }
            });
            throw new InvalidOperationException(
                "BiblePublicationSectionSelectionViewModel: Fetch failed with network error",
                ex);
        }
        catch (Exception ex)
        {
            ctx.SetInitCompleteFalse();
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetCanCancelFetch(false);
                    ctx.SetShowProgress(false);
                }
            });
            throw new InvalidOperationException(
                "BiblePublicationSectionSelectionViewModel: RefreshFromStateInternal - Error during repopulation",
                ex);
        }
    }
}
