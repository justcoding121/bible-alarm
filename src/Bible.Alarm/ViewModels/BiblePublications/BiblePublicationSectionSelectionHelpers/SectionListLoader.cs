#nullable enable

using System;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

internal sealed class SectionListLoader
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;

    public SectionListLoader(ILogger logger, IMediaService mediaService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
    }

    internal async Task<(List<BiblePublicationSectionListViewItemModel> Items, Dictionary<string, BiblePublicationSectionListViewItemModel> Mapping)> LoadAsync(
        string languageCode,
        string publicationCode,
        string? selectedSectionCode,
        IFetchProgress? progress = null)
    {
        // Show progress while fetching
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.1);
        var sectionsFromDb = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);

        // If no sections found and this is a non-English language, sections might be being fetched.
        // Retry a few times with delays to allow the fetch to complete.
        if ((sectionsFromDb == null || sectionsFromDb.Count == 0) &&
            !string.IsNullOrEmpty(languageCode) &&
            !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            logger.Information(
                "SectionListLoader: No sections found initially for publication={PublicationCode}, language={LanguageCode}. Sections may be being fetched, will retry...",
                publicationCode,
                languageCode);

            // Retry up to 5 times with increasing delays to allow fetch to complete
            // Total wait time: 2s + 3s + 4s + 5s + 6s = 20 seconds
            for (int retry = 0; retry < 5; retry++)
            {
                // Wait before retrying (2s, 3s, 4s, 5s, 6s)
                progress?.UpdateProgress(0.2 + (retry / 5.0) * 0.3); // 0.2 to 0.5
                await Task.Delay(1000 * (retry + 2));

                // Re-query to see if sections are now available
                sectionsFromDb = await mediaService.GetBiblePublicationSections(languageCode, publicationCode, progress);

                if (sectionsFromDb != null && sectionsFromDb.Count > 0)
                {
                    logger.Information(
                        "SectionListLoader: Found {Count} sections on retry {Retry} for publication={PublicationCode}, language={LanguageCode}",
                        sectionsFromDb.Count,
                        retry + 1,
                        publicationCode,
                        languageCode);
                    break;
                }
            }
        }

        progress?.UpdateProgress(0.7);

        if (sectionsFromDb == null || sectionsFromDb.Count == 0)
        {
            logger.Warning(
                "SectionListLoader: No sections found for publication={PublicationCode}, language={LanguageCode}. This publication may not be harvested yet or may not have sections.",
                publicationCode,
                languageCode ?? "(null)");

            progress?.UpdateProgress(1.0);
            progress?.SetIsVisible(false);
            return (new List<BiblePublicationSectionListViewItemModel>(), new Dictionary<string, BiblePublicationSectionListViewItemModel>(StringComparer.OrdinalIgnoreCase));
        }

        var vms = new List<BiblePublicationSectionListViewItemModel>();
        var map = new Dictionary<string, BiblePublicationSectionListViewItemModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in sectionsFromDb.Values)
        {
            var sectionVm = new BiblePublicationSectionListViewItemModel(section);
            vms.Add(sectionVm);
            map[section.SectionCode] = sectionVm;

            if (!string.IsNullOrEmpty(selectedSectionCode) &&
                string.Equals(section.SectionCode, selectedSectionCode, StringComparison.OrdinalIgnoreCase))
            {
                sectionVm.IsSelected = true;
            }
        }

        // Sort using natural sort (numeric sections as int, non-numeric as string)
        vms.Sort();

        progress?.UpdateProgress(0.9);
        progress?.UpdateProgress(1.0);
        progress?.SetIsVisible(false);

        return (vms, map);
    }
}

