using System.ComponentModel.DataAnnotations;
using SmartDroneDelivery.Api.Entities;

namespace SmartDroneDelivery.Api.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not valid.")]
    [MaxLength(255, ErrorMessage = "Email must be at most 255 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be 8 to 100 characters.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one digit.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(150, ErrorMessage = "Full name must be at most 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\d{9,11}$", ErrorMessage = "Phone number must be 9 to 11 digits.")]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(150, ErrorMessage = "Full name must be at most 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\d{9,11}$", ErrorMessage = "Phone number must be 9 to 11 digits.")]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Current password is required.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be 8 to 100 characters.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one digit.")]
    public string NewPassword { get; set; } = string.Empty;
}

public record AvatarResponse(string AvatarUrl);

public record RoleDto(int Id, string Code, string Name);

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string PhoneNumber,
    string? AvatarUrl,
    string Status,
    RoleDto Role,
    DateTime CreatedAt)
{
    public static UserDto From(User user) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.PhoneNumber,
        user.AvatarUrl,
        user.Status,
        new RoleDto(user.Role.Id, user.Role.Code, user.Role.Name),
        DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc));
}

public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);
