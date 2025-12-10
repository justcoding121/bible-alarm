#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing MelodyMusic database operations.
/// </summary>
public class MelodyMusicService(IServiceScopeFactory scopeFactory, ILogger logger) : IMelodyMusicService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isDisposed;

    public async Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            return await dbContext.MelodyMusic
                .AsNoTracking()
                .Include(x => x.Tracks)
                .Where(x => x.Code == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting MelodyMusic with Tracks. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            return await dbContext.MelodyMusic
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Code, x => x, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting all MelodyMusic");
            throw;
        }
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            var tracks = await dbContext.MelodyMusic
                .AsNoTracking()
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);
            
            return new SortedDictionary<int, MusicTrack>(tracks.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting MelodyMusic tracks. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task UpdateTrackUrlAsync(string publicationCode, int trackNumber, string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            var track = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (track?.Source != null)
            {
                track.Source.Url = url;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating MelodyMusic track URL. PublicationCode={PublicationCode}, TrackNumber={TrackNumber}", 
                publicationCode, trackNumber);
            throw;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error during cancellation token source disposal in MelodyMusicService");
        }
    }
}

