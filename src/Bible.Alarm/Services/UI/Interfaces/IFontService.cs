namespace Bible.Alarm.Services.UI.Interfaces;

/// <summary>
/// Service for managing scalable font sizes based on device metrics
/// </summary>
public interface IFontService
{
    /// <summary>
    /// Base font size (12 points) scaled by device density
    /// </summary>
    double StandardFontSize { get; }

    /// <summary>
    /// Header font size (18 points) scaled by device density
    /// </summary>
    double HeaderFontSize { get; }

    /// <summary>
    /// Small font size (10 points) scaled by device density
    /// </summary>
    double SmallFontSize { get; }

    /// <summary>
    /// Medium font size (14 points) scaled by device density
    /// </summary>
    double MediumFontSize { get; }

    /// <summary>
    /// Large font size (16 points) scaled by device density
    /// </summary>
    double LargeFontSize { get; }

    /// <summary>
    /// Title font size (20 points) scaled by device density
    /// </summary>
    double TitleFontSize { get; }

    /// <summary>
    /// Gets a scaled font size based on a base size in points
    /// </summary>
    /// <param name="baseSizeInPoints">Base font size in points</param>
    /// <returns>Scaled font size based on device density</returns>
    double GetScaledFontSize(double baseSizeInPoints);
}

