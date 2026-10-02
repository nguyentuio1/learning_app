using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure database is created/migrated
        await context.Database.MigrateAsync();

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
}
