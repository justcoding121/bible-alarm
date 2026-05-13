#nullable enable
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.Views.Bible;
using Bible.Alarm.Views.General;
using Bible.Alarm.Views.Music;
using Bible.Alarm.Views.Schedule;
using Bible.Alarm.Views.Shared;
using Serilog;
#if ANDROID
using Bible.Alarm.Platforms.Android.Services.UI.Interfaces;
#endif

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
        var kind = LanguageModalBindingClassifier.ClassifyBindingContextRuntimeType(bindingContext.GetType());
        ContentPage modal = kind switch
        {
            LanguageModalKind.Bible => serviceProvider.GetRequiredService<BiblePublicationLanguageModal>(),
            LanguageModalKind.Music => serviceProvider.GetRequiredService<MusicLanguageModal>(),
            _ => throw new InvalidOperationException($"Unhandled language modal classification: {kind}"),
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

    private const uint PlaybackModalAnimationDurationMs = 300;

    public async Task OpenPlaybackModalAsync(INavigation navigation, bool animated = false)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (IsPlaybackModalAlreadyShown(navigation))
                {
                    return;
                }

                var modal = serviceProvider.GetRequiredService<PlaybackModal>();
                NavigationPage.SetHasNavigationBar(modal, false);

                if (animated)
                {
                    modal.TranslationY = 2000;
                }

                await navigation.PushAsync(modal, animated: false);
                WindowSetupService.UpdateNavigationBarColors();

#if ANDROID
                var barHost = serviceProvider.GetService<IAndroidMiniPlaybackBarHost>();
                barHost?.SetPlaybackModalActive(true);
#endif

                if (animated)
                {
                    var startY = modal.Height > 0 ? modal.Height : 2000;
                    modal.TranslationY = startY;
                    await modal.TranslateToAsync(0, 0, PlaybackModalAnimationDurationMs, Easing.CubicOut);
                }
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("PlaybackModal push failed", ex);
        }
    }

    public static bool IsPlaybackModalAlreadyShown(INavigation navigation)
    {
        return NavigationStackTypeInspector.ContainsPageWithRuntimeType(navigation.NavigationStack, typeof(PlaybackModal));
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
