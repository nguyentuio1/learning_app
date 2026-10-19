using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.Security;
using Xunit;

namespace SmartDroneDelivery.Tests;

public class RbacRoleUnitTests
{
    [Fact]
    public void DispatcherOrAdminEndpoint_ShouldAllow_BothRoles_StrictCheck()
    {
        var method = typeof(RbacProbeController).GetMethod(nameof(RbacProbeController.DispatcherOrAdmin));
        var authAttribute = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttribute);
        var allowedRoles = authAttribute.Roles?.Split(',');

        Assert.NotNull(allowedRoles);
        Assert.Contains(AppRoles.Dispatcher, allowedRoles);
        Assert.Contains(AppRoles.Admin, allowedRoles);
    }

    [Fact]
    public void DispatcherOrAdminEndpoint_ShouldBlock_CustomerRole()
    {
        var method = typeof(RbacProbeController).GetMethod(nameof(RbacProbeController.DispatcherOrAdmin));
        var authAttribute = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttribute);
        var allowedRoles = authAttribute.Roles?.Split(',');

        Assert.NotNull(allowedRoles);
        Assert.DoesNotContain(AppRoles.Customer, allowedRoles);
    }

    [Theory]
    [InlineData(nameof(RbacProbeController.Customer))]
    [InlineData(nameof(RbacProbeController.Dispatcher))]
    [InlineData(nameof(RbacProbeController.StationOperator))]
    [InlineData(nameof(RbacProbeController.Manager))]
    [InlineData(nameof(RbacProbeController.Admin))]
    public void AllRoleEndpoints_MustNotBe_Anonymous(string methodName)
    {
        var method = typeof(RbacProbeController).GetMethod(methodName);
        var anonymousAttribute = method?.GetCustomAttribute<AllowAnonymousAttribute>();
        var authAttribute = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.Null(anonymousAttribute);
        Assert.NotNull(authAttribute);
    }
}
