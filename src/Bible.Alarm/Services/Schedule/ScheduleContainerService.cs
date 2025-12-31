#nullable enable
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.ViewModels.Schedule;
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
        Action<BibleSelectionContainerViewModel, MusicSelectionContainerViewModel, ChaptersSelectionContainerViewModel, ScheduleDetailsContainerViewModel> onContainersReady)
    {
        try
        {
            var containers = await Task.Run(() => new
            {
                BibleSelection = serviceProvider.GetRequiredService<BibleSelectionContainerViewModel>(),
                MusicSelection = serviceProvider.GetRequiredService<MusicSelectionContainerViewModel>(),
                ChaptersSelection = serviceProvider.GetRequiredService<ChaptersSelectionContainerViewModel>(),
                ScheduleDetails = serviceProvider.GetRequiredService<ScheduleDetailsContainerViewModel>()
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                onContainersReady(
                    containers.BibleSelection,
                    containers.MusicSelection,
                    containers.ChaptersSelection,
                    containers.ScheduleDetails);
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error initializing container view models");
        }
    }
}

