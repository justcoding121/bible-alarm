using System;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class ConfigurationService : IConfigurationService
{
    public string GetValue(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }

    public T GetValue<T>(string key)
    {
        var value = GetValue(key);
        return (T)Convert.ChangeType(value, typeof(T));
    }

    public bool HasValue(string key)
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key));
    }

    public string GetS3BucketName()
    {
        return GetValue("S3_BUCKET_NAME") ?? "jthomas.info";
    }

    public string GetS3KeyPrefix()
    {
        return GetValue("S3_KEY_PREFIX") ?? "bible-alarm/media-index";
    }

    public string GetAwsRegion()
    {
        return GetValue("AWS_REGION") ?? "ca-central-1";
    }

    public string IndexDirectory => GetValue("INDEX_DIRECTORY") ?? "index";

    public string S3BucketName => GetS3BucketName();

    public string S3KeyPrefix => GetS3KeyPrefix();

    public string AwsRegion => GetAwsRegion();
}
