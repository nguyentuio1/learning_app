using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.Entities;
using Xunit;

namespace SmartDroneDelivery.Tests;

public class UserProfileIntegrationTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public UserProfileIntegrationTests(AuthApiFactory factory)
    {
        _factory = factory;
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    [Fact]
    public async Task Verify_UserProfileDataFlow_WithInMemoryDatabase_DirectQuery()
    {
        await _factory.WithDbAsync(async db =>
        {
            var mockRole = new Role
            {
                Name = "Admin",
                Description = "System Administrator"
            };

            var testUser = new User
            {
                Email = "integration-profile@drone.com",
                PasswordHash = "hashedpassword123",
                Role = mockRole
            };

            db.Users.Add(testUser);
            await db.SaveChangesAsync();

            var userInDb = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == "integration-profile@drone.com");

            Assert.NotNull(userInDb);
            Assert.NotNull(userInDb.Role);
            Assert.Equal("Admin", userInDb.Role.Name);
        });
    }
}
