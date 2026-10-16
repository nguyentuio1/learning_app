# LA-227: Sequence Diagram - Create Delivery Order

## 1. Tổng quan và phạm vi (Introduction & Scope)

Tài liệu đặc tả luồng tương tác giữa các lớp trong hệ thống khi khách hàng tạo đơn giao hàng (UC-05). Luồng bao quát việc nhập thông tin đơn hàng, xác thực và kiểm tra quy tắc nghiệp vụ, lưu đơn hàng cùng lịch sử trạng thái, tạo thông báo và trả kết quả cho ứng dụng khách hàng. Phạm vi liên quan đến FR-07, FR-08, FR-09 và FR-12.

### Participants

| Participant | Vai trò / tầng |
|---|---|
| Customer | Actor — khách hàng khởi tạo yêu cầu |
| Client | Customer Mobile/Web App — giao diện khách hàng |
| Controller | `DeliveryOrdersController` — Presentation |
| OrderSvc | `IDeliveryOrderService` / `DeliveryOrderService` — Application |
| PkgSvc | `IPackageService` / `PackageService` — Application |
| NotifSvc | `INotificationService` / `NotificationService` — Application |
| DbContext | `AppDbContext` / PostgreSQL DB — Domain / Data Access |

## 2. Sơ đồ tuần tự Mermaid (Mermaid Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    title LA-227: Sequence Diagram - Create Delivery Order Flow (UC-05, FR-07, FR-08, FR-09, FR-12)

    actor Customer as Customer (Actor)
    participant Client as Customer Mobile/Web App
    participant Controller as DeliveryOrdersController<br/>(Presentation)
    participant OrderSvc as IDeliveryOrderService / DeliveryOrderService<br/>(Application)
    participant PkgSvc as IPackageService / PackageService<br/>(Application)
    participant NotifSvc as INotificationService / NotificationService<br/>(Application)
    participant DbContext as AppDbContext / PostgreSQL DB<br/>(Domain / Data)

    %% Step 1: Customer UI Interaction
    Customer ->> Client: Selects package, origin/dest stations, inputs recipient details
    Client ->> Client: Client-side validation (Required fields, phone format)

    alt Client-side Validation Fails
        Client -->> Customer: Displays form error messages
    else Client-side Validation Passes
        %% Step 2: HTTP POST Request
        Client ->> Controller: POST /api/delivery-orders<br/>(Header: Authorization with JWT access token, Body: CreateDeliveryOrderDto)

        %% Step 3: Auth & Role Validation
        alt Authentication / Authorization Fails (Token invalid, expired or Role != CUSTOMER)
            Controller -->> Client: 401 Unauthorized / 403 Forbidden (RFC 7807 ProblemDetails)
            Client -->> Customer: Displays login prompt or permission error
        else Auth & Role Valid
            Controller ->> OrderSvc: CreateOrderAsync(customerId, createDto)

            %% Step 4: Business Rules & Data Validation
            OrderSvc ->> PkgSvc: GetPackageByIdAsync(createDto.PackageId)
            PkgSvc ->> DbContext: packages.FirstOrDefaultAsync(p => p.Id == packageId)
            DbContext -->> PkgSvc: Return Package Entity / null
            PkgSvc -->> OrderSvc: Return Package Entity / null

            OrderSvc ->> DbContext: Check Origin & Destination stations exist & ACTIVE
            DbContext -->> OrderSvc: Station Entities / null Status

            alt Business Validation Fails<br/>(Package not found, Package not owned by customer,<br/>Weight/Dimensions exceed drone limit, Origin == Destination, Station inactive)
                OrderSvc -->> Controller: Throws ValidationException / BusinessRuleException
                Controller -->> Client: 400 Bad Request / 404 Not Found (RFC 7807 ProblemDetails)
                Client -->> Customer: Displays specific error (e.g. "Package exceeds max weight limit")
            else All Business Rules Validated
                %% Step 5: Entity Generation & Code Generation
                Note over OrderSvc: 1. Generate Unique TrackingNumber (e.g., "SDD20261015X8K9")<br/>2. Generate 6-digit SecureCode (e.g., "839201")<br/>3. Set Status = PENDING

                %% Step 6: Database Transaction & Persistence
                OrderSvc ->> DbContext: Begin Transaction
                OrderSvc ->> DbContext: AddAsync(DeliveryOrder Entity)
                OrderSvc ->> DbContext: AddAsync(DeliveryStatusLog Entity: From=null, To=PENDING)
                OrderSvc ->> DbContext: SaveChangesAsync()
                DbContext -->> OrderSvc: Transaction Committed Successfully

                %% Step 7: Notification Service Integration (FR-12)
                OrderSvc ->> NotifSvc: CreateNotificationAsync(customerId, orderId, "Order Created Successfully", PENDING)
                NotifSvc ->> DbContext: AddAsync(Notification Entity)
                NotifSvc ->> DbContext: SaveChangesAsync()
                DbContext -->> NotifSvc: Saved
                NotifSvc -->> OrderSvc: Notification Created

                %% Step 8: Return Response DTO
                OrderSvc -->> Controller: Return DeliveryOrderDetailResponseDto
                Controller -->> Client: 201 Created (Location: /api/delivery-orders/{id}, Body: DeliveryOrderDetailResponseDto)
                Client -->> Customer: Displays Order Created Screen (Tracking Number & Secure Code)
            end
        end
    end
