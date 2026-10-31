# API Contract - Customer Address, Package & Delivery Order Management

Contract for Customer Address, Package, and Delivery Order management in SmartDroneDelivery (UC-03, UC-04, UC-05, UC-06, UC-07, FR-11, FR-12).

## 1. Customer Addresses (`/api/customer-addresses`)

### 1.1 List Addresses

- **Endpoint**: `GET /api/customer-addresses`
- **Auth**: Bearer token (`CUSTOMER`)
- **Response**: `200 OK`

```json
[
  {
    "id": "11111111-1111-1111-1111-111111111111",
    "userId": "22222222-2222-2222-2222-222222222222",
    "label": "Home",
    "contactName": "Alice Smith",
    "contactPhone": "0912345678",
    "addressLine": "10 Main St, District 1",
    "latitude": 10.7769,
    "longitude": 106.7009,
    "isDefault": true,
    "createdAt": "2026-10-30T02:00:00Z",
    "updatedAt": "2026-10-30T02:00:00Z"
  }
]
```

### 1.2 Get Address by ID

- **Endpoint**: `GET /api/customer-addresses/{id}`
- **Auth**: Bearer token (`CUSTOMER`)
- **Response**: `200 OK` (404 if not found, 403 if address belongs to another customer)

### 1.3 Create Address

- **Endpoint**: `POST /api/customer-addresses`
- **Auth**: Bearer token (`CUSTOMER`)
- **Body**:

```json
{
  "label": "Office",
  "contactName": "Alice Work",
  "contactPhone": "0912345679",
  "addressLine": "20 Work St, District 3",
  "latitude": 10.78,
  "longitude": 106.69,
  "isDefault": true
}
```

- **Response**: `201 Created`

### 1.4 Update Address

- **Endpoint**: `PUT /api/customer-addresses/{id}`
- **Auth**: Bearer token (`CUSTOMER`)
- **Response**: `200 OK`

### 1.5 Delete Address

- **Endpoint**: `DELETE /api/customer-addresses/{id}`
- **Auth**: Bearer token (`CUSTOMER`)
- **Response**: `204 NoContent`

---

## 2. Package Management (`/api/packages`)

### 2.1 List Packages

- **Endpoint**: `GET /api/packages`
- **Auth**: Bearer token (`CUSTOMER`)
- **Response**: `200 OK`

### 2.2 Create Package

- **Endpoint**: `POST /api/packages`
- **Auth**: Bearer token (`CUSTOMER`)
- **Body**:

```json
{
  "packageName": "Medical Supplies",
  "weightGram": 1200.0,
  "lengthCm": 25.0,
  "widthCm": 15.0,
  "heightCm": 10.0,
  "isFragile": true,
  "packageImageUrl": null
}
```

- **Rules**:
  - `weightGram`: 1g - 5000g (max drone payload constraint)
  - `lengthCm`, `widthCm`, `heightCm`: 1cm - 100cm
- **Response**: `201 Created`

### 2.3 Update Package

- **Endpoint**: `PUT /api/packages/{id}`
- **Auth**: Bearer token (`CUSTOMER`)
- **Response**: `200 OK` (409 Conflict if linked to an active in-flight order)

### 2.4 Upload Package Image

- **Endpoint**: `POST /api/packages/{id}/image`
- **Auth**: Bearer token (`CUSTOMER`)
- **Content-Type**: `multipart/form-data`
- **Max Size**: 5MB (JPEG, PNG, WebP)
- **Response**: `200 OK`

```json
{
  "imageUrl": "http://localhost:9000/packages/11111111-1111-1111-1111-111111111111.jpg"
}
```

---

## 3. Delivery Orders (`/api/delivery-orders`)

### 3.1 Create Order

- **Endpoint**: `POST /api/delivery-orders`
- **Auth**: Bearer token (`CUSTOMER`)
- **Body**:

```json
{
  "packageId": "11111111-1111-1111-1111-111111111111",
  "originStationId": "33333333-3333-3333-3333-333333333333",
  "destinationStationId": "44444444-4444-4444-4444-444444444444",
  "recipientName": "John Doe",
  "recipientPhone": "0987654321",
  "deliveryAddressId": null
}
```

- **Business Rules**:
  - Customer must own `packageId`.
  - Package cannot already be assigned to another active order.
  - `originStationId` and `destinationStationId` must be different and `OPERATIONAL`.
  - Generates unique `trackingNumber` (e.g. `DD-20261030-A1B2C3`).
  - Generates 6-digit `pickupSecureCode`.
  - Sets initial status `PENDING` and records in `delivery_status_logs`.
  - Generates a `Notification` for the customer (FR-12).
- **Response**: `201 Created`

### 3.2 Update Order

- **Endpoint**: `PUT /api/delivery-orders/{id}`
- **Auth**: Bearer token (`CUSTOMER`)
- **Rule**: Only permitted when `status == "PENDING"`.
- **Response**: `200 OK` (409 Conflict if status != PENDING)

### 3.3 Cancel Order

- **Endpoint**: `POST /api/delivery-orders/{id}/cancel`
- **Auth**: Bearer token (`CUSTOMER`)
- **Body**:

```json
{
  "reason": "Customer requested cancellation before takeoff"
}
```

- **Rule**: Allowed in `PENDING` or `APPROVED` states. Cannot cancel once `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, or already `CANCELLED`.
- **Side Effect**: Generates a cancellation `Notification` for the customer (FR-12).
- **Response**: `200 OK`

### 3.4 List Orders (Filtered & Paginated)

- **Endpoint**: `GET /api/delivery-orders?status=PENDING&trackingNumber=DD-&fromDate=2026-10-01T00:00:00Z&toDate=2026-10-31T23:59:59Z&page=1&pageSize=20`
- **Auth**: Bearer token (Customer only sees their own orders; Staff sees all)
- **Query Parameters**:
  - `status`: String (PENDING, APPROVED, READY_FOR_TAKEOFF, IN_TRANSIT, ARRIVED, DELIVERED, CANCELLED, FAILED)
  - `trackingNumber`: Case-insensitive partial search
  - `fromDate`: ISO 8601 UTC date filter
  - `toDate`: ISO 8601 UTC date filter
  - `page`: Page index (default: 1)
  - `pageSize`: Page size (default: 20, max: 100)
- **Response**: `200 OK`

```json
{
  "items": [...],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3,
  "hasPrevious": false,
  "hasNext": true
}
```

### 3.5 Order Details & Status History

- **Detail**: `GET /api/delivery-orders/{id}` (Auth: Bearer)
- **History**: `GET /api/delivery-orders/{id}/status-history` (Auth: Bearer)
