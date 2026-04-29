#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Shared.Constants;
using DirectoryHelper = Bible.Alarm.Cataloger.Utility.DirectoryHelper;

namespace Bible.Alarm.Cataloger.Catalogers;

internal static class MediatorFilePersistence
{
    /// <summary>
    /// Saves mediator publication sections and tracks to disk. On-disk path remains "Dramas" for backward compatibility.
    /// </summary>
    internal static void SaveMediatorSectionsAndTracks(
        string publicationCode,
        string languageCode,
        Dictionary<string, List<MediatorTrack>> tracksBySection,
        Dictionary<string, string> sectionNames)
    {
        // Unified structure: media/Dramas/{languageCode}/{publicationCode}/sections.json
        // and media/Dramas/{languageCode}/{publicationCode}/{sectionCode}/tracks.json
        var normalizedLanguageCode = languageCode.ToUpperInvariant();
        var normalizedPublicationCode = publicationCode.ToUpperInvariant();
        var publicationDir = $"{DirectoryHelper.IndexDirectory}/media/{AppConstants.Media.BiblePublicationCategoryDramas}/{normalizedLanguageCode}/{normalizedPublicationCode}";

        if (!Directory.Exists(publicationDir))
        {
            Directory.CreateDirectory(publicationDir);
        }

        // Save sections.json with section codes and names
        // Use section name from GETPUBMEDIALINKS if available, otherwise fall back to section code
        var sections = tracksBySection.Select((kvp, index) => new
        {
            Code = kvp.Key,
            Name = sectionNames.TryGetValue(kvp.Key, out var name) && !string.IsNullOrEmpty(name) ? name : kvp.Key,
            Number = index + 1 // Sequential number for ordering
        }).OrderBy(x => x.Code).ToList();

        var sectionsJson = JsonSerializer.Serialize(sections.Select(s => new
        {
            Code = s.Code,
            Name = s.Name,
            Number = s.Number
        }));
        File.WriteAllText($"{publicationDir}/{AppConstants.ApiEndpoints.MediaIndexSectionsFileName}", sectionsJson);

        // Save tracks for each section
        foreach (var sectionEntry in tracksBySection)
        {
            var sectionCode = sectionEntry.Key;
            var tracks = sectionEntry.Value;
            var normalizedSectionCode = sectionCode.ToUpperInvariant();
            var sectionDir = $"{publicationDir}/{normalizedSectionCode}";

            if (!Directory.Exists(sectionDir))
            {
                Directory.CreateDirectory(sectionDir);
            }

            var tracksJson = JsonSerializer.Serialize(tracks.OrderBy(x => x.TrackCode));
            File.WriteAllText($"{sectionDir}/{AppConstants.ApiEndpoints.MediaIndexTracksFileName}", tracksJson);
        }
    }
}

