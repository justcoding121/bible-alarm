#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing MelodyMusic database operations.
/// Uses BiblePublications table filtered by Music category without LanguageId (Kingdom Melodies).
/// </summary>
public sealed class MelodyMusicService(IServiceScopeFactory scopeFactory, ILogger logger) : IMelodyMusicService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;
    private const string MusicCategoryName = "Music";

    public async Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Melody music is now stored as a sectioned publication (e.g., iam has sections like "iam-1", "iam-2")
            // Include sections and their tracks
            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Sections)
                    .ThenInclude(s => s.Tracks)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId == null 
                    && x.PublicationCode == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication == null)
            {
                return null;
            }

            // Collect all tracks from sections and direct tracks
            var allTracks = new List<BiblePublicationTrack>();
            
            // Add tracks from sections (for sectioned melody music like iam)
            if (publication.Sections != null)
            {
                foreach (var section in publication.Sections)
                {
                    if (section.Tracks != null)
                    {
                        allTracks.AddRange(section.Tracks);
                    }
                }
            }
            
            // Add direct tracks (for non-sectioned melody music, if any)
            if (publication.Tracks != null)
            {
                allTracks.AddRange(publication.Tracks);
            }

            // Create a new publication object with all tracks combined AND sections
            var publicationWithTracks = new BiblePublication
            {
                Id = publication.Id,
                PublicationCode = publication.PublicationCode,
                Name = publication.Name,
                LanguageId = publication.LanguageId,
                CategoryId = publication.CategoryId,
                IsVideo = publication.IsVideo,
                Category = publication.Category,
                Tracks = allTracks,
                Sections = publication.Sections ?? new List<BiblePublicationSection>() // Include sections so GetSampleSchedule can select a section
            };

            // MelodyMusic is a subclass of BiblePublication, so we can return the publication directly
            return new MelodyMusic { Publication = publicationWithTracks };
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting MelodyMusic with Tracks. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var melodyList = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => x.Category.CategoryName == MusicCategoryName && x.LanguageId == null)
                .ToListAsync(cancellationToken);

            // Handle potential duplicates gracefully - use first occurrence
            var result = new Dictionary<string, MelodyMusic>();
            foreach (var melody in melodyList)
            {
                if (!result.ContainsKey(melody.PublicationCode))
                {
                    result[melody.PublicationCode] = new MelodyMusic { Publication = melody };
                }
                else
                {
                    logger.Warning("Duplicate MelodyMusic entry found. Code={Code}", melody.PublicationCode);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting all MelodyMusic");
            throw;
        }
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Melody music is now stored as a sectioned publication (e.g., iam has sections like "iam-1", "iam-2")
            // Include sections and their tracks (with UrlParams for OriginalTrackCode)
            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Sections)
                    .ThenInclude(s => s.Tracks)
                    .ThenInclude(t => t.UrlParams)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId == null 
                    && x.PublicationCode == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication == null)
            {
                return new SortedDictionary<int, MusicTrack>();
            }

            // Collect all tracks from sections and direct tracks
            var allTracks = new List<BiblePublicationTrack>();
            
            // Add tracks from sections (for sectioned melody music like iam)
            if (publication.Sections != null)
            {
                foreach (var section in publication.Sections)
                {
                    if (section.Tracks != null)
                    {
                        allTracks.AddRange(section.Tracks);
                    }
                }
            }
            
            // Add direct tracks (for non-sectioned melody music, if any)
            if (publication.Tracks != null)
            {
                allTracks.AddRange(publication.Tracks);
            }

            // Map BiblePublicationTrack to MusicTrack
            // For sectioned melody (e.g. iam): set DownloadCode = section code (e.g. iam-1) and OriginalTrackCode from UrlParams so LookUpPath uses pub=sectionCode.
            // Handle duplicate track codes by taking the first occurrence
            var musicTracks = allTracks
                .OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b)))
                .GroupBy(t => t.TrackCode)
                .Select(g => g.First())
                .Select(t => MapBiblePublicationTrackToMusicTrack(t))
                .ToDictionary(x => x.Number, x => x);
            
            return new SortedDictionary<int, MusicTrack>(musicTracks);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting MelodyMusic tracks. PublicationCode={PublicationCode}", publicationCode);
            throw;
        }
    }

    public async Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Get the publication with sections and track UrlParams
            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Sections)
                    .ThenInclude(s => s.Tracks)
                    .ThenInclude(t => t.UrlParams)
                .Where(x => x.Category.CategoryName == MusicCategoryName 
                    && x.LanguageId == null 
                    && x.PublicationCode == publicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication == null)
            {
                return new SortedDictionary<int, MusicTrack>();
            }

            // Find the section by section code
            var section = publication.Sections?.FirstOrDefault(s => 
                s.SectionCode != null && 
                s.SectionCode.Equals(sectionCode, StringComparison.OrdinalIgnoreCase));

            if (section == null || section.Tracks == null || section.Tracks.Count == 0)
            {
                return new SortedDictionary<int, MusicTrack>();
            }

            // Map BiblePublicationTrack to MusicTrack (use sectionCode so LookUpPath uses pub=sectionCode)
            var musicTracks = section.Tracks
                .OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b)))
                .Select(t => MapBiblePublicationTrackToMusicTrack(t, sectionCode))
                .ToDictionary(x => x.Number, x => x);

            return new SortedDictionary<int, MusicTrack>(musicTracks);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting MelodyMusic tracks by section. PublicationCode={PublicationCode}, SectionCode={SectionCode}", 
                publicationCode, sectionCode);
            throw;
        }
    }

    /// <summary>
    /// Maps a BiblePublicationTrack to MusicTrack, setting DownloadCode (section/disc code) and OriginalTrackCode
    /// from Section and UrlParams so that LookUpPath uses pub=sectionCode (e.g. iam-1) for melody music.
    /// </summary>
    /// <param name="sectionCodeFallback">When provided (e.g. from GetTracksBySectionCodeAsync), used if track.Section is not populated.</param>
    private static MusicTrack MapBiblePublicationTrackToMusicTrack(BiblePublicationTrack t, string? sectionCodeFallback = null)
    {
        var downloadCode = t.Section?.SectionCode ?? sectionCodeFallback;
        int? originalTrackCode = null;
        if (t.UrlParams != null)
        {
            var trackParam = t.UrlParams.FirstOrDefault(p => p.Key == "track");
            if (trackParam != null && int.TryParse(trackParam.Value, out var trackCode))
            {
                originalTrackCode = trackCode;
            }
        }

        // Parse TrackCode as int for MusicTrack.Number (for backward compatibility with MusicTrack model)
        var trackNumber = int.TryParse(t.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var num) ? num : 0;
        return new MusicTrack
        {
            Number = trackNumber,
            Title = t.Title,
            Url = string.Empty,
            LookUpPath = string.Empty,
            DownloadCode = downloadCode,
            OriginalTrackCode = originalTrackCode
        };
    }

    public async Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default)
    {
        // URLs are now computed on-demand, no need to store them
        // This method is kept for backward compatibility but does nothing
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
    }
}

