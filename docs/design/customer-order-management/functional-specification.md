# Functional Specification — Customer & Order Management

| Thuộc tính | Giá trị |
|---|---|
| Task | LA-216 — Functions of Customer and Order module |
| Module | Customer & Order Management |
| Trạng thái | Bản đặc tả chức năng |
| Tài liệu nguồn | [`SRS.md`](../../requirements/SRS.md), [`use-cases.md`](../../requirements/use-cases.md) |

## 1. Phạm vi tài liệu

Tài liệu đặc tả hành vi, điều kiện, dữ liệu vào/ra, quy tắc nghiệp vụ và trường hợp lỗi của sáu nhóm chức năng:

1. Quản lý địa chỉ (Address management)
2. Quản lý kiện hàng (Package management)
3. Tạo đơn hàng (Order creation)
4. Cập nhật đơn hàng (Order update)
5. Hủy đơn hàng (Order cancellation)
6. Xem danh sách và chi tiết đơn hàng (Order listing & details)

Đối tượng sử dụng chính của cả sáu nhóm là **CUSTOMER**. Tài liệu không bao gồm mã nguồn C#, API/controller implementation, thành phần giao diện (UI components) hoặc database migrations. Các DTO bên dưới mô tả hợp đồng dữ liệu ở mức chức năng, không khẳng định các DTO đó đã được triển khai.

### 1.1 Quy ước và mức độ xác nhận

- `Guid`, `string`, `bool`, `decimal`, `int` và `DateTime` là kiểu dữ liệu logic; thời gian trao đổi dùng ISO 8601, ưu tiên UTC.
- `Required` là bắt buộc khi tạo; trường chỉ rõ `nullable` có thể bỏ trống.
- Các giới hạn tải trọng/kích thước và quy tắc nghiệp vụ được nêu rõ trong yêu cầu LA-216 được xem là baseline của thiết kế.
- SRS xác nhận mã FR và phạm vi use case, nhưng không quy định tất cả giới hạn độ dài, tên DTO, trạng thái tài khoản cụ thể hay hợp đồng HTTP. Những giới hạn độ dài đề xuất dưới đây là ràng buộc thiết kế để kiểm tra và cần được Leader chốt trước khi triển khai.
- Khi tài liệu nguồn không nêu chi tiết (ví dụ, hủy ở trạng thái nào), phần tương ứng được đánh dấu là chi tiết thiết kế cần xác nhận; không coi đó là nội dung đã được SRS xác nhận.

### 1.2 Điều kiện truy cập chung

Mỗi yêu cầu cần có JWT hợp lệ, vai trò `CUSTOMER`, và tài khoản đang hoạt động (không bị vô hiệu hóa/khóa). Quyền sở hữu được kiểm tra ở phía máy chủ dựa trên danh tính trong token; không tin `userId`/`customerId` do client gửi. Vi phạm xác thực/trạng thái tài khoản trả `401`; sai vai trò trả `403`.

## 2. Functional specification details

### 2.1 Quản lý địa chỉ (Address management)

#### Purpose & Actor

Cho phép CUSTOMER tạo, xem danh sách, cập nhật, xóa địa chỉ giao hàng và chọn địa chỉ mặc định để dùng khi tạo đơn.

#### Requirements traceability

- **FR-06** — Tạo, cập nhật, xóa, liệt kê địa chỉ giao hàng và đánh dấu địa chỉ mặc định.
- **UC-03** — Manage delivery addresses (actor: Customer).

#### Preconditions

- Đáp ứng điều kiện truy cập chung ở mục 1.2.
- Với cập nhật, xóa hoặc đặt mặc định: địa chỉ tồn tại và thuộc CUSTOMER hiện tại.

#### Inputs & Outputs

**Tạo/cập nhật địa chỉ — DTO chức năng `AddressUpsertInput`:**

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `label` | string | Bắt buộc; 1–50 ký tự sau trim (đề xuất). |
| `contactName` | string | Bắt buộc; 1–100 ký tự sau trim (đề xuất). |
| `contactPhone` | string | Bắt buộc; 1–20 ký tự, định dạng số điện thoại được chấp nhận theo quy tắc hệ thống (đề xuất). |
| `addressLine` | string | Bắt buộc; 1–255 ký tự sau trim (đề xuất). |
| `latitude` | decimal \| null | Tùy chọn; từ -90 đến 90. |
| `longitude` | decimal \| null | Tùy chọn; từ -180 đến 180. |
| `isDefault` | bool | Tùy chọn; mặc định `false`. |

