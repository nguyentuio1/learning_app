using SmartDroneDelivery.Api.Services;

namespace SmartDroneDelivery.Tests;

public class FakeAvatarStorage : IAvatarStorage
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> Files { get; } = new();
    public List<string> Deleted { get; } = new();

    public async Task<string> UploadAsync(Guid userId, Stream content, long length, string contentType, string extension)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        var url = $"http://storage.test/avatars/{userId}.{extension}";
        Files[url] = (buffer.ToArray(), contentType);
        return url;
    }

    public Task DeleteAsync(string avatarUrl)
    {
        Deleted.Add(avatarUrl);
        Files.Remove(avatarUrl);
        return Task.CompletedTask;
    }
}
