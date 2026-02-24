namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.Interfaces;

/// <summary>
/// Starts and stops the 5-minute default-schedule rotation when Android Auto is connected and no track is playing.
/// </summary>
public interface IAndroidAutoDefaultScheduleRotationService
{
    /// <summary>
    /// Starts the rotation loop. Call when Android Auto connects.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the rotation loop. Call when Android Auto disconnects.
    /// </summary>
    void Stop();
}
