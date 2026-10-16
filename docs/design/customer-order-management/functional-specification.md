# Functional Specification — Customer & Order Management

| Thuộc tính | Giá trị |
|---|---|
| Task | LA-216 — Functions of Customer and Order module |
| Phạm vi | Customer & Order Management |
| Nguồn truy vết | [SRS](../../requirements/SRS.md), [Use Cases](../../requirements/use-cases.md) |
| Tài liệu thiết kế liên quan | [Class Specification](./class-specification.md), [Create Order Sequence](./sequence-create-order.md), [Update/Cancel Order Sequence](./sequence-update-cancel-order.md) |

## 1. Scope & Boundaries

Tài liệu định nghĩa mục tiêu, actor, điều kiện tiên quyết, dữ liệu vào/ra, quy tắc nghiệp vụ và phản hồi ngoại lệ cho sáu nhóm chức năng:

1. Address Management — quản lý địa chỉ giao hàng.
2. Package Management — quản lý kiện hàng.
3. Order Creation — tạo đơn hàng.
4. Order Update — cập nhật đơn hàng.
5. Order Cancellation — hủy đơn hàng.
6. Order Listing & Details — xem danh sách và chi tiết đơn hàng.

Đối tượng chính của các chức năng này là khách hàng (`CUSTOMER`). Xác thực yêu cầu JWT hợp lệ; quyền sở hữu đơn, địa chỉ và kiện hàng được xác định từ danh tính đã xác thực, không lấy từ dữ liệu do client tự khai báo.

Đặc tả này mô tả hợp đồng chức năng, không chứa mã nguồn C#, triển khai API/controller, UI components hoặc database migrations. Tên DTO và trường dưới đây là hợp đồng logic; giới hạn được ghi là “đề xuất” cần được xác nhận nếu chưa có trong yêu cầu nguồn. `Guid`, `decimal`, `bool`, `int`, `string` và `DateTime` lần lượt biểu thị mã định danh, số thập phân, logic, số nguyên, chuỗi và thời điểm ISO 8601/UTC.

### Điều kiện truy cập chung

- JWT thiếu, không hợp lệ hoặc hết hạn: `401 Unauthorized`.
- Người dùng đã xác thực nhưng không có vai trò `CUSTOMER`, hoặc tài khoản không hoạt động: `403 Forbidden`.
- Không trả dữ liệu của người dùng khác; tài nguyên không tồn tại và tài nguyên không thuộc người gọi đều có thể trả `404 Not Found` để tránh tiết lộ quyền sở hữu.
- `400 Bad Request` dùng cho dữ liệu/điều kiện nghiệp vụ đầu vào không hợp lệ; `409 Conflict` dùng cho xung đột trạng thái, uniqueness hoặc quan hệ tài nguyên.

## 2. Detailed Functional Specs

### 2.1 Address Management — Quản lý địa chỉ

#### Purpose / Actor

CUSTOMER tạo, xem danh sách, cập nhật, xóa địa chỉ giao hàng và chọn một địa chỉ mặc định.

#### Requirements Traceability

- **FR-06**, **UC-03**.

#### Preconditions

- Đáp ứng điều kiện truy cập chung.
- Với thao tác trên địa chỉ có sẵn, địa chỉ phải tồn tại và thuộc khách hàng hiện tại.

#### Inputs / Outputs

**Input — `CreateCustomerAddressDto` / `UpdateCustomerAddressDto`:**

| Trường | Kiểu | Yêu cầu |
|---|---|---|
| `Label` | string | Bắt buộc; tối đa 50 ký tự. |
| `ContactName` | string | Bắt buộc; tối đa 150 ký tự. |
| `ContactPhone` | string | Bắt buộc; đúng định dạng số điện thoại; tối đa 20 ký tự. |
| `AddressLine` | string | Bắt buộc; không rỗng/khoảng trắng. |
| `Latitude` | decimal? | Tùy chọn; -90 đến 90. |
| `Longitude` | decimal? | Tùy chọn; -180 đến 180. |
| `IsDefault` | bool | Tùy chọn; mặc định `false`. |

