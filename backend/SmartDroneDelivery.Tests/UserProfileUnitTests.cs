using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartDroneDelivery.Api.Controllers;
using SmartDroneDelivery.Api.Models;
using Xunit;

namespace SmartDroneDelivery.Tests;

public class UserProfileUnitTests
{
    [Fact]
    public void UserManagementController_Class_MustBeProtected_ByAdminRole()
    {
        var controllerType = typeof(UserManagementController);
        var authAttribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttribute);
        Assert.NotNull(authAttribute.Roles);
        Assert.Equal("ADMIN", authAttribute.Roles.ToUpperInvariant());
    }

    [Theory]
    [InlineData(nameof(UserManagementController.GetAllUsers))]
    [InlineData(nameof(UserManagementController.CreateUser))]
    [InlineData(nameof(UserManagementController.UpdateUserRole))]
    public void UserProfileEndpoints_MustBe_PublicRouteAccessible(string methodName)
    {
        var method = typeof(UserManagementController).GetMethod(methodName);
        Assert.NotNull(method);

        var hasBaseRoute = typeof(UserManagementController).GetCustomAttribute<RouteAttribute>() != null;
        Assert.True(hasBaseRoute);
    }

    [Fact]
    public void CreateUserRequest_Properties_ShouldBe_ValidatableInSystem()
    {
        var emailProperty = typeof(CreateUserRequest).GetProperty("Email");
        var passwordProperty = typeof(CreateUserRequest).GetProperty("Password");
        var roleProperty = typeof(CreateUserRequest).GetProperty("Role");

        Assert.NotNull(emailProperty);
        Assert.NotNull(passwordProperty);
        Assert.NotNull(roleProperty);
    }
}
