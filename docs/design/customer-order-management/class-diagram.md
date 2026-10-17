# Customer, Address, Package and Order Management — Class Diagram

## Scope and architecture

This is a design specification for the SmartDroneDelivery ASP.NET Core 8 Web API. It is not an implementation or a claim that these controllers, services, and DTOs already exist.

The module follows these layers:

| Layer | Responsibility | Main types |
|---|---|---|
| Presentation | Authenticated HTTP endpoints, request binding, authorization, and HTTP response mapping | `CustomerAddressesController`, `PackagesController`, `DeliveryOrdersController`, `NotificationsController` |
| Application | Use-case orchestration, ownership checks, validation, status rules, and transaction boundaries | `ICustomerAddressService`, `IPackageService`, `IDeliveryOrderService`, `INotificationService` and their service implementations |
| Domain | Customer delivery concepts, relationships, and allowed order statuses | `CustomerAddress`, `Package`, `DeliveryOrder`, `DeliveryStatusLog`, `Notification`, `User`, `LandingStation`, `OrderStatus` |
| Data Access | EF Core persistence and database transactions | `AppDbContext`, PostgreSQL |
| DTO | Input/output contracts at the API boundary; entities are not returned directly | Create/update/cancel/filter request DTOs and order/notification response DTOs |

DTOs are shown as a boundary layer rather than a separate runtime dependency of the domain. Controllers bind DTOs and delegate to application services; services persist domain entities through `AppDbContext`.

## Class diagram

