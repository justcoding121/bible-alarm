using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface ICloudPublishingService
{
    Task PublishToCloudFrontAsync();
}
