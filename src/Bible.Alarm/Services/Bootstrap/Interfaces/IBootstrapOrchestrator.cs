#nullable enable

namespace Bible.Alarm.Services.Bootstrap.Interfaces;

/// <summary>
/// Orchestrates the overall bootstrap process, coordinating all bootstrap services.
/// </summary>
public interface IBootstrapOrchestrator
{
    /// <summary>
    /// Verifies and initializes all services required for the application.
    /// Thread-safe: ensures bootstrap runs only once.
    /// </summary>
    /// <param name="initializeUi">Whether to send navigation messages for UI initialization.</param>
    Task VerifyServicesAsync(bool initializeUi = false);
}

