namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IConfigurationService
{
    string GetValue(string key);
    T GetValue<T>(string key);
    bool HasValue(string key);
    string GetS3BucketName();
    string GetS3KeyPrefix();
    string GetAwsRegion();
    string IndexDirectory { get; }
    string S3BucketName { get; }
    string S3KeyPrefix { get; }
    string AwsRegion { get; }
}