`userId`, `id`, `createdAt`, `updatedAt` không phải trường client có quyền gán. `userId` được lấy từ JWT.

**DTO trả về `AddressDto`:** `id: Guid`, `userId: Guid`, `label`, `contactName`, `contactPhone`, `addressLine: string`, `latitude`, `longitude: decimal?`, `isDefault: bool`, `createdAt`, `updatedAt: DateTime`. Danh sách trả về `items: AddressDto[]` (có phân trang nếu được API hỗ trợ).

#### Main business logic & rules

1. Chỉ chủ sở hữu mới được đọc hoặc thay đổi địa chỉ. Không tiết lộ địa chỉ thuộc user khác.
2. Mỗi CUSTOMER có tối đa một địa chỉ `isDefault = true`. Khi chọn địa chỉ mới làm mặc định, hệ thống bỏ mặc định địa chỉ cũ trong cùng thao tác nguyên tử.
3. Trường bắt buộc không được null/rỗng sau trim; tọa độ nếu có phải nằm trong miền hợp lệ.
4. Không cho xóa địa chỉ đang được tham chiếu bởi đơn hàng để không làm mất thông tin giao nhận; phản hồi xung đột.
5. Số lượng địa chỉ tối đa và cách xử lý khi xóa địa chỉ mặc định cuối cùng cần Leader xác nhận (mục 4).

#### Exceptional / error cases

| HTTP | Tình huống |
|---|---|
| `401` | JWT thiếu/hết hạn, hoặc tài khoản không hoạt động. |
| `403` | Người gọi không có vai trò `CUSTOMER`. |
| `404` | Địa chỉ không tồn tại hoặc không thuộc CUSTOMER hiện tại. |
| `400` | Thiếu trường bắt buộc, vượt giới hạn độ dài, số điện thoại hoặc tọa độ không hợp lệ. |
| `409` | Xóa địa chỉ đang được đơn hàng tham chiếu; hoặc xung đột khi cập nhật trạng thái mặc định. |

### 2.2 Quản lý kiện hàng (Package management)

#### Purpose & Actor

Cho phép CUSTOMER đăng ký, xem, cập nhật và quản lý thông tin kiện hàng sẽ được giao.

#### Requirements traceability

- **FR-07** — Tạo/cập nhật kiện hàng với tên, trọng lượng, kích thước, cờ dễ vỡ và ảnh tùy chọn.
- **UC-04** — Manage packages (actor: Customer).

#### Preconditions

- Đáp ứng điều kiện truy cập chung ở mục 1.2.
- Với cập nhật/xóa: kiện hàng tồn tại và `senderId` thuộc CUSTOMER hiện tại.

#### Inputs & Outputs

**Tạo/cập nhật kiện — DTO chức năng `PackageUpsertInput`:**

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `packageName` | string | Bắt buộc; 1–100 ký tự sau trim (đề xuất). |
| `weightGram` | decimal | Bắt buộc; `> 0` và `<= 5000` gram (tương đương tối đa 5.0 kg). |
| `lengthCm` | decimal | Bắt buộc; `> 0` và `<= 40` cm. |
| `widthCm` | decimal | Bắt buộc; `> 0` và `<= 30` cm. |
| `heightCm` | decimal | Bắt buộc; `> 0` và `<= 30` cm. |
| `isFragile` | bool | Tùy chọn; mặc định `false`. |
| `packageImageUrl` | string \| null | Tùy chọn; tối đa 2048 ký tự (đề xuất), chỉ nhận URL hợp lệ nếu có. |

`senderId`, `id`, `createdAt` do hệ thống xác định. **Lưu ý:** thực thể hiện có dùng đơn vị `weightGram`; ngưỡng nghiệp vụ `5.0 kg` cần được quy đổi nhất quán khi nhập/xuất dữ liệu.

**DTO trả về `PackageDto`:** `id: Guid`, `senderId: Guid`, `packageName: string`, `weightGram`, `lengthCm`, `widthCm`, `heightCm: decimal`, `isFragile: bool`, `packageImageUrl: string?`, `createdAt: DateTime`, và `isUsedByOrder: bool` nếu cần để hiển thị khả năng xóa.

