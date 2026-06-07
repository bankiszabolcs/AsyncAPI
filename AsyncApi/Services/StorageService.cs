using Amazon.S3;
using Amazon.S3.Model;

namespace AsyncApi.Services;

public enum StorageBucket { Videos, Images }

public sealed class StorageService
{
    private readonly AmazonS3Client _client;
    private readonly string _videosBucket;
    private readonly string _imagesBucket;
    private readonly string _publicUrl;
    private readonly HashSet<string> _readyBuckets = [];
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".m3u8"] = "application/vnd.apple.mpegurl",
        [".ts"]   = "video/mp2t",
        [".jpg"]  = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"]  = "image/png",
        [".gif"]  = "image/gif",
        [".vtt"]  = "text/vtt"
    };

    public StorageService(IConfiguration configuration)
    {
        var endpoint   = configuration["Storage:Endpoint"]     ?? "localhost:9000";
        var accessKey  = configuration["Storage:AccessKey"]    ?? "minioadmin";
        var secretKey  = configuration["Storage:SecretKey"]    ?? "minioadmin";
        _videosBucket  = configuration["Storage:VideosBucket"] ?? "videos";
        _imagesBucket  = configuration["Storage:ImagesBucket"] ?? "images";
        _publicUrl     = configuration["Storage:PublicUrl"]    ?? "http://localhost:9000";

        _client = new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
        {
            ServiceURL     = $"http://{endpoint}",
            ForcePathStyle = true
        });
    }

    public async Task UploadFileAsync(string localPath, string key, StorageBucket bucket)
    {
        var bucketName = GetBucketName(bucket);
        await EnsureBucketReadyAsync(bucketName);

        var contentType = ContentTypes.GetValueOrDefault(
            Path.GetExtension(localPath).ToLowerInvariant(),
            "application/octet-stream");

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName  = bucketName,
            Key         = key,
            FilePath    = localPath,
            ContentType = contentType
        });
    }

    public async Task UploadDirectoryAsync(string localDir, string keyPrefix, StorageBucket bucket)
    {
        var files = Directory.GetFiles(localDir, "*", SearchOption.AllDirectories);
        await Task.WhenAll(files.Select(file =>
        {
            var relativePath = Path.GetRelativePath(localDir, file).Replace('\\', '/');
            return UploadFileAsync(file, $"{keyPrefix}/{relativePath}", bucket);
        }));
    }

    public string GetPublicUrl(string key, StorageBucket bucket) =>
        $"{_publicUrl}/{GetBucketName(bucket)}/{key}";

    private string GetBucketName(StorageBucket bucket) => bucket switch
    {
        StorageBucket.Videos => _videosBucket,
        StorageBucket.Images => _imagesBucket,
        _ => throw new ArgumentOutOfRangeException(nameof(bucket))
    };

    private async Task EnsureBucketReadyAsync(string bucketName)
    {
        if (_readyBuckets.Contains(bucketName)) return;

        await _initLock.WaitAsync();
        try
        {
            if (_readyBuckets.Contains(bucketName)) return;

            var response = await _client.ListBucketsAsync();
            var exists = response.Buckets?.Any(b => b.BucketName == bucketName) ?? false;

            if (!exists)
            {
                await _client.PutBucketAsync(bucketName);
                await _client.PutBucketPolicyAsync(new PutBucketPolicyRequest
                {
                    BucketName = bucketName,
                    Policy = $$"""{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucketName}}/*"]}]}"""
                });
            }

            _readyBuckets.Add(bucketName);
        }
        finally
        {
            _initLock.Release();
        }
    }
}
