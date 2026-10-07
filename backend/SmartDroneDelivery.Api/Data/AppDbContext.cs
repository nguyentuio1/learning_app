using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LandingStation> LandingStations => Set<LandingStation>();
    public DbSet<StationSlot> StationSlots => Set<StationSlot>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<DeliveryStatusLog> DeliveryStatusLogs => Set<DeliveryStatusLog>();
    public DbSet<Drone> Drones => Set<Drone>();
    public DbSet<FlightMission> FlightMissions => Set<FlightMission>();
    public DbSet<FlightTelemetryLog> FlightTelemetryLogs => Set<FlightTelemetryLog>();
    public DbSet<AiEtaPrediction> AiEtaPredictions => Set<AiEtaPrediction>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<AiChatMessage> AiChatMessages => Set<AiChatMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        // 2. User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.PhoneNumber).IsUnique();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("ACTIVE");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 3. RefreshToken
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. LandingStation
        modelBuilder.Entity<LandingStation>(entity =>
        {
            entity.ToTable("landing_stations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StationCode).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.StationCode).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
            entity.Property(e => e.Address).IsRequired();
            entity.Property(e => e.TotalSlots).HasDefaultValue(2);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("OPERATIONAL");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Operator)
                .WithMany(u => u.OperatedStations)
                .HasForeignKey(e => e.OperatorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 5. StationSlot
        modelBuilder.Entity<StationSlot>(entity =>
        {
            entity.ToTable("station_slots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("EMPTY");
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.StationId, e.SlotNumber }).IsUnique();

            entity.HasOne(e => e.Station)
                .WithMany(s => s.Slots)
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 6. Package
        modelBuilder.Entity<Package>(entity =>
        {
            entity.ToTable("packages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PackageName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.WeightGram).HasPrecision(8, 2);
            entity.Property(e => e.LengthCm).HasPrecision(6, 2);
            entity.Property(e => e.WidthCm).HasPrecision(6, 2);
            entity.Property(e => e.HeightCm).HasPrecision(6, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Sender)
                .WithMany(u => u.SentPackages)
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. DeliveryOrder
        modelBuilder.Entity<DeliveryOrder>(entity =>
        {
            entity.ToTable("delivery_orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TrackingNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.TrackingNumber).IsUnique();
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("PENDING");
            entity.Property(e => e.PickupSecureCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.RecipientPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.RecipientName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Customer)
                .WithMany(u => u.CustomerOrders)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Package)
                .WithOne(p => p.DeliveryOrder)
                .HasForeignKey<DeliveryOrder>(e => e.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OriginStation)
                .WithMany(s => s.OriginOrders)
                .HasForeignKey(e => e.OriginStationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DestinationStation)
                .WithMany(s => s.DestinationOrders)
                .HasForeignKey(e => e.DestinationStationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Dispatcher)
                .WithMany(u => u.DispatchedOrders)
                .HasForeignKey(e => e.DispatcherId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DeliveryAddress)
                .WithMany(a => a.DeliveryOrders)
                .HasForeignKey(e => e.DeliveryAddressId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 8. DeliveryStatusLog
        modelBuilder.Entity<DeliveryStatusLog>(entity =>
        {
            entity.ToTable("delivery_status_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromStatus).HasMaxLength(30);
            entity.Property(e => e.ToStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Order)
                .WithMany(o => o.StatusLogs)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ChangedByUser)
                .WithMany(u => u.StatusLogs)
                .HasForeignKey(e => e.ChangedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 9. Drone
        modelBuilder.Entity<Drone>(entity =>
        {
            entity.ToTable("drones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SerialNumber).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.SerialNumber).IsUnique();
            entity.Property(e => e.ModelName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.MaxPayloadGram).HasPrecision(8, 2);
            entity.Property(e => e.MaxFlightRangeKm).HasPrecision(6, 2);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("IDLE");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.CurrentStation)
                .WithMany(s => s.StationDrones)
                .HasForeignKey(e => e.CurrentStationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 10. FlightMission
        modelBuilder.Entity<FlightMission>(entity =>
        {
            entity.ToTable("flight_missions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FlightStatus).HasMaxLength(30).HasDefaultValue("PLANNED");

            entity.HasOne(e => e.Order)
                .WithOne(o => o.FlightMission)
                .HasForeignKey<FlightMission>(e => e.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Drone)
                .WithMany(d => d.FlightMissions)
                .HasForeignKey(e => e.DroneId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StartSlot)
                .WithMany(s => s.DepartureMissions)
                .HasForeignKey(e => e.StartSlotId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DestinationSlot)
                .WithMany(s => s.DestinationMissions)
                .HasForeignKey(e => e.DestinationSlotId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 11. FlightTelemetryLog
        modelBuilder.Entity<FlightTelemetryLog>(entity =>
        {
            entity.ToTable("flight_telemetry_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
            entity.Property(e => e.AltitudeMeters).HasPrecision(6, 2);
            entity.Property(e => e.SpeedMps).HasPrecision(5, 2);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.MissionId, e.Timestamp });

            entity.HasOne(e => e.Mission)
                .WithMany(m => m.TelemetryLogs)
                .HasForeignKey(e => e.MissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 12. AiEtaPrediction
        modelBuilder.Entity<AiEtaPrediction>(entity =>
        {
            entity.ToTable("ai_eta_predictions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CalculatedDistanceKm).HasPrecision(6, 2);
            entity.Property(e => e.WeatherCondition).HasMaxLength(50);
            entity.Property(e => e.ConfidenceScore).HasPrecision(4, 3);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Order)
                .WithOne(o => o.AiEtaPrediction)
                .HasForeignKey<AiEtaPrediction>(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. AiChatSession
        modelBuilder.Entity<AiChatSession>(entity =>
        {
            entity.ToTable("ai_chat_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionTitle).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.User)
                .WithMany(u => u.ChatSessions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 14. AiChatMessage
        modelBuilder.Entity<AiChatMessage>(entity =>
        {
            entity.ToTable("ai_chat_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.MessageText).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 15. AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");

            entity.HasOne(e => e.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 16. CustomerAddress
        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.ToTable("customer_addresses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ContactName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AddressLine).IsRequired();
            entity.Property(e => e.Latitude).HasPrecision(9, 6);
            entity.Property(e => e.Longitude).HasPrecision(9, 6);
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Addresses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 17. Notification
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.UserId, e.IsRead });

            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.Notifications)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