`UserId` lấy từ JWT và không được chỉ định trong body. Tọa độ phải được gửi cùng nhau hoặc cả hai đều để trống.

**Output — `CustomerAddressResponseDto`:** `Id`, `Label`, `ContactName`, `ContactPhone`, `AddressLine`, `Latitude?`, `Longitude?`, `IsDefault`, `CreatedAt`, `UpdatedAt`. Danh sách chỉ chứa địa chỉ của CUSTOMER hiện tại.

#### Business Rules

1. Các trường bắt buộc không được null/rỗng; xác thực giới hạn ký tự, định dạng điện thoại và miền tọa độ.
2. Mỗi khách hàng có nhiều nhất một địa chỉ `IsDefault = true`. Đặt một địa chỉ làm mặc định phải đồng thời bỏ mặc định địa chỉ trước đó.
3. Không cho phép đọc/sửa/xóa địa chỉ của khách hàng khác.
4. Chính sách xóa địa chỉ đang được đơn hàng tham chiếu cần được chốt; không được làm mất lịch sử/thông tin giao hàng.
5. Giới hạn số lượng địa chỉ được đề xuất là 10 địa chỉ/khách hàng; cần xác nhận tại mục 4.

#### Exceptional Cases

| HTTP | Trường hợp |
|---|---|
| `401` | JWT thiếu, hết hạn hoặc không hợp lệ. |
| `403` | Sai role hoặc tài khoản không hoạt động. |
| `404` | Địa chỉ không tồn tại/không thuộc người gọi. |
| `400` | Thiếu trường, vượt giới hạn ký tự, điện thoại sai định dạng, tọa độ ngoài miền hoặc chỉ gửi một trong hai tọa độ. |
| `409` | Xung đột đặt mặc định, vượt giới hạn địa chỉ đã chốt, hoặc địa chỉ đang được đơn hàng tham chiếu theo chính sách xóa đã chọn. |

### 2.2 Package Management — Quản lý kiện hàng

#### Purpose / Actor

CUSTOMER tạo, xem và cập nhật kiện hàng để lựa chọn khi tạo đơn giao hàng.

#### Requirements Traceability

- **FR-07**, **UC-04**.

#### Preconditions

- Đáp ứng điều kiện truy cập chung.
- Với cập nhật, kiện hàng phải tồn tại và thuộc CUSTOMER hiện tại.

#### Inputs / Outputs

**Input — `CreatePackageDto` / `UpdatePackageDto`:**

| Trường | Kiểu | Yêu cầu |
|---|---|---|
| `PackageName` | string | Bắt buộc; tối đa 200 ký tự. |
| `WeightGram` | decimal | Bắt buộc; lớn hơn 0 và tối đa 5000 gram (5.0 kg). |
| `LengthCm` | decimal | Bắt buộc; lớn hơn 0 và tối đa 40 cm. |
| `WidthCm` | decimal | Bắt buộc; lớn hơn 0 và tối đa 30 cm. |
| `HeightCm` | decimal | Bắt buộc; lớn hơn 0 và tối đa 30 cm. |
| `PackageImageUrl` | string? | Không bắt buộc; URL/tham chiếu ảnh hợp lệ nếu có. |
| `IsFragile` | bool | Tùy chọn; mặc định `false`. |

`SenderId` lấy từ JWT; client không được gán chủ sở hữu.

**Output — `PackageResponseDto`:** `Id`, `PackageName`, `WeightGram`, `LengthCm`, `WidthCm`, `HeightCm`, `PackageImageUrl?`, `IsFragile`, `CreatedAt`.

#### Business Rules

1. Kích thước tối đa là **40 × 30 × 30 cm** theo `LengthCm`, `WidthCm`, `HeightCm`; từng chiều và trọng lượng phải lớn hơn 0.
2. Tải trọng tối đa là **5.0 kg** (`WeightGram <= 5000`). Kiểm tra lại giới hạn khi tạo đơn.
3. Quan hệ là **One Package One Order**: một kiện chỉ được gắn với tối đa một đơn hàng.
4. Không cho xóa kiện hàng đang được đơn hàng tham chiếu (**RESTRICT**).
5. Chỉ chủ kiện mới được xem/cập nhật; cập nhật không được làm vi phạm giới hạn hoặc tính hợp lệ của đơn hàng đã gắn.

