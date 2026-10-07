namespace SmartDroneDelivery.Api.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int RoleId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<DeliveryOrder> CustomerOrders { get; set; } = new List<DeliveryOrder>();
    public ICollection<DeliveryOrder> DispatchedOrders { get; set; } = new List<DeliveryOrder>();
    public ICollection<Package> SentPackages { get; set; } = new List<Package>();
    public ICollection<LandingStation> OperatedStations { get; set; } = new List<LandingStation>();
    public ICollection<DeliveryStatusLog> StatusLogs { get; set; } = new List<DeliveryStatusLog>();
    public ICollection<AiChatSession> ChatSessions { get; set; } = new List<AiChatSession>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
