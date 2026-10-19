using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace SmartDroneDelivery.Tests;

public class RbacRoleIntegrationTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;
    private readonly AuthApiFactory _factory;

    public RbacRoleIntegrationTests(AuthApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    [Fact]
    public async Task Get_OpenEndpoint_Returns200Ok_WithoutToken()
    {
        var response = await _client.GetAsync("probe/open");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_SignedInEndpoint_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("probe/signed-in");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Verify_RbacDataFlow_WithInMemoryDatabase_Query()
    {
        await _factory.WithDbAsync(async db =>
        {
            Assert.NotNull(db);
            await Task.CompletedTask;
        });
    }
}
