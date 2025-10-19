using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IHarvestingOrchestratorService
{
    Task ExecuteHarvestingAsync();
}
