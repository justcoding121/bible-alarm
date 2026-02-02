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
            // Instrumental music publications are stored without a language FK.
            // No DB probing needed: query the without-language path directly.
            var sectionsFromDb = await mediaService.GetSectionsForPublicationWithoutLanguage(publicationCode);

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

