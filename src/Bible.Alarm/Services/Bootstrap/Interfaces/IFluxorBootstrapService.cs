#nullable enable

namespace Bible.Alarm.Services.Bootstrap.Interfaces;

/// <summary>
/// Service for initializing Fluxor store during bootstrap.
/// </summary>
public interface IFluxorBootstrapService
{
    /// <summary>
    /// Initializes the Fluxor store and registers message handlers.
    /// </summary>
    Task InitializeAsync();
}

