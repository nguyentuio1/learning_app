# LA-228: Sequence Diagram - Update/Cancel Delivery Order

## 1. Overview and Scope

This document specifies the customer interaction flows for updating an order (UC-06, FR-10) and cancelling an order with a reason (UC-06, FR-10, FR-12, FR-14). The update flow checks ownership, order status, and the submitted changes before saving. The cancellation flow validates the reason, checks whether the order can be cancelled, records the status history, and creates a customer notification.

### Participants

| Participant | Role / layer |
|---|---|
| Customer | Actor — customer initiating the request |
| Client | Customer Mobile/Web App — customer interface |
| Controller | `DeliveryOrdersController` — Presentation |
| OrderSvc | `IDeliveryOrderService` / `DeliveryOrderService` — Application |
| NotifSvc | `INotificationService` / `NotificationService` — Application |
| DbContext | `AppDbContext` / PostgreSQL DB — Domain / Data Access |

## 2. Mermaid Sequence Diagrams

### 2.1. Update Order Flow

```mermaid
sequenceDiagram
    autonumber
    title LA-228: Sequence Diagram - Update Delivery Order Flow (UC-06, FR-10)

    actor Customer as Customer (Actor)
    participant Client as Customer Mobile/Web App
    participant Controller as DeliveryOrdersController<br/>(Presentation)
    participant OrderSvc as IDeliveryOrderService / DeliveryOrderService<br/>(Application)
    participant DbContext as AppDbContext / PostgreSQL DB<br/>(Domain / Data)

    %% Step 1: Customer UI Interaction
    Customer ->> Client: Edits order details (RecipientName, RecipientPhone, DestinationStationId, DeliveryAddressId)
    Client ->> Client: Client-side validation (Required fields, phone format)
    Note over Client: UpdateDeliveryOrderDto contains exactly RecipientName, RecipientPhone, DestinationStationId, and DeliveryAddressId.<br/>PackageId and Status are not editable through this request.
    Note over OrderSvc: Package limits are validated at order creation, not updated here: WeightGram <= 5000 (5.0 kg), LengthCm <= 40 cm, WidthCm <= 30 cm, HeightCm <= 30 cm.

    alt Client-side Validation Fails
        Client -->> Customer: Displays form error messages
    else Client-side Validation Passes
        %% Step 2: HTTP PUT Request
        Client ->> Controller: PUT /api/delivery-orders/{id}<br/>(Header: Authorization: Bearer access token, Body: UpdateDeliveryOrderDto)

        alt Request attempts to include PackageId or Status
            Controller -->> Client: 400 Bad Request (RFC 7807 ProblemDetails, field is not allowed)
            Client -->> Customer: Displays "Package and order status cannot be changed"
        else Request contains only the four allowed fields
        %% Step 3: Auth & Role Validation
        alt Authentication / Authorization Fails (Token invalid, expired or Role != CUSTOMER)
            Controller -->> Client: 401 Unauthorized / 403 Forbidden (RFC 7807 ProblemDetails)
            Client -->> Customer: Displays login prompt or permission error
        else Auth & Role Valid
            Controller ->> OrderSvc: UpdateOrderAsync(id, customerId, updateDto)

            %% Step 4: Fetch Order & Ownership Check
            OrderSvc ->> DbContext: delivery_orders.FirstOrDefaultAsync(o => o.Id == id)
            DbContext -->> OrderSvc: Return DeliveryOrder Entity / null

            alt Order Not Found OR Order.CustomerId != customerId
                OrderSvc -->> Controller: Throws NotFoundException
                Controller -->> Client: 404 Not Found (RFC 7807 ProblemDetails)
                Client -->> Customer: Displays "Order not found or permission denied"
            else Order Exists & Owned by Customer
                %% Step 5: Business Rule Validation (FR-10)
                alt Order.Status != PENDING (Order is APPROVED, IN_TRANSIT, DELIVERED, CANCELLED, etc.)
                    OrderSvc -->> Controller: Throws BusinessRuleException("Order cannot be modified after approval")
                    Controller -->> Client: 400 Bad Request (RFC 7807 ProblemDetails)
                    Client -->> Customer: Displays "Cannot update order that is already approved or processed"
                else Order Status == PENDING
                    %% Step 6: Validate New Destination Station / Address (if changed)
                    alt DestinationStationId changed & Invalid (Station not found, not OPERATIONAL, or Same as Origin)
                        OrderSvc -->> Controller: Throws ValidationException
                        Controller -->> Client: 400 Bad Request (RFC 7807 ProblemDetails)
                        Client -->> Customer: Displays specific error (e.g. "Destination station is invalid or not operational")
                    else New Data Valid
                        %% Step 7: Apply Updates & Persistence
                        OrderSvc ->> OrderSvc: Update properties (RecipientName, RecipientPhone, DestinationStationId, DeliveryAddressId, UpdatedAt)
                        OrderSvc ->> DbContext: SaveChangesAsync()
                        DbContext -->> OrderSvc: Changes Saved

                        %% Step 8: Return Response DTO
                        OrderSvc -->> Controller: Return DeliveryOrderDetailResponseDto
                        Controller -->> Client: 200 OK (Body: DeliveryOrderDetailResponseDto)
                        Client -->> Customer: Displays "Order updated successfully" notification & updated order details
                    end
                end
                end
        end
                        end
    end
```