#### Main business logic & rules

1. Mỗi kiện hàng chỉ thuộc một CUSTOMER; chỉ chủ sở hữu được xem/sửa/xóa.
2. Tải trọng tối đa là **5.0 kg** (`weightGram <= 5000`); kích thước tối đa theo trục là **40 × 30 × 30 cm** (`lengthCm <= 40`, `widthCm <= 30`, `heightCm <= 30`). Các giá trị phải lớn hơn 0.
3. Quan hệ kiện hàng–đơn hàng là **One Package One Order**: một kiện hàng không được gắn vào nhiều đơn hàng. Cho đến khi Leader chốt chính sách tái sử dụng, không gán lại kiện đã liên kết với đơn khác.
4. Không cho xóa kiện hàng đang được đơn hàng tham chiếu (**RESTRICT**); trả `409 Conflict`.
5. Ảnh không bắt buộc. Quy tắc lưu trữ/tải ảnh nằm ngoài phạm vi đặc tả này.

#### Exceptional / error cases

| HTTP | Tình huống |
|---|---|
| `401` | JWT thiếu/hết hạn, hoặc tài khoản không hoạt động. |
| `403` | Người gọi không có vai trò `CUSTOMER`. |
| `404` | Kiện hàng không tồn tại hoặc không thuộc CUSTOMER hiện tại. |
| `400` | Tên thiếu/quá dài; trọng lượng hoặc kích thước không dương/vượt giới hạn; ảnh không hợp lệ. |
| `409` | Kiện hàng đã được dùng cho đơn hàng khác hoặc đang được đơn hàng tham chiếu khi xóa. |

### 2.3 Tạo đơn hàng (Order creation)

#### Purpose & Actor

Cho phép CUSTOMER chọn kiện hàng, trạm gửi/nhận và thông tin người nhận để tạo yêu cầu giao hàng.

#### Requirements traceability

- **FR-08** — Tạo đơn cho kiện hàng, trạm đi/đến và thông tin người nhận.
- **FR-09** — Sinh tracking number và secure code duy nhất; đặt trạng thái ban đầu `PENDING`.
- **FR-12** — Tạo thông báo cho khách hàng khi trạng thái đơn thay đổi; UC-05 cũng nêu hệ thống thông báo sau khi tạo đơn.
- **FR-14** — Ghi nhận chuyển trạng thái hợp lệ.
- **UC-05** — Create delivery order (actor: Customer).

#### Preconditions

- Đáp ứng điều kiện truy cập chung ở mục 1.2.
- Kiện hàng tồn tại, thuộc CUSTOMER hiện tại và chưa gắn với đơn khác.
- Trạm gửi và trạm nhận tồn tại, đều ở trạng thái `OPERATIONAL`, đồng thời có ID khác nhau.
- Nếu gửi `deliveryAddressId`, địa chỉ phải tồn tại và thuộc CUSTOMER hiện tại.

#### Inputs & Outputs

**DTO yêu cầu `OrderCreateInput`:**

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `packageId` | Guid | Bắt buộc; kiện hàng của CUSTOMER và chưa được sử dụng. |
| `originStationId` | Guid | Bắt buộc; trạm đang `OPERATIONAL`. |
| `destinationStationId` | Guid | Bắt buộc; trạm đang `OPERATIONAL`, khác `originStationId`. |
| `deliveryAddressId` | Guid \| null | Tùy chọn; nếu có phải thuộc CUSTOMER hiện tại. |
| `recipientName` | string | Bắt buộc; 1–100 ký tự sau trim (đề xuất). |
| `recipientPhone` | string | Bắt buộc; 1–20 ký tự, định dạng số điện thoại hợp lệ (đề xuất). |

**DTO trả về `OrderCreatedDto`:** `id: Guid`, `trackingNumber: string`, `customerId`, `packageId`, `originStationId`, `destinationStationId: Guid`, `deliveryAddressId: Guid?`, `recipientName`, `recipientPhone: string`, `status: "PENDING"`, `pickupSecureCode: string`, `createdAt: DateTime`.

#### Main business logic & rules