```

## 3. Diễn giải luồng (Interaction Flow)

1. Khách hàng chọn kiện hàng, ga gửi và ga nhận, rồi nhập thông tin người nhận. Ứng dụng kiểm tra các trường bắt buộc và định dạng số điện thoại trước khi gửi yêu cầu.
2. Khi dữ liệu hợp lệ, ứng dụng gửi `POST /api/delivery-orders` kèm JWT trong header `Authorization` và `CreateDeliveryOrderDto`. Controller xác thực danh tính và quyền `CUSTOMER`, sau đó chuyển tiếp yêu cầu cùng `customerId` tới dịch vụ đơn hàng.
3. Dịch vụ đơn hàng lấy kiện hàng qua `IPackageService` và kiểm tra các ga. Các quy tắc nghiệp vụ gồm sự tồn tại và quyền sở hữu kiện hàng, giới hạn trọng lượng/kích thước, hai ga khác nhau và trạng thái hoạt động của ga.
4. Nếu hợp lệ, dịch vụ tạo mã theo dõi duy nhất, mã bảo mật 6 chữ số, đặt trạng thái ban đầu là `PENDING`, rồi lưu đơn hàng và bản ghi lịch sử trạng thái trong transaction.
5. Sau khi lưu đơn hàng, dịch vụ tạo thông báo cho khách hàng qua `INotificationService`. Cuối cùng, phản hồi thành công chứa `DeliveryOrderDetailResponseDto` được trả về với mã HTTP `201 Created` và URI của đơn hàng mới.

## 4. Kết quả và nhánh ngoại lệ (Responses & Exception Paths)

| Tình huống | Kết quả trong luồng |
|---|---|
| Kiểm tra phía client không đạt | Ứng dụng hiển thị lỗi biểu mẫu; không gửi yêu cầu tới API. |
| JWT không hợp lệ/hết hạn hoặc vai trò không phải `CUSTOMER` | API trả `401 Unauthorized` hoặc `403 Forbidden` với `ProblemDetails`; ứng dụng yêu cầu đăng nhập hoặc thông báo lỗi quyền. |
| Dữ liệu nghiệp vụ không hợp lệ | API trả `400 Bad Request` hoặc `404 Not Found` với `ProblemDetails`; ứng dụng hiển thị lỗi cụ thể. |
| Tạo đơn hàng thành công | API trả `201 Created`, `Location` và `DeliveryOrderDetailResponseDto`; màn hình kết quả hiển thị mã theo dõi và mã bảo mật. |
