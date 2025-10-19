using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IMusicHarvesterService
{
    Task HarvestVocalMusicLinksAsync();
    Task HarvestMelodyMusicLinksAsync();
}