1. Máy chủ xác thực quyền sở hữu kiện hàng/địa chỉ và kiểm tra lại giới hạn tải trọng, kích thước của kiện trước khi tạo.
2. Hai trạm phải đang `OPERATIONAL` tại thời điểm xác nhận; trạm đi và đến không được trùng nhau.
3. Sinh `trackingNumber` theo mẫu **`SDD` + `YYYYMMDD` + 6 ký tự**. Ngày dùng ngày UTC tại thời điểm tạo; 6 ký tự phải bảo đảm uniqueness toàn hệ thống, có xử lý thử lại khi phát hiện va chạm.
4. Sinh `pickupSecureCode` gồm đúng **6 chữ số**, giữ được số 0 ở đầu; giá trị phải được tạo bằng bộ sinh số ngẫu nhiên an toàn.
5. Tạo đơn với `status = PENDING`. Trong cùng giao dịch, ghi `delivery_status_logs` với trạng thái trước rỗng/null và trạng thái sau `PENDING`.
6. Tạo thông báo xác nhận cho CUSTOMER theo hành vi UC-05; việc chuyển trạng thái phải tuân theo FR-12. Không để lại đơn nếu tạo log bắt buộc thất bại.
7. Khi thành công, đơn hàng và kiện hàng được liên kết theo quy tắc một kiện/một đơn. Không cho client chỉ định `customerId`, trạng thái, tracking number, secure code hoặc trường thời gian hệ thống.

#### Exceptional / error cases

| HTTP | Tình huống |
|---|---|
| `401` | JWT thiếu/hết hạn, hoặc tài khoản không hoạt động. |
| `403` | Người gọi không có vai trò `CUSTOMER`. |
| `404` | Kiện, địa chỉ hoặc một trong các trạm không tồn tại/không thuộc người gọi. |
| `400` | Thiếu dữ liệu người nhận; hai trạm trùng nhau; trạm không `OPERATIONAL`; kiện vượt giới hạn drone. |
| `409` | Kiện đã được gắn với đơn khác hoặc tài nguyên/trạng thái thay đổi đồng thời làm yêu cầu không còn hợp lệ. |

### 2.4 Cập nhật đơn hàng (Order update)

#### Purpose & Actor

Cho phép CUSTOMER sửa thông tin giao nhận của đơn trước khi đơn được duyệt.

#### Requirements traceability

- **FR-10** — CUSTOMER chỉ sửa đơn trước khi được phê duyệt; FR này cũng bao gồm hủy đơn có lý do.
- **UC-06** — Update / cancel delivery order (actor: Customer).

#### Preconditions

- Đáp ứng điều kiện truy cập chung ở mục 1.2.
- Đơn tồn tại, thuộc CUSTOMER hiện tại và có `status == PENDING`.

#### Inputs & Outputs

**DTO yêu cầu `OrderUpdateInput` (PATCH):**

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `recipientName` | string \| null | Nếu gửi: 1–100 ký tự sau trim (đề xuất). |
| `recipientPhone` | string \| null | Nếu gửi: 1–20 ký tự, số điện thoại hợp lệ (đề xuất). |
| `destinationStationId` | Guid \| null | Nếu gửi: trạm tồn tại, `OPERATIONAL`, khác trạm đi hiện tại. |
| `deliveryAddressId` | Guid \| null | Nếu gửi ID: địa chỉ thuộc CUSTOMER hiện tại; null có thể dùng để bỏ liên kết địa chỉ. |

Ít nhất một trường được phép phải được gửi. Các trường bị cấm gồm `packageId`/`package_id`, `status`, `customerId`, `originStationId`, `trackingNumber` và `pickupSecureCode`.

**DTO trả về `OrderDto`:** đơn đã cập nhật với `id`, `trackingNumber`, `status`, thông tin người nhận, `destinationStationId`, `deliveryAddressId`, `updatedAt` và các trường hiển thị hợp lệ khác.

#### Main business logic & rules

1. Chỉ cho phép cập nhật khi trạng thái hiện tại là `PENDING` (**FR-10**). Kiểm tra trạng thái tại thời điểm ghi để tránh race condition với thao tác duyệt.
2. Phạm vi sửa duy nhất: `recipientName`, `recipientPhone`, `destinationStationId`, `deliveryAddressId`.
3. Không cho sửa kiện (`package_id`), trạng thái, trạm đi hoặc mã định danh đơn.
4. Trạm nhận mới phải `OPERATIONAL` và khác trạm đi. Địa chỉ mới phải thuộc chủ đơn.
5. Cập nhật không làm thay đổi lịch sử trạng thái; ghi thời gian `updatedAt`.

