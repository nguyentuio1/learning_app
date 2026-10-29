using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SmartDroneDelivery.Api.Options;

namespace SmartDroneDelivery.Api.Services;

public interface IAvatarStorage
{
    /// <summary>Stores the image as <c>{userId}.{extension}</c> and returns its public URL.</summary>
    Task<string> UploadAsync(Guid userId, Stream content, long length, string contentType, string extension);

    /// <summary>Removes an avatar by the URL returned from <see cref="UploadAsync"/>. A missing object is ignored.</summary>
    Task DeleteAsync(string avatarUrl);
}

public class MinioAvatarStorage : IAvatarStorage
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioAvatarStorage> _logger;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public MinioAvatarStorage(IOptions<MinioOptions> options, ILogger<MinioAvatarStorage> logger)
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

    public async Task<string> UploadAsync(Guid userId, Stream content, long length, string contentType, string extension)
    {
        await EnsureBucketAsync();
        var objectName = $"{userId}.{extension}";

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_options.AvatarBucket)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(length)
            .WithContentType(contentType));

        return $"{BaseUrl}/{_options.AvatarBucket}/{objectName}";
    }

    public async Task DeleteAsync(string avatarUrl)
    {
        var prefix = $"{BaseUrl}/{_options.AvatarBucket}/";
        if (!avatarUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_options.AvatarBucket)
                .WithObject(avatarUrl[prefix.Length..]));
        }
        catch (Exception ex)
        {
            // The new avatar is already saved; a leftover old file is not worth failing the request.
            _logger.LogWarning(ex, "Could not delete the old avatar {Url}", avatarUrl);
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

            var bucket = _options.AvatarBucket;
            if (!await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket)))
            {
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
                // Anyone may read avatar files; nobody can list or write without credentials.
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
