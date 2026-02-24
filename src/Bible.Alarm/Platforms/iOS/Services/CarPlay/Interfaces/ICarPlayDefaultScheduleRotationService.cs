namespace Bible.Alarm.Platforms.iOS.Services.CarPlay.Interfaces;

/// <summary>
/// Starts and stops the 5-minute default-schedule rotation when CarPlay is connected and no track is playing.
/// </summary>
public interface ICarPlayDefaultScheduleRotationService
{
    /// <summary>
    /// Starts the rotation loop. Call when CarPlay connects.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the rotation loop. Call when CarPlay disconnects.
    /// </summary>
    void Stop();
}
