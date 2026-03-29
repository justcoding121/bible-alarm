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
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenMusicTrackSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<Views.Music.MusicTrackSelectionModal>();
        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenBibleSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BiblePublicationSelectionModal>();
        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenSectionSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BiblePublicationSectionSelectionModal>();
        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenMusicSectionSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<Views.Music.MusicSectionSelectionModal>();
        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenBiblePublicationTrackSelectionModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<BiblePublicationTrackSelectionModal>();
        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenNumberOfTracksModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<NumberOfTracksModal>();
        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenLanguageModalAsync(INavigation navigation, object bindingContext)
    {
        ContentPage modal = bindingContext switch
        {
            BiblePublicationSelectionViewModel => serviceProvider.GetRequiredService<BiblePublicationLanguageModal>(),
            MusicPublicationSelectionViewModel => serviceProvider.GetRequiredService<MusicLanguageModal>(),
            _ => throw new ArgumentException($"Unsupported ViewModel type: {bindingContext?.GetType().Name}",
                nameof(bindingContext))
        };

        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
    }

    public async Task OpenCategoryModalAsync(INavigation navigation, object bindingContext)
    {
        var modal = serviceProvider.GetRequiredService<CategorySelectionModal>();
        modal.BindingContext = bindingContext;
        NavigationPage.SetHasNavigationBar(modal, false);
        await navigation.PushAsync(modal, animated: false);
        WindowSetupService.UpdateNavigationBarColors();
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
                NavigationPage.SetHasNavigationBar(modal, false);
                await navigation.PushAsync(modal, animated: false);
                WindowSetupService.UpdateNavigationBarColors();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening PlaybackModal");
            }
        });
    }

    public static bool IsPlaybackModalAlreadyShown(INavigation navigation)
    {
        return navigation.NavigationStack.Any(p =>
            p?.GetType() == typeof(PlaybackModal));
    }

    public async Task OpenBatteryOptimizationModalAsync(INavigation navigation, object bindingContext)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var modal = serviceProvider.GetRequiredService<AndroidAlarmPermissionModal>();
                modal.BindingContext = bindingContext;
                NavigationPage.SetHasNavigationBar(modal, false);
                await navigation.PushAsync(modal, animated: false);
                WindowSetupService.UpdateNavigationBarColors();
                logger.Information("AndroidAlarmPermissionModal opened successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening AndroidAlarmPermissionModal");
            }
        });
    }

    public async Task OpenNotificationPermissionModalAsync(INavigation navigation, object bindingContext)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var modal = serviceProvider.GetRequiredService<NotificationPermissionModal>();
                modal.BindingContext = bindingContext;
                NavigationPage.SetHasNavigationBar(modal, false);
                await navigation.PushAsync(modal, animated: false);
                WindowSetupService.UpdateNavigationBarColors();
                logger.Information("NotificationPermissionModal opened successfully");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error opening NotificationPermissionModal");
            }
        });
    }
}
