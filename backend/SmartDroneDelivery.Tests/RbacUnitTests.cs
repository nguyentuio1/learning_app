using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.Security;
using Xunit;

namespace SmartDroneDelivery.Tests;

public class RbacUnitTests
{
    // Xác thực AC 1 (Cấu hình tầng mã nguồn): Endpoint Open phải cho phép truy cập tự do
    [Fact]
    public void OpenEndpoint_ShouldHave_AllowAnonymousAttribute()
    {
        // Arrange & Act
        var method = typeof(RbacProbeController).GetMethod(nameof(RbacProbeController.Open));
        var attribute = method?.GetCustomAttribute<AllowAnonymousAttribute>();

        // Assert
        Assert.NotNull(attribute);
    }

    // Xác thực AC 4 (Cấu hình tầng mã nguồn): Các endpoint phải gắn đúng quyền tĩnh của AppRoles
    [Theory]
    [InlineData(nameof(RbacProbeController.Customer), AppRoles.Customer)]
    [InlineData(nameof(RbacProbeController.Dispatcher), AppRoles.Dispatcher)]
    [InlineData(nameof(RbacProbeController.StationOperator), AppRoles.StationOperator)]
    [InlineData(nameof(RbacProbeController.Manager), AppRoles.Manager)]
    [InlineData(nameof(RbacProbeController.Admin), AppRoles.Admin)]
    public void RoleEndpoints_ShouldRequire_SpecificRoleAttribute(string methodName, string expectedRole)
    {
        // Arrange & Act
        var method = typeof(RbacProbeController).GetMethod(methodName);
        var authAttribute = method?.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authAttribute);
        Assert.Equal(expectedRole, authAttribute.Roles);
    }

    [Fact]
    public void DispatcherOrAdminEndpoint_ShouldAllow_BothRoles()
    {
        // Arrange & Act
        var method = typeof(RbacProbeController).GetMethod(nameof(RbacProbeController.DispatcherOrAdmin));
        var authAttribute = method?.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authAttribute);
        Assert.Contains(AppRoles.Dispatcher, authAttribute.Roles);
        Assert.Contains(AppRoles.Admin, authAttribute.Roles);
    }
}
