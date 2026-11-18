# Staff delivery lookup (LA-355)

`GET /api/delivery-insights/orders` lets an active Dispatcher or Logistics Manager find current and historical deliveries before opening their existing ETA/summary routes. It does not expose the customer-only order list to staff. It does not approve, schedule or otherwise change a delivery.

Authentication: bearer JWT. Both its role and the persisted active user/role are checked. Customer, Station Operator and Administrator are denied. Responses are `Cache-Control: no-store`.

## Query

| Parameter | Default | Validation / meaning |
|---|---|---|
| `searchKeyword` | No filter | At most 50 characters before trimming; case-insensitive literal substring of tracking number. Blank means no filter. |
| `status` | All statuses | Optional, trimmed and case-insensitive: `PENDING`, `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED`. Blank means no filter. |
| `fromDate` | No lower bound | Optional creation calendar day, strictly `YYYY-MM-DD` in Asia/Ho_Chi_Minh (UTC+07). Inclusive. |
| `toDate` | No upper bound | Optional creation calendar day in the same format/timezone. Inclusive; implemented as an exclusive start of the following local day. Must not precede `fromDate`. |
| `page` | 1 | Integer 1 through 2147483647. A page beyond results returns empty items, retaining the filtered count. |
| `pageSize` | 20 | Integer 1 through 100. |

Unknown, incorrectly cased or repeated query parameters are rejected. Invalid values, non-calendar timestamps, impossible dates and boundaries outside .NET's supported UTC range return HTTP400 with field errors. `fromDate=0001-01-01` and `toDate=9999-12-31` cannot be represented by these timezone boundaries and are rejected.

Example: `/api/delivery-insights/orders?searchKeyword=ORD-DEMO&status=delivered&fromDate=2026-11-01&toDate=2026-11-18&page=1&pageSize=20`.

## Successful response

HTTP200 uses the existing `PagedResult` envelope. This synthetic example illustrates the schema, not a new production record:

```json
{
  "items": [{
    "id": "00000000-0000-0000-0000-000000000001",
    "trackingNumber": "ORD-DEMO-003",
    "status": "DELIVERED",
    "originStationName": "Origin hub",
    "destinationStationName": "Destination hub",
    "createdAt": "2026-11-17T17:00:00Z"
  }],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Items are sorted by `createdAt` descending, then `id` ascending for stable ties. Historical completed/failed/cancelled deliveries are included unless filtered out. No match is HTTP200 with `items: []`, `totalCount: 0`, `totalPages: 0`.

Only the six item fields above are returned. Recipient/contact details, customer identifiers, package data, addresses, pickup codes and chat history are excluded. This is a selection list, not an additional order-detail capability. Use `id` for existing `/delivery-eta/:id` and `/delivery-summary/:id` web routes and the existing insight APIs; those routes retain their own authorization and enabled/model checks. A matching lookup result does not imply that model generation is enabled.

HTTP401 means invalid/missing identity; HTTP403 means insufficient current role or inactive account. HTTP400 uses the standard API error envelope with `errors[parameter]`. Network/server failures must remain distinct from empty search results. Clients must refresh lookup results when operational state changes; separate count/item queries do not guarantee a frozen snapshot during concurrent updates.

Machine-readable contract: [delivery-insight-lookup.openapi.json](delivery-insight-lookup.openapi.json). Runtime generated Swagger includes the route and response DTO; the companion document specifies its strictly parsed query parameters.

Verification: `DeliveryInsightLookupTests` covers projection, active/historical selection, role revocation, query validation, timezone bounds and safe pagination; `DeliveryInsightLookupPostgresTests` verifies real PostgreSQL query translation. Web integration belongs to LA-356; this API task does not establish completion of parent LA-350 or actual native/mobile acceptance.
