using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IS3Client
{
    Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request);
    Task PutObjectAsync(PutObjectRequest request);
    Task DeleteObjectsAsync(DeleteObjectsRequest request);
}

// DTOs for S3 operations
public class ListObjectsRequest
{
    public string BucketName { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
}

public class ListObjectsResponse
{
    public List<S3Object> S3Objects { get; set; } = new();
}

public class S3Object
{
    public string Key { get; set; } = string.Empty;
}

public class PutObjectRequest
{
    public string BucketName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class DeleteObjectsRequest
{
    public string BucketName { get; set; } = string.Empty;
    public List<DeleteObject> Objects { get; set; } = new();
}

public class DeleteObject
{
    public string Key { get; set; } = string.Empty;
}
