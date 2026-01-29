#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.ViewModels.BiblePublications;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;

internal sealed class MusicInstrumentalSectionListLoader
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;

    public MusicInstrumentalSectionListLoader(ILogger logger, IMediaService mediaService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
    }

    public async Task<(List<BiblePublicationSectionListViewItemModel> Items, BiblePublicationSectionListViewItemModel? Selected)> LoadAsync(
        string publicationCode,
        string? selectedSectionCode,
        IFetchProgress? progress = null)
    {
        // Show progress while fetching
        progress?.SetIsVisible(true);
        progress?.UpdateProgress(0.1);

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (items, selected) = await Task.Run(async () =>
        {
            // For music publications (publications without language like "iam"), use empty string as language code
            // GetBiblePublicationSections will detect LanguageId == null and handle it appropriately
            var sectionsFromDb = await mediaService.GetBiblePublicationSections(string.Empty, publicationCode, progress);

            // If no sections found, sections might be being fetched (though music should be pre-harvested)
            // Add retry logic similar to Bible container for consistency
            if (sectionsFromDb == null || sectionsFromDb.Count == 0)
            {
                logger.Information("[MusicSectionSelection] LoadAsync: No sections found initially for publication={PublicationCode}. Sections may be being fetched, will retry...",
                    publicationCode);

                // Retry up to 5 times with increasing delays to allow fetch to complete
                // Total wait time: 2s + 3s + 4s + 5s + 6s = 20 seconds
                for (int retry = 0; retry < 5; retry++)
                {
                    // Wait before retrying (2s, 3s, 4s, 5s, 6s)
                    progress?.UpdateProgress(0.2 + (retry / 5.0) * 0.3); // 0.2 to 0.5
                    await Task.Delay(1000 * (retry + 2));

                    // Re-query to see if sections are now available
                    sectionsFromDb = await mediaService.GetBiblePublicationSections(string.Empty, publicationCode, progress);

                    if (sectionsFromDb != null && sectionsFromDb.Count > 0)
                    {
                        logger.Information("[MusicSectionSelection] LoadAsync: Found {Count} sections on retry {Retry} for publication={PublicationCode}",
                            sectionsFromDb.Count, retry + 1, publicationCode);
                        break;
                    }
                }
            }

            progress?.UpdateProgress(0.7);

            if (sectionsFromDb == null || sectionsFromDb.Count == 0)
            {
                logger.Warning("[MusicSectionSelection] LoadAsync: No sections found for publication={PublicationCode}. This publication may not be harvested yet or may not have sections.",
                    publicationCode);

                return (new List<BiblePublicationSectionListViewItemModel>(), (BiblePublicationSectionListViewItemModel?)null);
            }

            var vms = new List<BiblePublicationSectionListViewItemModel>();
            BiblePublicationSectionListViewItemModel? selected = null;

            foreach (var section in sectionsFromDb.Values)
            {
                var sectionVm = new BiblePublicationSectionListViewItemModel(section);
                vms.Add(sectionVm);

                if (!string.IsNullOrEmpty(selectedSectionCode) &&
                    section.SectionCode.Equals(selectedSectionCode, StringComparison.OrdinalIgnoreCase))
                {
                    selected = sectionVm;
                    selected.IsSelected = true;
                }
            }

            // Sort using natural sort (numeric sections as int, non-numeric as string)
            vms.Sort();

            progress?.UpdateProgress(0.9);

            return (vms, selected);
        });

        progress?.UpdateProgress(1.0);
        progress?.SetIsVisible(false);

        return (items, selected);
    }
}