#### Exceptional / error cases

| HTTP | Tình huống |
|---|---|
| `401` | JWT thiếu/hết hạn, hoặc tài khoản không hoạt động. |
| `403` | Người gọi không có vai trò `CUSTOMER`. |
| `404` | Đơn không tồn tại hoặc không thuộc CUSTOMER hiện tại. |
| `400` | Không có trường cập nhật; có trường ngoài phạm vi; dữ liệu người nhận không hợp lệ; trạm đi/đến trùng hoặc trạm đích không hoạt động. |
| `409` | Đơn không còn `PENDING` tại thời điểm cập nhật (đã được duyệt hoặc trạng thái đã thay đổi). |

### 2.5 Hủy đơn hàng (Order cancellation)

#### Purpose & Actor

Cho phép CUSTOMER yêu cầu hủy đơn còn trong giai đoạn có thể hủy và lưu rõ lý do.

#### Requirements traceability

- **FR-10** — Hủy đơn với lý do khi việc hủy còn được phép.
- **FR-14** — Ghi log chuyển trạng thái.
- **FR-12** — Tạo thông báo khi trạng thái đơn thay đổi.
- **UC-06** — Update / cancel delivery order (actor: Customer).

#### Preconditions

- Đáp ứng điều kiện truy cập chung ở mục 1.2.
- Đơn tồn tại và thuộc CUSTOMER hiện tại.
- Theo baseline của LA-216, trạng thái hiện tại phải là `PENDING` hoặc `APPROVED`; `IN_TRANSIT` không thể hủy bởi CUSTOMER.

#### Inputs & Outputs

**DTO yêu cầu `OrderCancellationInput`:**

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `cancellationReason` | string | Bắt buộc; 1–500 ký tự sau trim (độ dài tối đa đề xuất). |

**DTO trả về `OrderCancellationDto`:** `id: Guid`, `trackingNumber: string`, `status: "CANCELLED"`, `cancellationReason: string`, `updatedAt: DateTime`.

#### Main business logic & rules

1. Không nhận lý do rỗng/null; lưu lý do gắn với đơn (`CancelReason`).
2. Chỉ hủy khi trạng thái là `PENDING` hoặc `APPROVED` theo thiết kế LA-216. Từ chối hủy `IN_TRANSIT`; các trạng thái khác ngoài tập cho phép cũng không được hủy.
3. Trong cùng giao dịch, chuyển trạng thái sang `CANCELLED`, ghi `delivery_status_logs` gồm `fromStatus`, `toStatus`, người thực hiện, thời điểm và lý do/ghi chú (**FR-14**).
4. Tạo thông báo trạng thái mới cho CUSTOMER (**FR-12**). Ghi log và thông báo cần nhất quán với chuyển trạng thái.
5. Thao tác hủy lặp lại không tạo thêm log/thông báo; trả xung đột do đơn đã ở trạng thái cuối.

#### Exceptional / error cases

| HTTP | Tình huống |
|---|---|
| `401` | JWT thiếu/hết hạn, hoặc tài khoản không hoạt động. |
| `403` | Người gọi không có vai trò `CUSTOMER`. |
| `404` | Đơn không tồn tại hoặc không thuộc CUSTOMER hiện tại. |
| `400` | Thiếu lý do hủy hoặc lý do chỉ gồm khoảng trắng/vượt giới hạn. |
| `409` | Đơn `IN_TRANSIT`, đã hoàn tất/đã hủy, hoặc trạng thái hiện tại không cho phép CUSTOMER hủy. |

### 2.6 Xem danh sách và chi tiết đơn hàng (Order listing & details)

#### Purpose & Actor

Cho phép CUSTOMER tra cứu các đơn của chính mình theo bộ lọc, phân trang và xem chi tiết trạng thái, lịch sử, mã nhận hàng và dự báo ETA.

#### Requirements traceability

- **FR-11** — Liệt kê đơn của chính CUSTOMER với bộ lọc và xem lịch sử trạng thái.
- **FR-24** — Cập nhật trạng thái/vị trí drone cho client theo thời gian thực (liên quan hiển thị theo dõi).
- **FR-29** — Ước lượng thời gian giao hàng và cập nhật khi điều kiện thay đổi.
- Mã secure code được tạo theo **FR-09 / UC-05**; xác nhận giao hàng bằng mã thuộc **FR-26 / UC-08**.
- **UC-07** — Track delivery order; **UC-20** — View ETA and delivery summary.