```mermaid
classDiagram
    direction LR

    class CustomerAddressesController {
        +GetAllAsync() Task~IReadOnlyList~CustomerAddressResponseDto~~
        +GetByIdAsync(id) Task~CustomerAddressResponseDto~
        +CreateAsync(dto) Task~CustomerAddressResponseDto~
        +UpdateAsync(id, dto) Task~CustomerAddressResponseDto~
        +DeleteAsync(id) Task
        +SetDefaultAsync(id) Task
    }
    class PackagesController {
        +GetAllAsync() Task~IReadOnlyList~PackageResponseDto~~
        +GetByIdAsync(id) Task~PackageResponseDto~
        +CreateAsync(dto) Task~PackageResponseDto~
        +UpdateAsync(id, dto) Task~PackageResponseDto~
    }
    class DeliveryOrdersController {
        +CreateAsync(dto) Task~DeliveryOrderDetailResponseDto~
        +GetMineAsync(filters) Task~PagedResponse~DeliveryOrderDetailResponseDto~~
        +GetByIdAsync(id) Task~DeliveryOrderDetailResponseDto~
        +UpdateAsync(id, dto) Task~DeliveryOrderDetailResponseDto~
        +CancelAsync(id, dto) Task
        +ConfirmDeliveryAsync(id, dto) Task
    }
    class NotificationsController {
        +GetMineAsync() Task~IReadOnlyList~NotificationResponseDto~~
        +MarkReadAsync(id) Task
    }

    class ICustomerAddressService {
        <<interface>>
        +GetAllAsync(customerId) Task~IReadOnlyList~CustomerAddress~
        +GetByIdAsync(customerId, id) Task~CustomerAddress~
        +CreateAsync(customerId, dto) Task~CustomerAddress~
        +UpdateAsync(customerId, id, dto) Task~CustomerAddress~
        +DeleteAsync(customerId, id) Task
        +SetDefaultAsync(customerId, id) Task
    }
    class IPackageService {
        <<interface>>
        +GetAllAsync(customerId) Task~IReadOnlyList~Package~
        +GetByIdAsync(customerId, id) Task~Package~
        +CreateAsync(customerId, dto) Task~Package~
        +UpdateAsync(customerId, id, dto) Task~Package~
    }
    class IDeliveryOrderService {
        <<interface>>
        +CreateAsync(customerId, dto) Task~DeliveryOrder~
        +GetMineAsync(customerId, filters) Task~PagedResponse~DeliveryOrderDetailResponseDto~~
        +GetByIdAsync(customerId, id) Task~DeliveryOrder~
        +UpdateAsync(customerId, id, dto) Task~DeliveryOrder~
        +CancelAsync(customerId, id, dto) Task
        +ConfirmDeliveryAsync(customerId, id, secureCode) Task
    }
    class INotificationService {
        <<interface>>
        +CreateOrderStatusNotificationAsync(order, status) Task
        +GetMineAsync(userId) Task~IReadOnlyList~Notification~
        +MarkReadAsync(userId, id) Task
    }
    class CustomerAddressService
    class PackageService
    class DeliveryOrderService
    class NotificationService

    class CustomerAddress {
        +Guid Id
        +Guid UserId
        +string Label
        +string ContactName
        +string ContactPhone
        +string AddressLine
        +decimal? Latitude
        +decimal? Longitude
        +bool IsDefault
    }
    class Package {
        +Guid Id
        +Guid SenderId
        +string PackageName
        +decimal WeightGram
        +decimal LengthCm
        +decimal WidthCm
        +decimal HeightCm
        +string? PackageImageUrl
        +bool IsFragile
    }
    class DeliveryOrder {
        +Guid Id
        +string TrackingNumber
        +Guid CustomerId
        +Guid PackageId
        +Guid OriginStationId
        +Guid DestinationStationId
        +Guid? DeliveryAddressId
        +OrderStatus Status
        +string PickupSecureCode [6 digits]
        +string RecipientName
        +string RecipientPhone
    }
    class DeliveryStatusLog {
        +long Id
        +Guid OrderId
        +Guid? ChangedBy
        +string? FromStatus
        +string ToStatus
        +DateTime CreatedAt
    }
    class Notification {
        +Guid Id
        +Guid UserId
        +Guid? OrderId
        +string Type
        +string Title
        +string Message
        +bool IsRead
        +DateTime? ReadAt
    }
    class User {
        +Guid Id
        +string FullName
        +string PhoneNumber
    }
    class LandingStation {
        +Guid Id
        +string StationCode
        +string Name
        +string Status
    }
    class OrderStatus {
        <<enumeration>>
        PENDING
        APPROVED
        READY_FOR_TAKEOFF
        IN_TRANSIT
        ARRIVED
        DELIVERED
        CANCELLED
        FAILED
    }

    class AppDbContext
    class CreateCustomerAddressDto
    class UpdateCustomerAddressDto
    class CustomerAddressResponseDto
    class CreatePackageDto
    class UpdatePackageDto
    class PackageResponseDto
    class CreateDeliveryOrderDto
    class UpdateDeliveryOrderDto {
        +string RecipientName
        +string RecipientPhone
        +Guid DestinationStationId
        +Guid? DeliveryAddressId
    }
    class CancelDeliveryOrderDto {
        +string CancellationReason
    }
    class ConfirmDeliveryDto
    class DeliveryOrderFilterParams
    class DeliveryOrderDetailResponseDto
    class DeliveryStatusLogResponseDto
    class NotificationResponseDto
    class PagedResponse

    CustomerAddressesController ..> ICustomerAddressService : uses
    PackagesController ..> IPackageService : uses
    DeliveryOrdersController ..> IDeliveryOrderService : uses
    NotificationsController ..> INotificationService : uses

    CustomerAddressService ..|> ICustomerAddressService
    PackageService ..|> IPackageService
    DeliveryOrderService ..|> IDeliveryOrderService
    NotificationService ..|> INotificationService
    DeliveryOrderService ..> INotificationService : status notification
    CustomerAddressService ..> AppDbContext : persists
    PackageService ..> AppDbContext : persists
    DeliveryOrderService ..> AppDbContext : persists
    NotificationService ..> AppDbContext : persists

    CustomerAddressesController ..> CreateCustomerAddressDto : binds
    CustomerAddressesController ..> UpdateCustomerAddressDto : binds
    CustomerAddressesController ..> CustomerAddressResponseDto : returns
    PackagesController ..> CreatePackageDto : binds
    PackagesController ..> UpdatePackageDto : binds
    PackagesController ..> PackageResponseDto : returns
    DeliveryOrdersController ..> CreateDeliveryOrderDto : binds
    DeliveryOrdersController ..> UpdateDeliveryOrderDto : binds
    DeliveryOrdersController ..> CancelDeliveryOrderDto : binds
    DeliveryOrdersController ..> ConfirmDeliveryDto : binds
    DeliveryOrdersController ..> DeliveryOrderFilterParams : binds
    DeliveryOrdersController ..> DeliveryOrderDetailResponseDto : returns
    NotificationsController ..> NotificationResponseDto : returns

    AppDbContext ..> CustomerAddress : DbSet
    AppDbContext ..> Package : DbSet
    AppDbContext ..> DeliveryOrder : DbSet
    AppDbContext ..> DeliveryStatusLog : DbSet
    AppDbContext ..> Notification : DbSet
    AppDbContext ..> User : DbSet
    AppDbContext ..> LandingStation : DbSet

    User "1" --> "0..*" CustomerAddress : owns
    User "1" --> "0..*" Package : sends
    User "1" --> "0..*" DeliveryOrder : customer
    User "0..1" --> "0..*" DeliveryOrder : dispatcher
    User "0..1" --> "0..*" DeliveryStatusLog : changes
    User "1" --> "0..*" Notification : receives
    LandingStation "1" --> "0..*" DeliveryOrder : origin
    LandingStation "1" --> "0..*" DeliveryOrder : destination
    CustomerAddress "0..1" --> "0..*" DeliveryOrder : optional address
    Package "1" --> "0..1" DeliveryOrder : one order per package
    DeliveryOrder "1" --> "0..*" DeliveryStatusLog : history
    DeliveryOrder "0..1" --> "0..*" Notification : related notifications
    DeliveryOrder ..> OrderStatus : status
    DeliveryOrderDetailResponseDto ..> DeliveryStatusLogResponseDto : contains history
    PagedResponse ..> DeliveryOrderDetailResponseDto : page items
```

