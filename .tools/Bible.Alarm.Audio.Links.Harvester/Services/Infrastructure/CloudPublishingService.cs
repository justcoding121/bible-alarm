using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;
using Bible.Alarm.Shared.Utilities;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class CloudPublishingService : ICloudPublishingService
{
    public async Task PublishToCloudFrontAsync()
    {
        var keyPrefix = "bible-alarm/media-index";
        var bucketName = "jthomas.info";
        
        using var s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName("ca-central-1"));

        var listObjectsResponse = await s3Client.ListObjectsAsync(new ListObjectsRequest
        {
            Prefix = $"{keyPrefix}/",
            BucketName = bucketName
        });

        var utcTime = DateTime.UtcNow;
        var fileName = $"{utcTime.Day}-{utcTime.Month}-{utcTime.Year}.zip";
        var keyName = $"{keyPrefix}/{fileName}";
        
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = keyName,
            FilePath = $"{DirectoryHelper.IndexDirectory}/index.zip"
        });

        if (listObjectsResponse.S3Objects.Count > 0)
        {
            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName
            };

            listObjectsResponse.S3Objects.ForEach(x =>
            {
                if (x.Key != keyName) deleteObjectsRequest.AddKey(x.Key);
            });

            if (deleteObjectsRequest.Objects.Count > 0) 
                await s3Client.DeleteObjectsAsync(deleteObjectsRequest);
        }
    }
}
