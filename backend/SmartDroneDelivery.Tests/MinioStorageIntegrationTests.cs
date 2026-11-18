using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SmartDroneDelivery.Api.Options;
using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

/// <summary>Opt-in live object-storage regression; owns and cleans only its unique test bucket.</summary>
public class MinioStorageIntegrationTests
{
    [MinioFact]
    public async Task Package_photo_in_existing_private_bucket_becomes_readable_without_credentials() => await VerifyUpload(false);

    [MinioFact]
    public async Task Avatar_in_existing_private_bucket_becomes_readable_without_credentials() => await VerifyUpload(true);

    private static async Task VerifyUpload(bool avatar)
    {
        var endpoint = Environment.GetEnvironmentVariable("TEST_MINIO_ENDPOINT")!;
        var useSsl = Environment.GetEnvironmentVariable("TEST_MINIO_SSL") == "true";
        var bucket = "photo-regression-" + Guid.NewGuid().ToString("N");
        var objectId = Guid.NewGuid();
        var options = new MinioOptions
        {
            Endpoint = endpoint, AccessKey = Environment.GetEnvironmentVariable("TEST_MINIO_ACCESS_KEY")!,
            SecretKey = Environment.GetEnvironmentVariable("TEST_MINIO_SECRET_KEY")!, WithSSL = useSsl,
            AvatarBucket = bucket, PackageBucket = bucket,
            PublicBaseUrl = $"{(useSsl ? "https" : "http")}://{endpoint}/",
        };
        using var minio = new MinioClient().WithEndpoint(endpoint).WithCredentials(options.AccessKey, options.SecretKey).WithSSL(useSsl).Build();
        await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
        try
        {
            using var http = new HttpClient();
            var objectUrl = $"{options.PublicBaseUrl}{bucket}/{objectId}.png";
            // A pre-existing private bucket models provisioning, or retry after MakeBucket succeeds but SetPolicy fails.
            using (var before = await http.GetAsync(objectUrl))
                Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4l8AAAAASUVORK5CYII=");
            using var stream = new MemoryStream(png);
            var url = avatar
                ? await new MinioAvatarStorage(Options.Create(options), NullLogger<MinioAvatarStorage>.Instance).UploadAsync(objectId, stream, png.Length, "image/png", "png")
                : await new MinioPackageStorage(Options.Create(options), NullLogger<MinioPackageStorage>.Instance).UploadAsync(objectId, stream, png.Length, "image/png", "png");
            Assert.Equal(objectUrl, url);
            using var after = await http.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, after.StatusCode);
            Assert.Equal("image/png", after.Content.Headers.ContentType?.MediaType);
            Assert.Equal(png, await after.Content.ReadAsByteArrayAsync());
            using var listing = await http.GetAsync(options.PublicBaseUrl + bucket);
            Assert.Equal(HttpStatusCode.Forbidden, listing.StatusCode); // Object reads do not grant bucket listing.
        }
        finally
        {
            await minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject($"{objectId}.png"));
            await minio.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucket));
        }
    }
}

public sealed class MinioFactAttribute : FactAttribute
{
    public MinioFactAttribute()
    {
        if (new[] { "TEST_MINIO_ENDPOINT", "TEST_MINIO_ACCESS_KEY", "TEST_MINIO_SECRET_KEY" }
            .Any(key => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))))
            Skip = "Set TEST_MINIO_ENDPOINT, TEST_MINIO_ACCESS_KEY and TEST_MINIO_SECRET_KEY to run against live MinIO.";
    }
}