The diagram uses `0..1` for optional relationships and `0..*` for collections. A package can be unassigned or linked to one order; `DeliveryOrder.PackageId` is unique in the ERD. Origin and destination are separate required references to `LandingStation`.

Package measurements are constrained to `0 < WeightGram <= 5000`, `0 < LengthCm <= 40`, `0 < WidthCm <= 30`, and `0 < HeightCm <= 30`.

`NotificationsController` is included to provide the read/mark-as-read operations in UC-10. The three core controllers requested for address, package, and order management remain the primary module endpoints.

## Main processing flows

### Create delivery order

1. Require an authenticated `CUSTOMER`; take the customer ID from the authenticated principal, never from a client-supplied owner field.
2. Validate the request and confirm the selected package belongs to that customer and is not already assigned to an order. Confirm origin and destination are distinct, valid stations; validate an optional delivery address belongs to the same customer.
3. Check that package weight is at most `5000` grams and dimensions are at most `40 x 30 x 30` cm.
4. Generate a unique `TrackingNumber`. Enforce uniqueness with the database unique constraint and handle a collision by generating another value. Generate a random six-digit pickup secure code using a cryptographically secure random source.
5. In one database transaction, create the order with `PENDING`, add its initial `DeliveryStatusLog` (`FromStatus = null`, `ToStatus = PENDING`), and create the customer's order notification.
6. Commit and return `201 Created` with the order detail/location. Do not expose the secure code in an ordinary order-detail response; use an explicitly authorized delivery channel/view.

### Update and cancel an order

- Update only `RecipientName`, `RecipientPhone`, `DestinationStationId`, and `DeliveryAddressId` on an order owned by the signed-in customer and still before approval (`PENDING`). `PackageId`, `OriginStationId`, and `Status` are not updateable through this DTO. Reject edits after approval or another disallowed transition with `409 Conflict`; validate ownership of a replacement address and the destination station.
- Cancellation requires a non-empty `CancellationReason` and an order in a cancellable state. Within a transaction, set `CANCELLED`, map `CancellationReason` to the `cancel_reason` column, append a status log, and notify the customer. The exact cancellable statuses are not specified by the SRS and must be agreed before implementation.
- A failure after database commit but before an external push is sent must not silently lose the notification; use a durable notification/outbox strategy if delivery is asynchronous.

### Manage addresses and packages

- Address list/read/update/delete/default operations are always scoped to the authenticated customer. Setting a default must make the default unique for that customer in a transaction.
- An address referenced by an existing order must not cause historical order data to be deleted or changed. The optional order-to-address FK and delete behavior should be preserved (for example, detach the FK or reject deletion); choose and test the final policy before implementation.
- Package create/update is customer-scoped. Enforce `0 < WeightGram <= 5000`, `0 < LengthCm <= 40`, `0 < WidthCm <= 30`, and `0 < HeightCm <= 30`, along with optional image metadata validation. Do not allow reassignment or deletion to bypass the one-package/one-order invariant. Package capacity is checked when creating an order and again at physical intake where appropriate.
