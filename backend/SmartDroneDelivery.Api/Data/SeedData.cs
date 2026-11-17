using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure database is created/migrated (non-relational providers are only used by the tests)
        if (context.Database.IsRelational())
            await context.Database.MigrateAsync();
        else
            await context.Database.EnsureCreatedAsync();

        // 1. Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role { Id = 1, Code = "CUSTOMER", Name = "Customer", Description = "End customer who creates and tracks delivery orders" },
                new Role { Id = 2, Code = "DISPATCHER", Name = "Dispatcher", Description = "Dispatcher who approves and monitors delivery missions" },
                new Role { Id = 3, Code = "STATION_OPERATOR", Name = "Station Operator", Description = "Operator stationed at landing stations to handle packages and lockers" },
                new Role { Id = 4, Code = "MANAGER", Name = "Logistics Manager", Description = "Manager overseeing delivery operations and analytics" },
                new Role { Id = 5, Code = "ADMIN", Name = "System Administrator", Description = "Super administrator with full access to configuration and audit logs" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // 2. Seed the first administrator (LA-328). Email and password come from configuration
        // (Seed:AdminEmail, Seed:AdminPassword); the repository holds no password. Without a password no administrator is created.
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminEmail = configuration[RequiredConfiguration.AdminEmailKey];
        if (string.IsNullOrWhiteSpace(adminEmail)) adminEmail = RequiredConfiguration.DefaultAdminEmail;
        var adminPassword = configuration[RequiredConfiguration.AdminPasswordKey];

        if (!await context.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
            if (string.IsNullOrEmpty(adminPassword))
            {
                scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedData))
                    .LogWarning("No administrator was created: set {Setting} to create {Email} on the next start.",
                        RequiredConfiguration.AdminPasswordKey, adminEmail);
            }
            else if (adminRole != null)
            {
                var adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    RoleId = adminRole.Id,
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    FullName = "System Administrator",
                    PhoneNumber = "0900000001",
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Demo data for local development (LA-325, LA-187): accounts, stations, addresses, drones and delivery scenarios.
    /// Safe to run on every start: anything that already exists (matched by station code or email) is left as it is.
    /// The caller decides when it runs; <c>Program</c> only calls it in the Development environment with
    /// <c>Seed:DevelopmentData</c> switched on. Expects <see cref="InitializeAsync"/> to have created the roles.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await EnsureUserAsync(context, "CUSTOMER", "customer@smartdronedelivery.com", "Customer@123456", "Demo Customer", "0900000002");
        var dispatcher = await EnsureUserAsync(context, "DISPATCHER", "dispatcher@smartdronedelivery.com", "Dispatcher@123456", "Demo Dispatcher", "0900000003");
        var stationOperator = await EnsureUserAsync(context, "STATION_OPERATOR", "operator@smartdronedelivery.com", "Operator@123456", "Demo Station Operator", "0900000004");
        var stationOperator2 = await EnsureUserAsync(context, "STATION_OPERATOR", "operator2@smartdronedelivery.com", "Operator@123456", "Demo Thu Duc Operator", "0900000006");
        await EnsureUserAsync(context, "MANAGER", "manager@smartdronedelivery.com", "Manager@123456", "Demo Logistics Manager", "0900000005");

        var hcm01 = await EnsureStationAsync(context, "HCM-01", "District 1 Hub", "1 Le Loi, District 1, Ho Chi Minh City", 10.7769m, 106.7009m, 4, "OPERATIONAL", stationOperator.Id);
        var hcm02 = await EnsureStationAsync(context, "HCM-02", "Thu Duc Hub", "2 Vo Van Ngan, Thu Duc, Ho Chi Minh City", 10.8506m, 106.7719m, 4, "OPERATIONAL", stationOperator2.Id);
        var hcm03 = await EnsureStationAsync(context, "HCM-03", "Binh Thanh Hub", "3 Xo Viet Nghe Tinh, Binh Thanh, Ho Chi Minh City", 10.8012m, 106.7098m, 2, "MAINTENANCE", null);

        var address1 = await EnsureAddressAsync(context, customer, "Home - District 1", "Demo Customer", "0900000002",
            "45 Nguyen Hue, District 1, Ho Chi Minh City", 10.7745m, 106.7035m, true);
        var address2 = await EnsureAddressAsync(context, customer, "Office - Thu Duc", "Tran Thi Binh", "0912345678",
            "120 Vo Van Ngan, Thu Duc, Ho Chi Minh City", 10.8512m, 106.7725m, false);

        var droneHcm01 = await EnsureDroneAsync(context, "DRONE-HCM-001", "DJI Matrice 300 RTK", 3000m, 16.0m, 38000, "IDLE", hcm01.Id);
        var droneHcm02 = await EnsureDroneAsync(context, "DRONE-HCM-002", "Wingcopter 198", 2500m, 75.0m, 24000, "IDLE", hcm02.Id);
        await EnsureDroneAsync(context, "DRONE-HCM-003", "DJI Matrice 300 RTK", 3000m, 16.0m, 38000, "CHARGING", hcm01.Id);
        await EnsureDroneAsync(context, "DRONE-HCM-004", "Matrice 350 RTK", 2700m, 15.0m, 5880, "MAINTENANCE", hcm03.Id);

        var now = DateTime.UtcNow;
        await EnsureDeliveryOrderAsync(context, customer, hcm01, hcm02, null, address1, null,
            "ORD-DEMO-001", "Medical Supplies Kit", 1200m, 30m, 20m, 15m, true, "482910",
            "Tran Thi Binh", "0912345678", "PENDING", now, now, null, null, null, null,
            [
                new(null, "PENDING", customer.Id, "Demo order submitted", now)
            ]);

        await EnsureDeliveryOrderAsync(context, customer, hcm01, hcm02, dispatcher, address2, null,
            "ORD-DEMO-002", "Electronics Spare Parts", 2500m, 35m, 25m, 20m, false, "739104",
            "Le Van Cuong", "0913456789", "APPROVED", now.AddMinutes(-10), now, null, null, null, null,
            [
                new(null, "PENDING", customer.Id, "Demo order submitted", now.AddMinutes(-10)),
                new("PENDING", "APPROVED", dispatcher.Id, "Demo order approved", now)
            ]);

        var scheduledDeparture = now.AddHours(-2);
        var actualDeparture = now.AddMinutes(-110);
        var estimatedDelivery = now.AddMinutes(-65);
        var actualDelivery = now.AddHours(-1);
        await EnsureDeliveryOrderAsync(context, customer, hcm01, hcm02, dispatcher, address2, droneHcm02,
            "ORD-DEMO-003", "Signed Legal Documents", 450m, 32m, 24m, 5m, false, "195826",
            "Pham Minh Dung", "0914567890", "DELIVERED", scheduledDeparture, actualDelivery,
            scheduledDeparture, actualDeparture, estimatedDelivery, actualDelivery,
            [
                new(null, "PENDING", customer.Id, "Demo order submitted", scheduledDeparture),
                new("PENDING", "APPROVED", dispatcher.Id, "Demo order approved", scheduledDeparture.AddMinutes(5)),
                new("APPROVED", "READY_FOR_TAKEOFF", stationOperator.Id, "Demo mission prepared", scheduledDeparture.AddMinutes(10)),
                new("READY_FOR_TAKEOFF", "IN_TRANSIT", null, "Demo mission departed", actualDeparture),
                new("IN_TRANSIT", "ARRIVED", null, "Demo mission arrived", estimatedDelivery),
                new("ARRIVED", "DELIVERED", stationOperator2.Id, "Demo delivery completed", actualDelivery)
            ]);
    }

    private sealed record StatusChange(string? FromStatus, string ToStatus, Guid? ChangedBy, string Remarks, DateTime CreatedAt);

    private static async Task<User> EnsureUserAsync(AppDbContext context, string roleCode, string email, string password, string fullName, string phoneNumber)
    {
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existing != null) return existing;

        var role = await context.Roles.SingleAsync(r => r.Code == roleCode);
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<LandingStation> EnsureStationAsync(AppDbContext context, string code, string name, string address,
        decimal latitude, decimal longitude, int totalSlots, string status, Guid? operatorId)
    {
        var existing = await context.LandingStations.FirstOrDefaultAsync(s => s.StationCode == code);
        if (existing != null) return existing;

        var station = new LandingStation
        {
            Id = Guid.NewGuid(),
            StationCode = code,
            Name = name,
            Address = address,
            Latitude = latitude,
            Longitude = longitude,
            TotalSlots = totalSlots,
            Status = status,
            OperatorId = operatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        for (var number = 1; number <= totalSlots; number++)
            station.Slots.Add(new StationSlot { Id = Guid.NewGuid(), StationId = station.Id, SlotNumber = number, Status = "EMPTY", LastUpdated = DateTime.UtcNow });

        context.LandingStations.Add(station);
        await context.SaveChangesAsync();
        return station;
    }

    private static async Task<CustomerAddress> EnsureAddressAsync(
        AppDbContext context, User customer, string label, string contactName, string contactPhone, string addressLine,
        decimal latitude, decimal longitude, bool isDefault)
    {
        var existing = await context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.UserId == customer.Id && a.Label == label);
        if (existing != null) return existing;

        var address = new CustomerAddress
        {
            Id = Guid.NewGuid(),
            UserId = customer.Id,
            Label = label,
            ContactName = contactName,
            ContactPhone = contactPhone,
            AddressLine = addressLine,
            Latitude = latitude,
            Longitude = longitude,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.CustomerAddresses.Add(address);
        await context.SaveChangesAsync();
        return address;
    }

    private static async Task<Drone> EnsureDroneAsync(
        AppDbContext context, string serialNumber, string modelName, decimal maxPayloadGram,
        decimal maxFlightRangeKm, int batteryCapacityMah, string status, Guid stationId)
    {
        var existing = await context.Drones.FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);
        if (existing != null) return existing;

        var drone = new Drone
        {
            Id = Guid.NewGuid(),
            SerialNumber = serialNumber,
            ModelName = modelName,
            MaxPayloadGram = maxPayloadGram,
            MaxFlightRangeKm = maxFlightRangeKm,
            BatteryCapacityMah = batteryCapacityMah,
            Status = status,
            CurrentStationId = stationId,
            CreatedAt = DateTime.UtcNow
        };
        context.Drones.Add(drone);
        await context.SaveChangesAsync();
        return drone;
    }

    private static async Task EnsureDeliveryOrderAsync(
        AppDbContext context, User customer, LandingStation origin, LandingStation destination, User? dispatcher,
        CustomerAddress address, Drone? missionDrone, string trackingNumber, string packageName, decimal weightGram,
        decimal lengthCm, decimal widthCm, decimal heightCm, bool isFragile, string pickupSecureCode,
        string recipientName, string recipientPhone, string status, DateTime createdAt, DateTime updatedAt,
        DateTime? scheduledDepartureTime, DateTime? actualDepartureTime, DateTime? estimatedDeliveryTime,
        DateTime? actualDeliveryTime, IReadOnlyList<StatusChange> statusChanges)
    {
        var order = await context.DeliveryOrders.FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber);
        if (order == null)
        {
            var package = new Package
            {
                Id = Guid.NewGuid(),
                SenderId = customer.Id,
                PackageName = packageName,
                WeightGram = weightGram,
                LengthCm = lengthCm,
                WidthCm = widthCm,
                HeightCm = heightCm,
                PackageImageUrl = null,
                IsFragile = isFragile,
                CreatedAt = createdAt
            };
            order = new DeliveryOrder
            {
                Id = Guid.NewGuid(),
                TrackingNumber = trackingNumber,
                CustomerId = customer.Id,
                PackageId = package.Id,
                OriginStationId = origin.Id,
                DestinationStationId = destination.Id,
                DispatcherId = dispatcher?.Id,
                DeliveryAddressId = address.Id,
                Status = status,
                PickupSecureCode = pickupSecureCode,
                RecipientName = recipientName,
                RecipientPhone = recipientPhone,
                ScheduledDepartureTime = scheduledDepartureTime,
                ActualDepartureTime = actualDepartureTime,
                EstimatedDeliveryTime = estimatedDeliveryTime,
                ActualDeliveryTime = actualDeliveryTime,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
            context.Packages.Add(package);
            context.DeliveryOrders.Add(order);
            await context.SaveChangesAsync();
        }

        foreach (var change in statusChanges)
        {
            if (await context.DeliveryStatusLogs.AnyAsync(log =>
                    log.OrderId == order.Id && log.ToStatus == change.ToStatus))
                continue;

            context.DeliveryStatusLogs.Add(new DeliveryStatusLog
            {
                OrderId = order.Id,
                ChangedBy = change.ChangedBy,
                FromStatus = change.FromStatus,
                ToStatus = change.ToStatus,
                Remarks = change.Remarks,
                CreatedAt = change.CreatedAt
            });
        }
        await context.SaveChangesAsync();

        if (missionDrone != null &&
            !await context.FlightMissions.AnyAsync(m => m.OrderId == order.Id))
        {
            context.FlightMissions.Add(new FlightMission
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                DroneId = missionDrone.Id,
                FlightStatus = "COMPLETED",
                StartTime = actualDepartureTime,
                EndTime = actualDeliveryTime,
                ScheduledDepartureTime = scheduledDepartureTime,
                EstimatedArrivalTime = estimatedDeliveryTime,
                CreatedAt = updatedAt
            });
            await context.SaveChangesAsync();
        }
    }
}
