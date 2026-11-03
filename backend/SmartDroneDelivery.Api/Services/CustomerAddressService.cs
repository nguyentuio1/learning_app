using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartDroneDelivery.Api.Data;
using SmartDroneDelivery.Api.DTOs;
using SmartDroneDelivery.Api.Entities;
using SmartDroneDelivery.Api.Security;

namespace SmartDroneDelivery.Api.Services;

public interface ICustomerAddressService
{
    Task<IReadOnlyList<CustomerAddressDto>> ListAsync(Guid userId);
    Task<CustomerAddressDto> GetByIdAsync(Guid addressId, Guid userId, string userRole);
    Task<CustomerAddressDto> CreateAsync(Guid userId, CreateCustomerAddressRequest request);
    Task<CustomerAddressDto> UpdateAsync(Guid addressId, Guid userId, string userRole, UpdateCustomerAddressRequest request);
    Task<CustomerAddressDto> SetDefaultAsync(Guid addressId, Guid userId, string userRole);
    Task DeleteAsync(Guid addressId, Guid userId, string userRole);
}

public class CustomerAddressService : ICustomerAddressService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> UserLocks = new();
    private static readonly Regex PhoneRegex = new(@"^\d{9,11}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public CustomerAddressService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomerAddressDto>> ListAsync(Guid userId)
    {
        await EnsureActiveUserAsync(userId);

        return await _db.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a))
            .ToListAsync();
    }

    public async Task<CustomerAddressDto> GetByIdAsync(Guid addressId, Guid userId, string userRole)
    {
        await EnsureActiveUserAsync(userId);

        var address = await _db.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == addressId);

        if (address == null || (userRole == AppRoles.Customer && address.UserId != userId))
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

        return ToDto(address);
    }

    public async Task<CustomerAddressDto> CreateAsync(Guid userId, CreateCustomerAddressRequest request)
    {
        await EnsureActiveUserAsync(userId);
        ValidateAddressFields(request.Label, request.ContactName, request.ContactPhone, request.AddressLine, request.Latitude, request.Longitude);

        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync();
        try
        {
            var count = await _db.CustomerAddresses.CountAsync(a => a.UserId == userId);
            if (count >= 10)
                throw new ApiException(StatusCodes.Status409Conflict, "Maximum of 10 delivery addresses reached.");

            if (request.IsDefault)
            {
                await UnsetDefaultAddressesAsync(userId);
            }
            else if (count == 0)
            {
                request = request with { IsDefault = true };
            }

            var address = new CustomerAddress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Label = request.Label.Trim(),
                ContactName = request.ContactName.Trim(),
                ContactPhone = request.ContactPhone.Trim(),
                AddressLine = request.AddressLine.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsDefault = request.IsDefault,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.CustomerAddresses.Add(address);
            await _db.SaveChangesAsync();

            return ToDto(address);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task<CustomerAddressDto> UpdateAsync(Guid addressId, Guid userId, string userRole, UpdateCustomerAddressRequest request)
    {
        await EnsureActiveUserAsync(userId);
        ValidateAddressFields(request.Label, request.ContactName, request.ContactPhone, request.AddressLine, request.Latitude, request.Longitude);

        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync();
        try
        {
            var address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
            if (address == null || (userRole == AppRoles.Customer && address.UserId != userId))
                throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

            if (request.IsDefault && !address.IsDefault)
            {
                await UnsetDefaultAddressesAsync(address.UserId);
            }

            address.Label = request.Label.Trim();
            address.ContactName = request.ContactName.Trim();
            address.ContactPhone = request.ContactPhone.Trim();
            address.AddressLine = request.AddressLine.Trim();
            address.Latitude = request.Latitude;
            address.Longitude = request.Longitude;
            address.IsDefault = request.IsDefault;
            address.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return ToDto(address);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task<CustomerAddressDto> SetDefaultAsync(Guid addressId, Guid userId, string userRole)
    {
        await EnsureActiveUserAsync(userId);

        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync();
        try
        {
            var address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
            if (address == null || (userRole == AppRoles.Customer && address.UserId != userId))
                throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

            if (!address.IsDefault)
            {
                await UnsetDefaultAddressesAsync(address.UserId);
                address.IsDefault = true;
                address.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return ToDto(address);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task DeleteAsync(Guid addressId, Guid userId, string userRole)
    {
        await EnsureActiveUserAsync(userId);

        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync();
        try
        {
            var address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
            if (address == null || (userRole == AppRoles.Customer && address.UserId != userId))
                throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

            var isUsedByOrder = await _db.DeliveryOrders.AnyAsync(o => o.DeliveryAddressId == addressId);
            if (isUsedByOrder)
                throw new ApiException(StatusCodes.Status409Conflict, "Cannot delete address because it is referenced by a delivery order.");

            _db.CustomerAddresses.Remove(address);
            await _db.SaveChangesAsync();
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task EnsureActiveUserAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Missing, invalid or expired token.");

        if (user.Status != "ACTIVE")
            throw new ApiException(StatusCodes.Status403Forbidden, "This account is not active.");
    }

    private static void ValidateAddressFields(
        string? label,
        string? contactName,
        string? contactPhone,
        string? addressLine,
        decimal? latitude,
        decimal? longitude)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(label))
            errors["label"] = ["Label is required."];
        else if (label.Trim().Length > 50)
            errors["label"] = ["Label must be at most 50 characters."];

        if (string.IsNullOrWhiteSpace(contactName))
            errors["contactName"] = ["Contact name is required."];
        else if (contactName.Trim().Length > 150)
            errors["contactName"] = ["Contact name must be at most 150 characters."];

        if (string.IsNullOrWhiteSpace(contactPhone) || !PhoneRegex.IsMatch(contactPhone.Trim()))
            errors["contactPhone"] = ["Contact phone must be 9 to 11 digits."];

        if (string.IsNullOrWhiteSpace(addressLine))
            errors["addressLine"] = ["Address line is required."];

        if (latitude.HasValue != longitude.HasValue)
        {
            const string coordMsg = "Latitude and longitude must both be provided or both be omitted.";
            errors["latitude"] = [coordMsg];
            errors["longitude"] = [coordMsg];
        }
        else
        {
            if (latitude.HasValue && (latitude.Value < -90m || latitude.Value > 90m))
                errors["latitude"] = ["Latitude must be between -90 and 90."];

            if (longitude.HasValue && (longitude.Value < -180m || longitude.Value > 180m))
                errors["longitude"] = ["Longitude must be between -180 and 180."];
        }

        if (errors.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more fields are invalid.", errors);
    }

    private async Task UnsetDefaultAddressesAsync(Guid userId)
    {
        var defaultAddresses = await _db.CustomerAddresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync();

        foreach (var addr in defaultAddresses)
        {
            addr.IsDefault = false;
            addr.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static CustomerAddressDto ToDto(CustomerAddress a) => new(
        a.Id,
        a.UserId,
        a.Label,
        a.ContactName,
        a.ContactPhone,
        a.AddressLine,
        a.Latitude,
        a.Longitude,
        a.IsDefault,
        a.CreatedAt,
        a.UpdatedAt);
}
