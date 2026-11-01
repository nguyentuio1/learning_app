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
    private readonly AppDbContext _db;

    public CustomerAddressService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomerAddressDto>> ListAsync(Guid userId)
    {
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
        var address = await _db.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == addressId);

        if (address == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

        if (userRole == AppRoles.Customer && address.UserId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to access this address.");

        return ToDto(address);
    }

    public async Task<CustomerAddressDto> CreateAsync(Guid userId, CreateCustomerAddressRequest request)
    {
        if (request.Latitude.HasValue != request.Longitude.HasValue)
            throw new ApiException(StatusCodes.Status400BadRequest, "Latitude and longitude must both be provided or both be omitted.");

        var count = await _db.CustomerAddresses.CountAsync(a => a.UserId == userId);
        if (count >= 10)
            throw new ApiException(StatusCodes.Status409Conflict, "Maximum of 10 delivery addresses reached.");

        if (request.IsDefault)
        {
            await UnsetDefaultAddressesAsync(userId);
        }
        else if (count == 0)
        {
            // If this is the user's first address, automatically make it default
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

    public async Task<CustomerAddressDto> UpdateAsync(Guid addressId, Guid userId, string userRole, UpdateCustomerAddressRequest request)
    {
        if (request.Latitude.HasValue != request.Longitude.HasValue)
            throw new ApiException(StatusCodes.Status400BadRequest, "Latitude and longitude must both be provided or both be omitted.");

        var address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
        if (address == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

        if (userRole == AppRoles.Customer && address.UserId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to modify this address.");

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

    public async Task<CustomerAddressDto> SetDefaultAsync(Guid addressId, Guid userId, string userRole)
    {
        var address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
        if (address == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

        if (userRole == AppRoles.Customer && address.UserId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to modify this address.");

        if (!address.IsDefault)
        {
            await UnsetDefaultAddressesAsync(address.UserId);
            address.IsDefault = true;
            address.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return ToDto(address);
    }

    public async Task DeleteAsync(Guid addressId, Guid userId, string userRole)
    {
        var address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId);
        if (address == null)
            throw new ApiException(StatusCodes.Status404NotFound, "Delivery address not found.");

        if (userRole == AppRoles.Customer && address.UserId != userId)
            throw new ApiException(StatusCodes.Status403Forbidden, "You do not have permission to delete this address.");

        var isUsedByOrder = await _db.DeliveryOrders.AnyAsync(o => o.DeliveryAddressId == addressId);
        if (isUsedByOrder)
            throw new ApiException(StatusCodes.Status409Conflict, "Cannot delete address because it is referenced by a delivery order.");

        _db.CustomerAddresses.Remove(address);
        await _db.SaveChangesAsync();

        // If the deleted address was default, promote the newest remaining address to default
        if (address.IsDefault)
        {
            var nextDefault = await _db.CustomerAddresses
                .Where(a => a.UserId == address.UserId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextDefault != null)
            {
                nextDefault.IsDefault = true;
                nextDefault.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
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