#### Exceptional Cases

| HTTP | Trường hợp |
|---|---|
| `401` | JWT thiếu, hết hạn hoặc không hợp lệ. |
| `403` | Sai role hoặc tài khoản không hoạt động. |
| `404` | Kiện hàng không tồn tại/không thuộc người gọi. |
| `400` | Tên thiếu/quá dài, trọng lượng hoặc kích thước không dương/vượt giới hạn, URL ảnh không hợp lệ. |
| `409` | Kiện đã được gắn với đơn khác; xóa kiện đang được đơn hàng tham chiếu. |

### 2.3 Order Creation — Tạo đơn hàng

#### Purpose / Actor

CUSTOMER tạo yêu cầu giao hàng bằng cách chọn kiện hàng, trạm gửi/nhận, người nhận và địa chỉ giao hàng tùy chọn.

#### Requirements Traceability

- **FR-08**, **FR-09**, **FR-12**, **FR-14**, **UC-05**, **UC-10**.

#### Preconditions

- Đáp ứng điều kiện truy cập chung.
- Kiện hàng tồn tại, thuộc khách hàng hiện tại, hợp lệ theo giới hạn drone và chưa được dùng cho đơn khác.
- Hai trạm tồn tại, đều có trạng thái `OPERATIONAL`, và có ID khác nhau.
- Nếu cung cấp địa chỉ, địa chỉ phải thuộc khách hàng hiện tại.

#### Inputs / Outputs

**Input — `CreateDeliveryOrderDto`:**

| Trường | Kiểu | Yêu cầu |
|---|---|---|
| `PackageId` | Guid | Bắt buộc; kiện của người gọi, chưa gắn đơn khác. |
| `OriginStationId` | Guid | Bắt buộc; trạm `OPERATIONAL`. |
| `DestinationStationId` | Guid | Bắt buộc; trạm `OPERATIONAL`, khác trạm gửi. |
| `DeliveryAddressId` | Guid? | Tùy chọn; nếu có phải thuộc người gọi. |
| `RecipientName` | string | Bắt buộc; tối đa 150 ký tự. |
| `RecipientPhone` | string | Bắt buộc; đúng định dạng số điện thoại, tối đa 20 ký tự. |

Client không gửi `CustomerId`, trạng thái, tracking number, secure code hoặc timestamp hệ thống.

**Output — `DeliveryOrderDetailResponseDto` (phản hồi tạo đơn):** `Id`, `TrackingNumber`, `Status`, tóm tắt kiện hàng/trạm, địa chỉ giao hàng nếu có, `RecipientName`, `RecipientPhone`, timestamps và lịch sử trạng thái ban đầu. Secure code được hiển thị trong phản hồi tạo đơn theo luồng UC-05; không ghi mã vào log.

#### Business Rules

1. Máy chủ kiểm tra quyền sở hữu kiện/địa chỉ, trạng thái hai trạm và giới hạn drone **5.0 kg, 40 × 30 × 30 cm**.
2. Trạm gửi và trạm nhận phải khác nhau và cùng `OPERATIONAL` tại thời điểm xử lý.
3. Sinh `TrackingNumber` duy nhất và secure code ngẫu nhiên gồm đúng 6 chữ số; định dạng tracking number cụ thể vẫn cần xác nhận theo mục 4.
4. Đơn mới có trạng thái `PENDING`.
5. Ghi `DeliveryStatusLog` ban đầu (`FromStatus = null`, `ToStatus = PENDING`) cùng với đơn hàng trong transaction.
6. Sau khi tạo đơn thành công, tạo notification cho khách hàng theo FR-12/UC-10. Không tạo thông báo trạng thái sai nếu thao tác lưu đơn thất bại.
7. Một kiện hàng không thể được liên kết với đơn thứ hai.

#### Exceptional Cases

