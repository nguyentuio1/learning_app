using System;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;

namespace SmartDroneDelivery.Tests;

public class RbacIntegrationTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public RbacIntegrationTests(AuthApiFactory factory)
    {

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _client = factory.CreateClient();
    }
}
