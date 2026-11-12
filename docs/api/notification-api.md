# API Contract - Customer Notifications

Documentation task: [LA-339 - Sprint 5 API contracts](https://hungndemail.atlassian.net/browse/LA-339). The original notification contract was added in commit `0c9fddc` (PR #139).

Contract for LA-326 (customer notifications: list, unread count, mark as read; requirement FR-12, use case UC-10, wireframe C22). Backend, web and mobile build against this document.

> **Status: proposed, to be confirmed by the team.** Any change goes through a pull request that edits this file. Open points are in section 5.

The backend already writes `notifications` rows on key order transitions (BR-DW-21). This contract only adds the read and mark-as-read API on top of the existing `Notification` entity (`id`, `userId`, `orderId`, `type`, `title`, `message`, `isRead`, `createdAt`, `readAt`).

## 1. Conventions

Everything in section 1 of [auth-api.md](auth-api.md) and [customer-order-api.md](customer-order-api.md) applies (base URL, `/api` prefix, camelCase JSON, UUIDs, UTC ISO 8601, bearer token, RFC 7807 errors, `PagedResult`). Additions:

| Item | Rule |
|---|---|
| Role | `CUSTOMER`. Another role, or an account that is not `ACTIVE`, gets `403`. |
| Ownership | The user id always comes from the token (`sub`). A notification of another user, or one that does not exist, returns `404` (BR-CO-36). |
| Content | A notification never contains data of another user. The pickup code appears only in the creation notice of the customer's own order (open point 2). |

`Notification`
```json
{
  "id": "f6a8c0e2-5b7d-4d9f-8f3a-4c6e8a0b2d35",
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "type": "ORDER_STATUS_CHANGED",
  "title": "Order Approved",
  "message": "Your delivery order DD-202611-0012 was approved.",
  "isRead": false,
  "createdAt": "2026-11-12T03:15:00Z",
  "readAt": null
}
```
`orderId` is `null` for a notification that is not about an order. The app opens the order detail from `orderId`.

## 2. Endpoints

### 2.1 GET /api/notifications
Query: `isRead` (optional boolean; `false` gives unread only), `page` (default 1), `pageSize` (default 20, 1 to 100). Response `200 OK`: `PagedResult` of `Notification`, newest first. An empty list is `200` with `"items": []`. Errors: `400` bad `isRead`, `page` or `pageSize`; `403`.

### 2.2 GET /api/notifications/unread-count
Response `200 OK`: `{ "unreadCount": 3 }`. Used for the badge on web and mobile. Errors: `403`.

### 2.3 PUT /api/notifications/{id}/read
No body. Sets `isRead = true` and `readAt` the first time; repeating the call is `200` and changes nothing (idempotent). Response `200 OK`: the `Notification`. Errors: `403`; `404`.

### 2.4 PUT /api/notifications/read-all
No body. Marks every unread notification of the caller as read. Response `200 OK`: `{ "updatedCount": 3 }`. Errors: `403`.

## 3. Delivery channel

Proposed for Sprint 5: **in-app only**. The apps poll `unread-count` when they come to the foreground and every 60 seconds while open, and reload the list when the screen opens. A push channel (SignalR for web, push notification for mobile) is not part of this contract. When it is added, the stored row stays the source and a failed push never undoes a status change. See open point 1.

## 4. Event types

`type` is `ORDER_STATUS_CHANGED` for every transition in [delivery-workflow-api.md](delivery-workflow-api.md) section 4 (`APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED`) and for order creation. Clients show `title` and `message` as stored and must accept new `type` values without failing.

## 5. Open points

1. **Push channel.** In-app polling is proposed for Sprint 5. SignalR or push can follow with the tracking work of Sprint 6. To be confirmed.
2. **Pickup code in the creation notice.** [customer-order-api.md](customer-order-api.md) section 8 point 5 decided that the creation notice carries the code to the customer only, and noted that the implementation does not do this yet. This API returns `message` unchanged, so that gap is closed on the write side, not here.
3. **Retention.** No delete endpoint is defined. Proposed: keep notifications; a cleanup rule can be added later.
