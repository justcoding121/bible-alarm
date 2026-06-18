#nullable enable

using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Models.BiblePublications;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class DbSeederTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    private sealed class InMemoryScopeFactory(string databaseName) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var services = new ServiceCollection();
            services.AddDbContext<MediaDbContext>(options => options.UseInMemoryDatabase(databaseName));
            return services.BuildServiceProvider().CreateScope();
        }
    }

    private static DbSeeder CreateSut(string databaseName) =>
        new(SilentLogger, new InMemoryScopeFactory(databaseName), new StubDownloadUtility(SilentLogger, _ => Task.FromResult("{}")));

    [Fact]
    public async Task SavePublicationLanguages_merges_into_publication_languages_store()
    {
        var sut = CreateSut(Guid.NewGuid().ToString());

        await sut.SavePublicationLanguages("nwt", new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.DefaultLanguageCode] = new LanguageInfo("English", AppConstants.Media.TextDirectionLeftToRight),
        });
        await sut.SavePublicationLanguages("nwt", new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["S"] = new LanguageInfo("Spanish", AppConstants.Media.TextDirectionLeftToRight),
        });

        Assert.True(sut.PublicationLanguages.TryGetValue("nwt", out var languages));
        Assert.Equal(2, languages.Count);
        Assert.True(languages.ContainsKey(AppConstants.Media.DefaultLanguageCode));
        Assert.True(languages.ContainsKey("S"));
    }

    [Fact]
    public async Task SaveMusicTracks_and_save_melody_music_tracks_complete_without_db_writes()
    {
        var sut = CreateSut(Guid.NewGuid().ToString());
        var tracks = new List<MusicTrack>
        {
            new() { Number = 1, Title = "Song 1", Url = "https://cdn.example.com/1.mp3" },
        };

        await sut.SaveMusicTracks("sjjc", AppConstants.Media.DefaultLanguageCode, "Sing Out Joyfully", tracks);
        await sut.SaveMelodyMusicTracks(
            AppConstants.Media.MelodyMusicPublicationCodeIam,
            new Dictionary<string, List<MusicTrack>>(StringComparer.OrdinalIgnoreCase)
            {
                ["iam-1"] = tracks,
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["iam-1"] = "Disc 1",
            });

        using var scope = new InMemoryScopeFactory(Guid.NewGuid().ToString()).CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(0, await db.BiblePublicationTracks.CountAsync());
    }

    [Fact]
    public async Task SaveBiblePublicationSections_stores_sections_in_memory_persister()
    {
        var sut = CreateSut(Guid.NewGuid().ToString());
        var sections = new Dictionary<int, BiblePublicationSection>
        {
            [1] = new() { Number = 1, Name = "Genesis" },
        };
        var sectionCodeTrackMap = new Dictionary<int, Dictionary<int, BiblePublicationTrack>>();

        await sut.SaveBiblePublicationSections(
            AppConstants.Media.DefaultLanguageCode,
            AppConstants.Media.BiblePublicationCodeNwt,
            "New World Translation",
            sections,
            sectionCodeTrackMap);

        Assert.Empty(sut.PublicationLanguages);
    }

    [Fact]
    public async Task SaveSectionLanguages_merges_languages_for_publication_section_pair()
    {
        var sut = CreateSut(Guid.NewGuid().ToString());

        await sut.SaveSectionLanguages(
            AppConstants.Media.BiblePublicationCodeNwt,
            AppConstants.Media.BiblePublicationGenesisBookNumber,
            new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.DefaultLanguageCode] = new LanguageInfo("English", AppConstants.Media.TextDirectionLeftToRight),
        });

        await sut.SaveSectionLanguages(
            AppConstants.Media.BiblePublicationCodeNwt,
            AppConstants.Media.BiblePublicationGenesisBookNumber,
            new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["S"] = new LanguageInfo("Spanish", AppConstants.Media.TextDirectionLeftToRight),
        });

        using var scope = new InMemoryScopeFactory(Guid.NewGuid().ToString()).CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        Assert.Equal(0, await db.SectionLanguages.CountAsync());
    }

    [Fact]
    public async Task SaveLanguageDiscovery_and_save_mediator_publication_complete_without_db()
    {
        var sut = CreateSut(Guid.NewGuid().ToString());

        await sut.SaveLanguageDiscovery(
            AppConstants.Media.DefaultLanguageCode,
            AppConstants.Media.BiblePublicationCodeNwt,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.DefaultLanguageCode] = "English",
            });
        await sut.SaveMediatorPublication(
            AppConstants.Media.DefaultLanguageCode,
            AppConstants.Media.BiblePublicationCategoryDramas,
            "Dramas",
            new Dictionary<string, List<MediatorTrack>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        await sut.SaveVideoEpisodes(
            AppConstants.Media.DefaultLanguageCode,
            AppConstants.Media.BiblePublicationCodeSeriesDigForTreasures,
            "Dig for Treasures",
            []);

        Assert.Empty(sut.PublicationLanguages);
    }
}