| HTTP | Trường hợp |
|---|---|
| `401` | JWT thiếu, hết hạn hoặc không hợp lệ. |
| `403` | Sai role hoặc tài khoản không hoạt động. |
| `404` | Không tìm thấy kiện hàng hoặc một trong các trạm/địa chỉ. |
| `400` | Thiếu/sai dữ liệu người nhận; kiện không thuộc người gọi hoặc vượt giới hạn; trạm không `OPERATIONAL`; trạm gửi và nhận trùng nhau. |
| `409` | Kiện đã được gắn với đơn khác hoặc xung đột uniqueness khi cấp tracking number. |

### 2.4 Order Update — Cập nhật đơn hàng

#### Purpose / Actor

CUSTOMER cập nhật thông tin người nhận hoặc điểm giao trước khi đơn được duyệt.

#### Requirements Traceability

- **FR-10**, **UC-06**.

#### Preconditions

- Đáp ứng điều kiện truy cập chung.
- Đơn tồn tại, thuộc CUSTOMER hiện tại và đang ở trạng thái `PENDING`.

#### Inputs / Outputs

**Input — `UpdateDeliveryOrderDto`:** chỉ gồm đúng bốn trường sau:

| Trường | Kiểu | Yêu cầu |
|---|---|---|
| `RecipientName` | string | Bắt buộc; tối đa 150 ký tự. |
| `RecipientPhone` | string | Bắt buộc; đúng định dạng số điện thoại, tối đa 20 ký tự. |
| `DestinationStationId` | Guid | Bắt buộc; ID hợp lệ của trạm nhận. |
| `DeliveryAddressId` | Guid? | Tùy chọn; nếu có phải là địa chỉ của CUSTOMER hiện tại. |

`PackageId`, `OriginStationId`, `Status`, `CustomerId`, `TrackingNumber` và `PickupSecureCode` không thuộc DTO cập nhật.

**Output — `DeliveryOrderDetailResponseDto`:** thông tin đơn sau cập nhật, không bao gồm secure code.

#### Business Rules

1. Chỉ cho phép sửa khi trạng thái tại thời điểm lưu vẫn là `PENDING` (FR-10).
2. Chỉ được cập nhật đúng bốn trường trong DTO; cấm thay đổi kiện hàng hoặc trạng thái.
3. Trạm nhận mới phải tồn tại, `OPERATIONAL` và khác trạm gửi.
4. Địa chỉ giao hàng nếu có phải thuộc CUSTOMER đang thao tác.
5. Nếu request chứa trường ngoài phạm vi, từ chối thay vì âm thầm bỏ qua.

#### Exceptional Cases

| HTTP | Trường hợp |
|---|---|
| `401` | JWT thiếu, hết hạn hoặc không hợp lệ. |
| `403` | Sai role hoặc tài khoản không hoạt động. |
| `404` | Đơn không tồn tại/không thuộc người gọi; địa chỉ hoặc trạm không tìm thấy. |
| `400` | Trường bắt buộc thiếu/sai định dạng; gửi `PackageId`/`Status` hoặc trường không được phép; trạm đích không hoạt động hoặc trùng trạm gửi. |
| `409` | Đơn đã chuyển khỏi `PENDING` trước khi cập nhật được lưu. |

### 2.5 Order Cancellation — Hủy đơn hàng

#### Purpose / Actor

CUSTOMER hủy đơn còn trong giai đoạn cho phép và cung cấp lý do hủy.

#### Requirements Traceability

- **FR-10**, **FR-12**, **FR-14**, **UC-06**, **UC-10**.

#### Preconditions

- Đáp ứng điều kiện truy cập chung.
- Đơn tồn tại và thuộc CUSTOMER hiện tại.
- Chỉ đơn `PENDING` hoặc `APPROVED` được hủy bởi CUSTOMER; `IN_TRANSIT` không được hủy.

#### Inputs / Outputs

**Input — `CancelDeliveryOrderDto`:**

| Trường | Kiểu | Yêu cầu |
|---|---|---|
| `CancellationReason` | string | Bắt buộc, không rỗng/khoảng trắng; tối đa 500 ký tự. |

