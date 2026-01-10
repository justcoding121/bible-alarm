#nullable enable
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleContainerService : IScheduleContainerService
{
    private readonly ILogger logger;

    public ScheduleContainerService(ILogger logger)
    {
        this.logger = logger;
    }

    public async Task InitializeContainersAsync(
        IServiceProvider serviceProvider,
        Action<BibleSelectionContainerViewModel, MusicSelectionContainerViewModel, NumberOfTrackContainerViewModel, ScheduleDetailsContainerViewModel> onContainersReady)
    {
        try
        {
            // Create containers on background thread with yields to allow UI to breathe
            // This prevents the spinner from freezing during container creation
            var bibleSelection = await Task.Run(() => 
                serviceProvider.GetRequiredService<BibleSelectionContainerViewModel>());
            
            var musicSelection = await Task.Run(() => 
                serviceProvider.GetRequiredService<MusicSelectionContainerViewModel>());
            
            var numberOfTrack = await Task.Run(() => 
                serviceProvider.GetRequiredService<NumberOfTrackContainerViewModel>());
            
            var scheduleDetails = await Task.Run(() => 
                serviceProvider.GetRequiredService<ScheduleDetailsContainerViewModel>());

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                onContainersReady(
                    bibleSelection,
                    musicSelection,
                    numberOfTrack,
                    scheduleDetails);
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error initializing container view models");
        }
    }
}

