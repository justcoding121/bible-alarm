#nullable enable

using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Bootstrap;

public sealed record BootstrapOrchestratorDeps(
    IDatabaseBootstrapService DatabaseBootstrapService,
    IFluxorBootstrapService FluxorBootstrapService,
    IResourceBootstrapService ResourceBootstrapService,
    IScheduleBootstrapService ScheduleBootstrapService,
    IPlatformBootstrapService PlatformBootstrapService,
    ILanguageNameService LanguageNameService,
    ICategoryNameService CategoryNameService);
