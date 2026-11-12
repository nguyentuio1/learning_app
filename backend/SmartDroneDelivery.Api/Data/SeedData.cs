using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

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

        // 2. Seed Default Administrator User
        if (!await context.Users.AnyAsync(u => u.Email == "admin@smartdronedelivery.com"))
        {
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Code == "ADMIN");
            if (adminRole != null)
            {
                var adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    RoleId = adminRole.Id,
                    Email = "admin@smartdronedelivery.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
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
    /// Demo data for local development (LA-325): three stations, a customer, a dispatcher and a station operator.
    /// Safe to run on every start: anything that already exists (matched by station code or email) is left as it is.
    /// The caller decides when it runs; <c>Program</c> only calls it in the Development environment with
    /// <c>Seed:DevelopmentData</c> switched on. Expects <see cref="InitializeAsync"/> to have created the roles.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await EnsureUserAsync(context, "CUSTOMER", "customer@smartdronedelivery.com", "Customer@123456", "Demo Customer", "0900000002");
        await EnsureUserAsync(context, "DISPATCHER", "dispatcher@smartdronedelivery.com", "Dispatcher@123456", "Demo Dispatcher", "0900000003");
        var stationOperator = await EnsureUserAsync(context, "STATION_OPERATOR", "operator@smartdronedelivery.com", "Operator@123456", "Demo Station Operator", "0900000004");

        await EnsureStationAsync(context, "HCM-01", "District 1 Hub", "1 Le Loi, District 1, Ho Chi Minh City", 10.7769m, 106.7009m, 4, "OPERATIONAL", stationOperator.Id);
        await EnsureStationAsync(context, "HCM-02", "Thu Duc Hub", "2 Vo Van Ngan, Thu Duc, Ho Chi Minh City", 10.8506m, 106.7719m, 4, "OPERATIONAL", null);
        await EnsureStationAsync(context, "HCM-03", "Binh Thanh Hub", "3 Xo Viet Nghe Tinh, Binh Thanh, Ho Chi Minh City", 10.8012m, 106.7098m, 2, "MAINTENANCE", null);
    }

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

    private static async Task EnsureStationAsync(AppDbContext context, string code, string name, string address,
        decimal latitude, decimal longitude, int totalSlots, string status, Guid? operatorId)
    {
        if (await context.LandingStations.AnyAsync(s => s.StationCode == code)) return;

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
    }
}