**Output — `DeliveryOrderResponseDto`:** thông tin đơn đã chuyển sang `CANCELLED`, lý do hủy và thời gian cập nhật.

#### Business Rules

1. Chỉ cho phép hủy khi trạng thái hiện tại là `PENDING` hoặc `APPROVED`.
2. Lưu `CancellationReason` trên đơn và ghi `DeliveryStatusLog` từ trạng thái cũ sang `CANCELLED`, bao gồm người thực hiện và thời điểm.
3. Cập nhật trạng thái và log trong cùng transaction; sau khi thành công, tạo notification cho khách hàng theo FR-12.
4. Không cho hủy khi `IN_TRANSIT`, đã hoàn tất hoặc đã hủy; yêu cầu lặp không tạo log/thông báo lặp.

#### Exceptional Cases

| HTTP | Trường hợp |
|---|---|
| `401` | JWT thiếu, hết hạn hoặc không hợp lệ. |
| `403` | Sai role hoặc tài khoản không hoạt động. |
| `404` | Đơn không tồn tại/không thuộc người gọi. |
| `400` | `CancellationReason` thiếu, rỗng hoặc vượt 500 ký tự. |
| `409` | Trạng thái hiện tại không cho phép hủy, bao gồm `IN_TRANSIT`, `COMPLETED` hoặc `CANCELLED`. |

### 2.6 Order Listing & Details — Danh sách và chi tiết đơn hàng

#### Purpose / Actor

CUSTOMER xem các đơn của mình, lọc/phân trang danh sách và mở chi tiết gồm lịch sử trạng thái cùng dữ liệu ETA nếu có.

#### Requirements Traceability

- **FR-09**, **FR-11**, **FR-12**, **FR-24**, **FR-26**, **FR-29**, **FR-30**.
- **UC-05**, **UC-07**, **UC-08**, **UC-10**, **UC-20**. UC-08/FR-26 là luồng xác nhận giao tích hợp; actor nhận mã cần được phân quyền riêng theo đặc tả workflow.

#### Preconditions

- Đáp ứng điều kiện truy cập chung.
- Chi tiết đơn phải thuộc CUSTOMER hiện tại.

#### Inputs / Outputs

**Tham số lọc danh sách:**

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `status` | string hoặc string[] | Tùy chọn; chỉ trạng thái hợp lệ. Hỗ trợ mảng là open issue. |
| `fromDate` | DateTime? | Tùy chọn; ISO 8601. |
| `toDate` | DateTime? | Tùy chọn; ISO 8601, không sớm hơn `fromDate`. |
| `searchKeyword` | string? | Tùy chọn; tìm theo tracking number hoặc từ khóa được hỗ trợ. |
| `page` | int | Tùy chọn; mặc định 1, tối thiểu 1. |
| `pageSize` | int | Tùy chọn; mặc định 20, trong khoảng 1–100. |

**Output danh sách — `PagedResult<DeliveryOrderResponseDto>`:** `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`. Mỗi item có `Id`, `TrackingNumber`, `Status`, thông tin kiện/trạm và `CreatedAt`/`UpdatedAt`; không chứa `PickupSecureCode`.

**Output chi tiết — `DeliveryOrderDetailResponseDto`:** thông tin đơn, kiện, trạm, địa chỉ và người nhận phù hợp quyền truy cập; `StatusLogs[]` (mỗi log có trạng thái trước/sau, remarks, actor nếu được phép, thời điểm); `AiEtaPrediction?` theo UC-20/FR-29. Secure code không được đưa vào DTO chi tiết chung hoặc log; luồng trả/kiểm tra mã chỉ dành cho actor đã được xác nhận quyền.

#### Business Rules

1. Luôn giới hạn kết quả theo customer ID từ JWT; không dùng ID khách hàng do client gửi để mở rộng phạm vi.
2. Hỗ trợ lọc theo trạng thái, ngày tạo, từ khóa; kiểm tra `fromDate <= toDate`. Sắp xếp mặc định theo thời gian tạo mới nhất trước.
3. Áp dụng phân trang với `page >= 1`, `1 <= pageSize <= 100`.
4. Trả lịch sử `StatusLogs[]` theo thời gian tăng dần; ETA/dự báo AI có thể không có và khi đó trả null.
5. Thông tin notification liên quan đơn tuân theo FR-12/UC-10; dữ liệu của khách khác không bao giờ được trả về.
6. Vị trí drone real-time là khả năng theo dõi FR-24, không đồng nghĩa với snapshot ETA/lịch sử trong response chi tiết.

