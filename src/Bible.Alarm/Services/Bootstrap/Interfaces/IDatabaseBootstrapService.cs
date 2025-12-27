#nullable enable

namespace Bible.Alarm.Services.Bootstrap.Interfaces;

/// <summary>
/// Service for initializing and migrating databases during bootstrap.
/// </summary>
public interface IDatabaseBootstrapService
{
    /// <summary>
    /// Initializes the Schedule and Media databases, applying migrations if needed.
    /// </summary>
    Task InitializeAsync();
}