### 2.2. Cancel Order Flow

```mermaid
sequenceDiagram
    autonumber
    title LA-228: Sequence Diagram - Cancel Delivery Order Flow (UC-06, FR-10, FR-12, FR-14)

    actor Customer as Customer (Actor)
    participant Client as Customer Mobile/Web App
    participant Controller as DeliveryOrdersController<br/>(Presentation)
    participant OrderSvc as IDeliveryOrderService / DeliveryOrderService<br/>(Application)
    participant NotifSvc as INotificationService / NotificationService<br/>(Application)
    participant DbContext as AppDbContext / PostgreSQL DB<br/>(Domain / Data)

    %% Step 1: Customer UI Interaction
    Customer ->> Client: Selects "Cancel Order" & inputs CancellationReason
    Client ->> Client: Client-side validation (CancellationReason is required and non-empty)

    alt Cancellation Reason Empty / Whitespace
        Client -->> Customer: Displays "Please provide a reason for cancellation"
    else Validation Passes
        %% Step 2: HTTP POST Request
        Client ->> Controller: POST /api/delivery-orders/{id}/cancel<br/>(Header: Authorization: Bearer access token, Body: CancelDeliveryOrderDto)

        %% Step 3: Auth & Role Validation
        alt Authentication / Authorization Fails (Token invalid, expired or Role != CUSTOMER)
            Controller -->> Client: 401 Unauthorized / 403 Forbidden (RFC 7807 ProblemDetails)
            Client -->> Customer: Displays login prompt or permission error
        else Auth & Role Valid
            Controller ->> OrderSvc: CancelOrderAsync(id, customerId, cancelDto)

            %% Step 4: Fetch Order & Ownership Check
            OrderSvc ->> DbContext: delivery_orders.FirstOrDefaultAsync(o => o.Id == id)
            DbContext -->> OrderSvc: Return DeliveryOrder Entity / null

            alt Order Not Found OR Order.CustomerId != customerId
                OrderSvc -->> Controller: Throws NotFoundException
                Controller -->> Client: 404 Not Found (RFC 7807 ProblemDetails)
                Client -->> Customer: Displays "Order not found or permission denied"
            else Order Exists & Owned by Customer
                %% Step 5: Validate Cancellation Reason
                alt cancelDto.CancellationReason is null or whitespace
                    OrderSvc -->> Controller: Throws ValidationException("Cancellation reason is required")
                    Controller -->> Client: 400 Bad Request (RFC 7807 ProblemDetails)
                    Client -->> Customer: Displays "Cancellation reason cannot be empty"
                else Reason Provided
                    %% Step 6: Validate Status for Cancellation (FR-10, FR-14)
                    alt Order.Status NOT IN (PENDING, APPROVED) (Order is IN_TRANSIT, DELIVERED, CANCELLED, FAILED)
                        OrderSvc -->> Controller: Throws BusinessRuleException("Order cannot be cancelled in its current status")
                        Controller -->> Client: 400 Bad Request (RFC 7807 ProblemDetails)
                        Client -->> Customer: Displays "Cannot cancel order once flight has started or order is finalized"
                    else Order Status IS Cancellable (PENDING or APPROVED)
                        %% Step 7: Database Transaction & Persistence (FR-14)
                        OrderSvc ->> DbContext: Begin Transaction
                        Note over OrderSvc: Save oldStatus = Order.Status<br/>Set Order.Status = CANCELLED<br/>Set Order.CancellationReason = CancellationReason<br/>Set Order.UpdatedAt = DateTime.UtcNow

                        OrderSvc ->> DbContext: AddAsync(DeliveryStatusLog Entity:<br/>FromStatus=oldStatus, ToStatus=CANCELLED,<br/>ChangedByUserId=customerId, Notes=CancellationReason)
                        OrderSvc ->> DbContext: SaveChangesAsync()
                        DbContext -->> OrderSvc: Transaction Committed Successfully

                        %% Step 8: Create Customer Notification (FR-12)
                        OrderSvc ->> NotifSvc: CreateNotificationAsync(customerId, id,<br/>"Order " + TrackingNumber + " was cancelled successfully.", CANCELLED)
                        NotifSvc ->> DbContext: AddAsync(Notification Entity)
                        NotifSvc ->> DbContext: SaveChangesAsync()
                        DbContext -->> NotifSvc: Saved
                        NotifSvc -->> OrderSvc: Notification Created

                        %% Step 9: Return Response DTO
                        OrderSvc -->> Controller: Return DeliveryOrderResponseDto (Status=CANCELLED)
                        Controller -->> Client: 200 OK (Body: DeliveryOrderResponseDto)
                        Client -->> Customer: Displays "Order cancelled successfully" screen
                    end
                end
            end
        end
    end
```

