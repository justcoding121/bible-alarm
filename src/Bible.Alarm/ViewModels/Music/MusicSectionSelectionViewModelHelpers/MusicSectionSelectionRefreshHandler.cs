#nullable enable

using System.Net.Http;
using System.Net.Sockets;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

public sealed class MusicSectionSelectionRefreshHandler
{
    private readonly ILogger logger;

    public MusicSectionSelectionRefreshHandler(ILogger logger)
    {
        this.logger = logger;
    }

    private static Task HideFetchChromeAsync(MusicSectionSelectionRefreshContext ctx) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
            {
                ctx.SetCanCancelFetch(false);
                ctx.SetShowProgress(false);
                ctx.SetScreenOn(false);
            }
        });

    private static Task HideFetchChromeKeepScreenAsync(MusicSectionSelectionRefreshContext ctx) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!ctx.IsDisposed())
            {
                ctx.SetCanCancelFetch(false);
                ctx.SetShowProgress(false);
            }
        });

    public async Task RunRepopulationAsync(MusicSectionSelectionRefreshContext ctx, string publicationCode, IFetchProgress progressReporter)
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
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                {
                    ctx.SetCanCancelFetch(true);
                }
            });

            await ctx.PopulateSections(publicationCode, progressReporter);

            if (ctx.IsSelectingSection())
            {
                await HideFetchChromeAsync(ctx);
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ctx.IsDisposed() && !ctx.IsSelectingSection())
                    ctx.SetSelectedSection();
            });

            await HideFetchChromeAsync(ctx);
        }
        catch (OperationCanceledException ex)
        {
            logger.Debug(ex, "[MusicSectionSelection] RefreshFromState - Fetch cancelled by user");
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await HideFetchChromeKeepScreenAsync(ctx);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await HideFetchChromeKeepScreenAsync(ctx);
            throw new InvalidOperationException(
                "[MusicSectionSelection] RefreshFromState - Network error during repopulation",
                ex);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() => ctx.SetScreenOn(false));
            await HideFetchChromeKeepScreenAsync(ctx);
            throw new InvalidOperationException(
                "[MusicSectionSelection] RefreshFromState - Error during repopulation",
                ex);
        }
    }
}
