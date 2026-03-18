#nullable enable
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Services.Media.Interfaces;

/// <summary>
/// Re-fetches all tracks for a no-language melody disc section (e.g. iam / iam-1) from GETPUBMEDIALINKS.
/// </summary>
public interface IMelodyDiscTracksApiRefresher
{
    /// <summary>
    /// Deletes existing tracks for the disc section and repopulates from the API.
    /// </summary>
    Task<bool> ReplaceDiscSectionTracksFromApiAsync(
        string publicationCode,
        string discSectionCode,
        CancellationToken cancellationToken = default);
}