#### Preconditions

- Đáp ứng điều kiện truy cập chung ở mục 1.2.
- Với chi tiết: đơn phải thuộc CUSTOMER hiện tại. Không cho phép truy vấn bằng ID để đọc đơn của người khác.

#### Inputs & Outputs

**Tham số danh sách:**

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `status` | string hoặc danh sách string | Tùy chọn; từng giá trị phải thuộc tập trạng thái được hệ thống hỗ trợ. Hỗ trợ nhiều trạng thái cần Leader xác nhận. |
| `fromDate` | DateTime \| null | Tùy chọn; ISO 8601. |
| `toDate` | DateTime \| null | Tùy chọn; ISO 8601 và không sớm hơn `fromDate`. |
| `searchKeyword` | string \| null | Tùy chọn; tối đa 100 ký tự sau trim (đề xuất), tìm theo tracking number/người nhận nếu được hỗ trợ. |
| `page` | int | Tùy chọn; mặc định 1, tối thiểu 1. |
| `pageSize` | int | Tùy chọn; mặc định 20, từ 1 đến 100. |

**DTO danh sách `OrderListItemDto`:** `id: Guid`, `trackingNumber: string`, `status: string`, `recipientName: string`, `originStationId`, `destinationStationId: Guid`, `createdAt`, `updatedAt: DateTime`, `estimatedDeliveryTime: DateTime?`, `aiEtaPrediction: AiEtaPredictionDto?`.

**DTO phân trang `PagedResult<OrderListItemDto>`:** `items: OrderListItemDto[]`, `page: int`, `pageSize: int`, `totalCount: int`, `totalPages: int`.

**DTO chi tiết `OrderDetailDto`:** thông tin đơn/kiện/trạm/địa chỉ và người nhận được phép xem; `pickupSecureCode: string`; `statusLogs: StatusLogDto[]`; `aiEtaPrediction: AiEtaPredictionDto?`. Mỗi `StatusLogDto` gồm `fromStatus: string?`, `toStatus: string`, `remarks: string?`, `changedBy: Guid?`, `createdAt: DateTime`. `AiEtaPredictionDto` gồm `calculatedDistanceKm: decimal`, `predictedDurationMinutes: int`, `weatherCondition: string?`, `confidenceScore: decimal?`, `createdAt: DateTime`.

#### Main business logic & rules

1. Mọi truy vấn danh sách/chi tiết bắt buộc lọc theo `customerId` lấy từ JWT, kể cả khi client truyền điều kiện khác. Không trả PII hoặc dữ liệu đơn của người dùng khác.
2. Lọc theo trạng thái, `fromDate`, `toDate`, `searchKeyword`; kiểm tra `fromDate <= toDate`. Phân trang ổn định, có thứ tự mặc định theo `createdAt` giảm dần.
3. `statusLogs[]` được sắp theo `createdAt` tăng dần để thể hiện tiến trình. ETA có thể null nếu chưa có dự đoán.
4. `pickupSecureCode` là dữ liệu nhạy cảm: chỉ trả trong chi tiết đơn của đúng CUSTOMER sở hữu; không đưa vào danh sách, log ứng dụng hoặc kết quả tìm kiếm. Việc lộ mã cần được hạn chế ở lớp truy cập và truyền tải an toàn.
5. `aiEtaPrediction` phản ánh dự đoán hiện có; không tự tạo dự đoán nếu chưa có dữ liệu. Vị trí drone trực tiếp/real-time thuộc phạm vi theo dõi (FR-24) và không được đảm bảo bởi DTO lịch sử này.

#### Exceptional / error cases

| HTTP | Tình huống |
|---|---|
| `401` | JWT thiếu/hết hạn, hoặc tài khoản không hoạt động. |
| `403` | Người gọi không có vai trò `CUSTOMER`. |
| `404` | Đơn chi tiết không tồn tại hoặc không thuộc CUSTOMER hiện tại. |
| `400` | Trạng thái không hợp lệ; ngày sai định dạng hoặc `fromDate > toDate`; `page`/`pageSize` ngoài giới hạn; từ khóa quá dài. |

## 3. Traceability matrix

