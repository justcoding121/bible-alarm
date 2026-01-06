#nullable enable
using Bible.Alarm.ViewModels.Bible;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Microsoft.Maui.Controls;
using Serilog;

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

/// <summary>
/// Handles modal navigation operations.
/// </summary>
public sealed class ModalNavigationHandler(ILogger logger, IServiceProvider serviceProvider)
{
    public async Task OpenMusicSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<MusicSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenSongBookSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<SongBookSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenTrackSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<TrackSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenBibleSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BibleSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenBookSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BookSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenChapterSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<ChapterSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenNumberOfChaptersModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<NumberOfChaptersModal>();
        modal.BindingContext = bindingContext;
        // Disable animation for instant appearance
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenLanguageModalAsync(INavigation navigation, object bindingContext)
    {
        ContentPage modal = bindingContext switch
        {
            // Use the appropriate modal based on the ViewModel type for compiled bindings
            BibleSelectionViewModel => serviceProvider.GetRequiredService<BibleLanguageModal>(),
            SongBookSelectionViewModel => serviceProvider.GetRequiredService<MusicLanguageModal>(),
            _ => throw new ArgumentException($"Unsupported ViewModel type: {bindingContext?.GetType().Name}",
                nameof(bindingContext))
        };

        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenAlarmModalAsync(INavigation navigation)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (IsAlarmModalAlreadyShown(navigation))
                {
                    return;
                }

                var modal = serviceProvider.GetRequiredService<AlarmModal>();
                ConfigureAlarmModal(modal);
                await navigation.PushModalAsync(modal, animated: false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening AlarmModal");
            }
        });
    }

    private static bool IsAlarmModalAlreadyShown(INavigation navigation)
    {
        var existingModal = navigation.ModalStack.LastOrDefault();
        return existingModal?.GetType() == typeof(AlarmModal) ||
               (existingModal is NavigationPage navPage && navPage.CurrentPage is AlarmModal);
    }

    private static void ConfigureAlarmModal(AlarmModal modal)
    {
        NavigationPage.SetHasNavigationBar(modal, false);
    }

    public async Task OpenBatteryOptimizationModalAsync(INavigation navigation, object bindingContext)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var modal = serviceProvider.GetRequiredService<AlarmSettingsModal>();
                modal.BindingContext = bindingContext;
                // Disable animation for instant appearance
                await navigation.PushModalAsync(modal, animated: false);
                logger.Information("AlarmSettingsModal opened successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening AlarmSettingsModal");
            }
        });
    }
}