#### Exceptional Cases

| HTTP | Trường hợp |
|---|---|
| `401` | JWT thiếu, hết hạn hoặc không hợp lệ. |
| `403` | Sai role hoặc tài khoản không hoạt động. |
| `404` | Không tìm thấy đơn hoặc đơn không thuộc người gọi. |
| `400` | Trạng thái không hợp lệ; ngày sai định dạng hoặc `fromDate > toDate`; page/pageSize ngoài giới hạn; từ khóa vượt giới hạn đã cấu hình. |
| `409` | Chỉ áp dụng nếu hợp đồng API quy định trạng thái lọc xung đột là conflict; mặc định lỗi tham số lọc trả `400`. |

## 3. Traceability Matrix

| Nhóm Chức Năng | Tên Chức Năng Cụ Thể | Mã FR / UC Liên Quan | Trạng Thái Xác Nhận trong Source |
|---|---|---|---|
| 1 | Address Management — tạo, sửa, xóa, liệt kê, đặt mặc định | FR-06 / UC-03 | SRS/use-cases xác nhận thao tác và actor; trường/validation chi tiết theo class specification. Ngưỡng 10 địa chỉ là đề xuất cần chốt. |
| 2 | Package Management — tạo, cập nhật kiện hàng | FR-07 / UC-04 | SRS/use-cases xác nhận thuộc tính nghiệp vụ; class specification xác nhận giới hạn 5000 g, 40×30×30 cm và quan hệ 1-1. |
| 3 | Order Creation — tạo đơn, sinh mã, trạng thái ban đầu và notification | FR-08, FR-09, FR-12, FR-14 / UC-05, UC-10 | SRS/use-cases và sequence-create-order xác nhận luồng; `OPERATIONAL`, `PENDING`, log ban đầu và notification được thể hiện trong tài liệu thiết kế. Format TrackingNumber cần chốt. |
| 4 | Order Update — sửa bốn trường trước duyệt | FR-10 / UC-06 | Class specification và sequence-update-cancel-order quy định DTO chỉ có RecipientName, RecipientPhone, DestinationStationId, DeliveryAddressId; chỉ sửa khi PENDING. |
| 5 | Order Cancellation — hủy có lý do, ghi log và notification | FR-10, FR-12, FR-14 / UC-06, UC-10 | Sequence-update-cancel-order xác nhận CancellationReason bắt buộc, chỉ PENDING/APPROVED, chuyển CANCELLED, log và notification. |
| 6 | Order Listing & Details — lọc, phân trang, lịch sử, ETA | FR-09, FR-11, FR-12, FR-24, FR-26, FR-29, FR-30 / UC-05, UC-07, UC-08, UC-10, UC-20 | SRS/use-cases xác nhận đơn của khách, tracking, notification, secure-code confirmation và ETA/summary. Hỗ trợ mảng trạng thái và quyền hiển thị secure code cần chốt. |

## 4. Open Issues

1. **Giới hạn địa chỉ:** đề xuất tối đa **10 địa chỉ/khách hàng**. Xác nhận xử lý khi vượt giới hạn, xóa địa chỉ mặc định và xóa địa chỉ đang được đơn hàng tham chiếu.
2. **Tái sử dụng Package:** xác nhận kiện hàng có thể được gắn với đơn mới sau khi đơn trước kết thúc hay không. Mặc định hiện tại là One Package One Order; không tái sử dụng khi chưa có quyết định.
3. **Mảng trạng thái trong Filter:** xác nhận bộ lọc danh sách chấp nhận một trạng thái hay nhiều trạng thái (ví dụ `status=PENDING,APPROVED`), cách mã hóa và phản hồi khi một trạng thái không hợp lệ.
