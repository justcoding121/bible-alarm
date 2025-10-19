using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Shared.Utilities;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class CloudPublishingService(IS3Client s3Client, IConfigurationService configurationService)
    : ICloudPublishingService
{
    public async Task PublishToCloudFrontAsync()
    {
        var keyPrefix = configurationService.GetS3KeyPrefix();
        var bucketName = configurationService.GetS3BucketName();
        
        var listObjectsResponse = await s3Client.ListObjectsAsync(new Bible.Alarm.Audio.Links.Harvester.Services.Contracts.ListObjectsRequest
        {
            Prefix = $"{keyPrefix}/",
            BucketName = bucketName
        });

        var utcTime = DateTime.UtcNow;
        var fileName = $"{utcTime.Day}-{utcTime.Month}-{utcTime.Year}.zip";
        var keyName = $"{keyPrefix}/{fileName}";
        
        await s3Client.PutObjectAsync(new Bible.Alarm.Audio.Links.Harvester.Services.Contracts.PutObjectRequest
        {
            BucketName = bucketName,
            Key = keyName,
            FilePath = $"{DirectoryHelper.IndexDirectory}/index.zip"
        });

        if (listObjectsResponse.S3Objects.Count > 0)
        {
            var deleteObjectsRequest = new Bible.Alarm.Audio.Links.Harvester.Services.Contracts.DeleteObjectsRequest
            {
                BucketName = bucketName
            };

            listObjectsResponse.S3Objects.ForEach(x =>
            {
                if (x.Key != keyName) deleteObjectsRequest.Objects.Add(new Bible.Alarm.Audio.Links.Harvester.Services.Contracts.DeleteObject { Key = x.Key });
            });

            if (deleteObjectsRequest.Objects.Count > 0) 
                await s3Client.DeleteObjectsAsync(deleteObjectsRequest);
        }
    }

    public async Task PublishIndexToCloudFrontAsync()
    {
        await PublishToCloudFrontAsync();
    }
}
