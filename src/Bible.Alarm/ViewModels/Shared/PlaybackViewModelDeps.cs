#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared;

public sealed record PlaybackViewModelDeps(
    ILogger Logger,
    IPlaybackService PlaybackService,
    ISchedulePlaybackService SchedulePlaybackService,
    IState<PlaybackState> PlaybackState,
    IReviewPromptService ReviewPromptService,
    IAudioPlayer AudioPlayer,
    IMainThreadScheduler MainThreadScheduler);
