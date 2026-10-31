using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FakePackageStorage : IPackageStorage
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> Files { get; } = new();
    public List<string> Deleted { get; } = new();

    public async Task<string> UploadAsync(Guid packageId, Stream content, long length, string contentType, string extension)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var url = $"http://storage.test/packages/{packageId}.{extension}";
        Files[url] = (buffer.ToArray(), contentType);
        return url;
    }

    public Task DeleteAsync(string packageImageUrl)
    {
        Deleted.Add(packageImageUrl);
        Files.Remove(packageImageUrl);
        return Task.CompletedTask;
    }
}