| Nhóm Chức Năng | Tên Chức Năng Cụ Thể | Mã FR / UC Liên Quan | Trạng Thái Xác Nhận trong Source |
|---|---|---|---|
| 1 | Address management — tạo, sửa, xóa, liệt kê, chọn mặc định | FR-06 / UC-03 | FR/UC và hành vi mức cao được xác nhận trong SRS/use-cases. Giới hạn độ dài, số lượng tối đa và quy tắc địa chỉ mặc định là chi tiết thiết kế; cần chốt. |
| 2 | Package management — tạo, cập nhật và quản lý kiện | FR-07 / UC-04 | FR/UC và các thuộc tính chính được xác nhận trong SRS/use-cases. Ngưỡng 5 kg, kích thước và RESTRICT theo baseline LA-216; quy tắc tái sử dụng cần chốt. |
| 3 | Order creation — tạo đơn, mã theo dõi, secure code, trạng thái ban đầu | FR-08, FR-09, FR-12, FR-14 / UC-05 | FR/UC và flow tạo đơn PENDING/mã/notification được xác nhận. Mẫu mã cụ thể, trạng thái trạm `OPERATIONAL`, log khởi tạo là chi tiết thiết kế LA-216. |
| 4 | Order update — sửa thông tin giao nhận trước duyệt | FR-10 / UC-06 | FR/UC xác nhận chỉ sửa trước phê duyệt; danh sách trường sửa và cấm sửa package/status là baseline LA-216, chưa được SRS liệt kê chi tiết. |
| 5 | Order cancellation — hủy có lý do, ghi log và thông báo | FR-10, FR-12, FR-14 / UC-06 | FR/UC xác nhận hủy có lý do khi còn được phép. Tập trạng thái PENDING/APPROVED và chặn IN_TRANSIT là baseline LA-216, cần Leader xác nhận với workflow. |
| 6 | Order listing & details — lọc, phân trang, lịch sử, secure code, ETA | FR-09, FR-11, FR-24, FR-26, FR-29 / UC-05, UC-07, UC-08, UC-20 | FR/UC xác nhận đơn của chính khách, lịch sử, theo dõi và ETA. Bộ lọc cụ thể, phân trang và việc trả secure code trong chi tiết là hợp đồng thiết kế cần chốt. |

## 4. Open issues & assumptions

Các điểm dưới đây cần Leader xác nhận trước khi hợp đồng triển khai được xem là cuối cùng:

1. **Số lượng địa chỉ tối đa:** đề xuất tối đa **10 địa chỉ/CUSTOMER**. Xác nhận cách xử lý khi vượt ngưỡng và khi xóa địa chỉ mặc định.
2. **Package Reuse Policy:** kiện đã gắn với đơn đã kết thúc có được tái sử dụng/gắn với đơn mới không? Baseline hiện tại áp dụng One Package One Order và không cho gán lại cho đến khi có quyết định.
3. **Cấu hình ngưỡng tải trọng drone:** giới hạn 5 kg/kích thước là hằng số trong ứng dụng hay cấu hình động theo drone/trạm? Cần thống nhất đơn vị lưu/trao đổi, hiện thực thể sử dụng gram.
4. **Bộ lọc mảng trạng thái:** API listing có chấp nhận nhiều trạng thái, ví dụ `status=PENDING,APPROVED`, hay chỉ một giá trị `status`? Chốt cả delimiter/encoding và phản hồi khi giá trị không hợp lệ.
5. **Giới hạn độ dài đề xuất:** xác nhận các ngưỡng độ dài trong bảng input (tên, điện thoại, địa chỉ, lý do hủy, từ khóa), vì SRS chưa định nghĩa các con số này.
6. **Trạng thái cho phép hủy:** xác nhận PENDING/APPROVED và quy tắc cho các trạng thái khác với Dispatcher/workflow; SRS chỉ nêu “while cancellation is still allowed”.
7. **Nguồn trạng thái trạm:** xác nhận giá trị enum chính thức tương ứng với `OPERATIONAL` và thời điểm kiểm tra điều kiện trạm hoạt động.
8. **Secure code trong chi tiết:** xác nhận CUSTOMER tạo đơn có được xem mã đầy đủ trong màn hình chi tiết hay chỉ người nhận được quyền xem; tài liệu hiện cho phép chủ đơn xem theo yêu cầu LA-216.
