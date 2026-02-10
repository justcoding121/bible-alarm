#nullable enable
using System.Threading;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Wraps an IFetchProgress and scales reported progress by a factor (e.g. 0.5 to map 0-100% to 0-50%).
/// </summary>
public sealed class ScaledFetchProgressAdapter : IFetchProgress
{
    private readonly IFetchProgress target;
    private readonly double scale;

    public ScaledFetchProgressAdapter(IFetchProgress target, double scale)
    {
        this.target = target ?? throw new ArgumentNullException(nameof(target));
        this.scale = Math.Max(0, Math.Min(1, scale));
    }

    public CancellationToken CancellationToken => target.CancellationToken;

    public void UpdateProgress(double progress)
    {
        var scaled = Math.Max(0, Math.Min(1, progress)) * scale;
        target.UpdateProgress(scaled);
    }

    public void UpdateProgressText(string text) => target.UpdateProgressText(text);

    public void SetIsVisible(bool isVisible) => target.SetIsVisible(isVisible);
}
