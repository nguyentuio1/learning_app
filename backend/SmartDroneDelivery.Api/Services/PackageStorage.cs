using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public interface IPackageStorage
{
    /// <summary>Stores the image as <c>{packageId}.{extension}</c> and returns its public URL.</summary>
    Task<string> UploadAsync(Guid packageId, Stream content, long length, string contentType, string extension);

    /// <summary>Removes a package image by URL. A missing object is ignored.</summary>
    Task DeleteAsync(string packageImageUrl);
}

public class MinioPackageStorage : IPackageStorage
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioPackageStorage> _logger;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public MinioPackageStorage(IOptions<MinioOptions> options, ILogger<MinioPackageStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey)
            .WithSSL(_options.WithSSL)
            .Build();
    }

    private string BaseUrl => (string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
        ? $"{(_options.WithSSL ? "https" : "http")}://{_options.Endpoint}"
        : _options.PublicBaseUrl).TrimEnd('/');

    public async Task<string> UploadAsync(Guid packageId, Stream content, long length, string contentType, string extension)
    {
        await EnsureBucketAsync();
        var objectName = $"{packageId}.{extension}";

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_options.PackageBucket)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(length)
            .WithContentType(contentType));

        return $"{BaseUrl}/{_options.PackageBucket}/{objectName}";
    }

    public async Task DeleteAsync(string packageImageUrl)
    {
        var prefix = $"{BaseUrl}/{_options.PackageBucket}/";
        if (!packageImageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_options.PackageBucket)
                .WithObject(packageImageUrl[prefix.Length..]));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete old package image {Url}", packageImageUrl);
        }
    }

    private async Task EnsureBucketAsync()
    {
        if (_bucketReady)
            return;

        await _bucketLock.WaitAsync();
        try
        {
            if (_bucketReady)
                return;

            var bucket = _options.PackageBucket;
            if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket)))
            {
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
                var policy = $$"""
                    {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucket}}/*"]}]}
                    """;
                await _client.SetPolicyAsync(new SetPolicyArgs().WithBucket(bucket).WithPolicy(policy));
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }
}
