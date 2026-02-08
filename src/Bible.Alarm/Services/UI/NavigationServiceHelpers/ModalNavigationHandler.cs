#nullable enable
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Serilog;

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

/// <summary>
/// Handles modal navigation operations.
/// </summary>
public sealed class ModalNavigationHandler(ILogger logger, IServiceProvider serviceProvider)
{
    public async Task OpenSongPublicationSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<MusicPublicationSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenMusicTrackSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<Views.Music.MusicTrackSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenBibleSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BiblePublicationSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenSectionSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BiblePublicationSectionSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenMusicSectionSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<Views.Music.MusicSectionSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenBiblePublicationTrackSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BiblePublicationTrackSelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenNumberOfTracksModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<NumberOfTracksModal>();
        modal.BindingContext = bindingContext;
        // Disable animation for instant appearance
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenLanguageModalAsync(INavigation navigation, object bindingContext)
    {
        ContentPage modal = bindingContext switch
        {
            // Use the appropriate modal based on the ViewModel type for compiled bindings
            BiblePublicationSelectionViewModel => serviceProvider.GetRequiredService<BiblePublicationLanguageModal>(),
            MusicPublicationSelectionViewModel => serviceProvider.GetRequiredService<MusicLanguageModal>(),
            _ => throw new ArgumentException($"Unsupported ViewModel type: {bindingContext?.GetType().Name}",
                nameof(bindingContext))
        };

        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public async Task OpenCategoryModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<CategorySelectionModal>();
        modal.BindingContext = bindingContext;
        await navigation.PushModalAsync(modal, animated: false);
    }

    public Task OpenPlaybackModalAsync(INavigation navigation) =>
        OpenPlaybackModalAsync(navigation, revealHomeBehindModalOnLoad: true);

    public async Task OpenPlaybackModalAsync(INavigation navigation, bool revealHomeBehindModalOnLoad)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (IsPlaybackModalAlreadyShown(navigation))
                {
                    return;
                }

                var modal = serviceProvider.GetRequiredService<PlaybackModal>();
                if (modal.ViewModel != null)
                {
                    modal.ViewModel.RevealHomeBehindModalOnLoad = revealHomeBehindModalOnLoad;
                }
                ConfigurePlaybackModal(modal);
                await navigation.PushModalAsync(modal, animated: false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening PlaybackModal");
            }
        });
    }

    private static bool IsPlaybackModalAlreadyShown(INavigation navigation)
    {
        var existingModal = navigation.ModalStack.LastOrDefault();
        return existingModal?.GetType() == typeof(PlaybackModal) ||
               (existingModal is NavigationPage navPage && navPage.CurrentPage is PlaybackModal);
    }

    private static void ConfigurePlaybackModal(PlaybackModal modal)
    {
        NavigationPage.SetHasNavigationBar(modal, false);
    }

    public async Task OpenBatteryOptimizationModalAsync(INavigation navigation, object bindingContext)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var modal = serviceProvider.GetRequiredService<AndroidAlarmPermissionModal>();
                modal.BindingContext = bindingContext;
                // Disable animation for instant appearance
                await navigation.PushModalAsync(modal, animated: false);
                logger.Information("AndroidAlarmPermissionModal opened successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening AndroidAlarmPermissionModal");
            }
        });
    }

    public async Task OpenIOSNotificationPermissionModalAsync(INavigation navigation, object bindingContext)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var modal = serviceProvider.GetRequiredService<IOSNotificationPermissionModal>();
                modal.BindingContext = bindingContext;
                // Disable animation for instant appearance
                await navigation.PushModalAsync(modal, animated: false);
                logger.Information("IOSNotificationPermissionModal opened successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening IOSNotificationPermissionModal");
            }
        });
    }
}
