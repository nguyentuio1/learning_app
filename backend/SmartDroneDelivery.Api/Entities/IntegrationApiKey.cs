using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.Entities;

public class IntegrationApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    [ConcurrencyCheck] public DateTime? RevokedAt { get; set; }
    public User Customer { get; set; } = null!;
}
