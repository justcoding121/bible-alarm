using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class S3Client : IS3Client
{
    private readonly AmazonS3Client _amazonS3Client;

    public S3Client(IConfigurationService configurationService)
    {
        var region = Amazon.RegionEndpoint.GetBySystemName(configurationService.GetAwsRegion());
        _amazonS3Client = new AmazonS3Client(region);
    }

    public async Task<Bible.Alarm.Audio.Links.Harvester.Services.Contracts.ListObjectsResponse> ListObjectsAsync(Bible.Alarm.Audio.Links.Harvester.Services.Contracts.ListObjectsRequest request)
    {
        var awsRequest = new Amazon.S3.Model.ListObjectsRequest
        {
            BucketName = request.BucketName,
            Prefix = request.Prefix
        };

        var awsResponse = await _amazonS3Client.ListObjectsAsync(awsRequest);
        
        return new Bible.Alarm.Audio.Links.Harvester.Services.Contracts.ListObjectsResponse
        {
            S3Objects = awsResponse.S3Objects.ConvertAll(x => new Bible.Alarm.Audio.Links.Harvester.Services.Contracts.S3Object { Key = x.Key })
        };
    }

    public async Task PutObjectAsync(Bible.Alarm.Audio.Links.Harvester.Services.Contracts.PutObjectRequest request)
    {
        var awsRequest = new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = request.BucketName,
            Key = request.Key,
            FilePath = request.FilePath
        };

        await _amazonS3Client.PutObjectAsync(awsRequest);
    }

    public async Task DeleteObjectsAsync(Bible.Alarm.Audio.Links.Harvester.Services.Contracts.DeleteObjectsRequest request)
    {
        var awsRequest = new Amazon.S3.Model.DeleteObjectsRequest
        {
            BucketName = request.BucketName
        };

        foreach (var obj in request.Objects)
        {
            awsRequest.Objects.Add(new KeyVersion { Key = obj.Key });
        }

        await _amazonS3Client.DeleteObjectsAsync(awsRequest);
    }
}