## 3. Interaction Flow

1. **Update:** The customer edits the recipient details, destination station, or delivery address. The `UpdateDeliveryOrderDto` contains only `RecipientName`, `RecipientPhone`, `DestinationStationId`, and `DeliveryAddressId`; `PackageId` and `Status` cannot be updated. Package limits (`WeightGram <= 5000` g (5.0 kg), `LengthCm <= 40` cm, `WidthCm <= 30` cm, and `HeightCm <= 30` cm) are checked when the order is created, not in this update flow. A request that includes either prohibited field is rejected with `400 Bad Request`.
2. The controller authenticates the customer and checks the `CUSTOMER` role, then passes `id`, `customerId`, and the update data to the order service. The service verifies that the order exists, belongs to the customer, and is still in `PENDING` status.
3. The service checks that the new destination station is `OPERATIONAL` and differs from the origin station, and validates the delivery address if it has changed. If validation succeeds, the service updates the order properties and saves the changes; the API returns `200 OK` with a `DeliveryOrderDetailResponseDto`.
4. **Cancel:** The customer enters a non-empty cancellation reason. The application sends `POST /api/delivery-orders/{id}/cancel` with the JWT and a `CancelDeliveryOrderDto`; the service then checks ownership, the reason, and the order status.
5. Only orders in `PENDING` or `APPROVED` status can be cancelled. The service sets the status to `CANCELLED` and saves `CancellationReason` and a `DeliveryStatusLog` record (`FromStatus` → `CANCELLED`, `ChangedByUserId = customerId`, `Notes = CancellationReason`) in a transaction. `INotificationService` then creates a customer notification; the API returns `200 OK` with a `DeliveryOrderResponseDto`.

## 4. Responses and Exception Paths

| Scenario | Flow result |
|---|---|
| Scenario | Flow result |
|---|---|
| Client-side validation fails | The application displays form errors and does not send a request to the API. |
| JWT is invalid or expired, or the role is not `CUSTOMER` | The API returns `401 Unauthorized` or `403 Forbidden` with `ProblemDetails`; the application prompts the customer to sign in or displays a permission error. |
| The order cannot be found or does not belong to the customer | The API returns `404 Not Found` with `ProblemDetails`; the application reports that the order could not be found or accessed. |
| The order is in a state that does not permit updating or cancellation, or the submitted data/reason is invalid | The API returns `400 Bad Request` with `ProblemDetails`; the application displays the corresponding error. |
| An update request attempts to include `PackageId` or `Status` | The API rejects the disallowed field with `400 Bad Request`; neither field is part of `UpdateDeliveryOrderDto`. |
| The order is updated successfully | The API returns `200 OK` with a `DeliveryOrderDetailResponseDto`; the application displays the updated order details. |
| The order is cancelled successfully | The API returns `200 OK` with a `DeliveryOrderResponseDto`; the cancellation status and history are saved, and the customer is notified. |
