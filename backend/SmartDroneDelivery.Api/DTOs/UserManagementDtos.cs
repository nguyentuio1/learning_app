using System;
using System.ComponentModel.DataAnnotations;

namespace SmartDroneDelivery.Api.Models;

public record CreateUserRequest(
    [Required][EmailAddress][StringLength(256)] string Email,
    [Required][MinLength(6)][StringLength(100)] string Password,
    [Required][StringLength(50)] string Role
);

public record UpdateUserRoleRequest(
    [Required][StringLength(50)] string Role
);

public record UserResponse(Guid Id, string Email, string Role, DateTime CreatedAt);
