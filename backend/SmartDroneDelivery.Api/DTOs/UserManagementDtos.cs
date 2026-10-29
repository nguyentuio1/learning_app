using System;
using System.ComponentModel.DataAnnotations;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.Models;

public record CreateUserRequest(
    [Required][EmailAddress][StringLength(255)] string Email,
    [Required][StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one digit.")] string Password,
    [Required][StringLength(50)] string Role,
    [Required][StringLength(150)] string FullName,
    [Required][RegularExpression(@"^\d{9,11}$", ErrorMessage = "Phone number must contain 9 to 11 digits.")] string PhoneNumber
);

public record UpdateUserRoleRequest(
    [Required][StringLength(50)] string Role
);

public record UpdateUserRequest(
    [Required][EmailAddress][StringLength(255)] string Email,
    [Required][StringLength(150)] string FullName,
    [Required][RegularExpression(@"^\d{9,11}$", ErrorMessage = "Phone number must contain 9 to 11 digits.")] string PhoneNumber
);

public record UserResponse(Guid Id, string Email, string Role, DateTime CreatedAt,
    string RoleCode, string FullName, string PhoneNumber, string Status, string? AvatarUrl)
{
    public static UserResponse From(User user) => new(user.Id, user.Email, user.Role.Name,
        DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc), user.Role.Code,
        user.FullName, user.PhoneNumber, user.Status, user.AvatarUrl);
}
