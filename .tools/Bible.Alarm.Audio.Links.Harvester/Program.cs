using System;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bible.Alarm.Audio.Links.Harvester;

public class Program
{
    /// <summary>
    /// Harvest URL links to get the mp3 files links for Bible & Music 
    /// </summary>
    /// <param name="args"></param>
    public static async Task Main(string[] args)
    {
        try
        {
            // Create and configure the host
            var host = CreateHostBuilder(args).Build();

            // Get the orchestrator service and execute harvesting
            var orchestrator = host.Services.GetRequiredService<IHarvestingOrchestratorService>();
            await orchestrator.ExecuteHarvestingAsync();

            Console.WriteLine("Harvesting completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Harvesting failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddHarvesterServices();
            });
}