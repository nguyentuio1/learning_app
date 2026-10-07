# Business Process Models - SmartDroneDelivery

Models of the drone delivery business processes: the delivery lifecycle (LA-70) and the cancellation and failure flows (LA-71). They use the actors of [use-cases.md](../requirements/use-cases.md) and the status values of [erd.md](../architecture/erd.md).

The diagrams are drawn in Mermaid (rendered by GitHub) because the tool has no native BPMN. Swimlanes show who performs each step.

> **Proposed rules.** The status values come from the ERD. The transition and cancellation rules below are proposed by this document and must be reviewed by the team. Open points are listed at the end.

## 1. Order status model

`delivery_orders.status` takes one of eight values.

```mermaid
stateDiagram-v2
    [*] --> PENDING : Customer submits order
    PENDING --> APPROVED : Dispatcher approves
    PENDING --> CANCELLED : Customer cancels or Dispatcher rejects
    APPROVED --> READY_FOR_TAKEOFF : Delivery scheduled, drone assigned, package received at station
    APPROVED --> CANCELLED : Customer or Dispatcher cancels
    READY_FOR_TAKEOFF --> IN_TRANSIT : Drone takes off
    READY_FOR_TAKEOFF --> CANCELLED : Dispatcher cancels
    IN_TRANSIT --> ARRIVED : Drone lands at destination station
    IN_TRANSIT --> FAILED : Mission aborted
    ARRIVED --> DELIVERED : Recipient confirms with secure code
    ARRIVED --> FAILED : Package not collected in time
    FAILED --> APPROVED : Dispatcher reschedules
    FAILED --> CANCELLED : Dispatcher cancels or returns package
    DELIVERED --> [*]
    CANCELLED --> [*]
```

| Status | Meaning | Who can move the order out |
|---|---|---|
| PENDING | Created by the customer, waiting for review | Dispatcher, Customer (cancel) |
| APPROVED | Accepted, waiting for schedule and drone | Dispatcher, Station Operator, Customer (cancel) |
| READY_FOR_TAKEOFF | Drone assigned and package loaded | System (takeoff), Dispatcher (cancel) |
| IN_TRANSIT | Drone is flying | System |
| ARRIVED | Drone landed at the destination station | Recipient, System (timeout) |
| DELIVERED | Recipient confirmed receipt (final) | none |
| CANCELLED | Stopped before completion (final) | none |
| FAILED | A delivery attempt failed, waiting for a decision | Dispatcher |

## 2. Delivery lifecycle (LA-70)

The normal flow from the customer's request to the confirmation of delivery.

```mermaid
flowchart TB
    Start((Start)) --> A1

    subgraph Customer["Customer"]
        A1["Select package, origin and destination stations,<br/>enter recipient details"]
        A2["Submit order"]
        A1 --> A2
    end

    A2 --> S1

    subgraph System["System"]
        S1["Validate weight and size against drone limits"]
        S2["Create order: status PENDING,<br/>tracking number, secure code"]
        S3["Notify dispatchers"]
        S1 -->|valid| S2 --> S3
        S1 -->|invalid| SX["Show reason, customer corrects input"]
    end

    S3 --> D1

    subgraph Dispatcher["Dispatcher"]
        D1{"Approve request?"}
        D2["Set status APPROVED"]
        D3["Set departure time and assign an available drone"]
        D4["Create flight mission PLANNED, reserve slots"]
        D1 -->|yes| D2 --> D3 --> D4
        D1 -->|no| DR["Reject with reason, status CANCELLED"]
    end

    D4 --> O1

    subgraph Operator["Station Operator at the origin station"]
        O1["Receive package from the customer"]
        O2["Confirm package pickup in the app"]
        O1 --> O2
    end

    O2 --> T1

    subgraph Flight["System and drone"]
        T1["Status READY_FOR_TAKEOFF, drone ASSIGNED"]
        T2["Takeoff: status IN_TRANSIT, mission IN_AIR, drone FLYING"]
        T3["Send telemetry, push live position and ETA"]
        T4{"Landed at destination?"}
        T5["Status ARRIVED, mission COMPLETED, slot OCCUPIED"]
        T1 --> T2 --> T3 --> T4
        T4 -->|yes| T5
        T4 -->|mission aborted| TF["Go to failure flow, status FAILED"]
    end

    T5 --> R1

    subgraph Recipient["Recipient"]
        R1["Receive notification and go to the destination station"]
        R2["Enter the pickup secure code"]
        R3{"Code correct?"}
        R1 --> R2 --> R3
        R3 -->|no| R2
    end

    R3 -->|yes| E1

    subgraph Finish["System"]
        E1["Status DELIVERED, record actual delivery time"]
        E2["Release slot, drone back to IDLE or CHARGING"]
        E3["Notify customer, write status log"]
        E1 --> E2 --> E3
    end

    E3 --> End((End))
```

### Effects of each step

| Step | Order | Mission | Drone | Slot | Notification and log |
|---|---|---|---|---|---|
| Order submitted | PENDING | none | none | none | Log entry, notify dispatchers |
| Approved | APPROVED | none | none | none | Log entry, notify customer |
| Scheduled and drone assigned | APPROVED | PLANNED | ASSIGNED | destination slot RESERVED | Log entry |
| Pickup confirmed | READY_FOR_TAKEOFF | PLANNED | ASSIGNED | origin slot OCCUPIED | Log entry, notify customer |
| Takeoff | IN_TRANSIT | IN_AIR | FLYING | origin slot EMPTY | Log entry, live tracking starts |
| Landing | ARRIVED | COMPLETED | CHARGING or IDLE | destination slot OCCUPIED | Log entry, notify recipient |
| Delivery confirmed | DELIVERED | COMPLETED | IDLE | destination slot EMPTY | Log entry, notify customer |

## 3. Cancellation flow (LA-71)

```mermaid
flowchart TB
    Start((Start)) --> Q1{"Current order status"}

    subgraph Customer["Customer"]
        C1["Request cancellation with a reason"]
    end

    subgraph Dispatcher["Dispatcher"]
        D1["Cancel the order with a reason"]
    end

    subgraph System["System"]
        S1{"Status PENDING or APPROVED?"}
        S2["Set status CANCELLED, store cancel reason"]
        S3["Release reserved slot and drone if assigned,<br/>mission ABORTED"]
        S4["Write status log and notify the customer"]
        S5["Reject the request: order can no longer be cancelled by the customer"]
        S1 -->|yes| S2
        S1 -->|no| S5
        S2 --> S3 --> S4
    end

    Q1 -->|Customer acts| C1 --> S1
    Q1 -->|Dispatcher acts| D1 --> S2
    S4 --> End((End))
    S5 --> End
```

| Status when cancelled | Customer | Dispatcher | Result |
|---|---|---|---|
| PENDING | allowed | allowed (reject) | CANCELLED |
| APPROVED | allowed | allowed | CANCELLED, planned mission aborted |
| READY_FOR_TAKEOFF | not allowed | allowed | CANCELLED, package returned to the customer at the station |
| IN_TRANSIT, ARRIVED | not allowed | not allowed | handled as a failure when needed |
| DELIVERED, CANCELLED | not allowed | not allowed | final |

## 4. Failed delivery flow (LA-71)

A delivery fails when the mission is aborted in the air (weather, low battery, technical fault) or when the package is not collected after arrival within the allowed time.

```mermaid
flowchart TB
    Start((Start)) --> F1

    subgraph System["System"]
        F1{"What happened?"}
        F2["Mission ABORTED, drone returns to nearest station"]
        F3["Collection time expired"]
        F4["Set status FAILED, write status log"]
        F5["Notify customer and dispatcher"]
        F1 -->|mission aborted| F2 --> F4
        F1 -->|package not collected| F3 --> F4
        F4 --> F5
    end

    F5 --> D1

    subgraph Dispatcher["Dispatcher"]
        D1["Open the failed order and record the reason"]
        D2{"Choose a resolution"}
        D3["Reschedule: status APPROVED,<br/>choose new time and drone"]
        D4["Return package to origin station"]
        D5["Cancel the order"]
        D1 --> D2
        D2 -->|reschedule| D3
        D2 -->|return| D4
        D2 -->|cancel| D5
    end

    D3 --> Next["Continue with the delivery lifecycle<br/>from scheduling"]
    D4 --> Op1

    subgraph Operator["Station Operator"]
        Op1["Receive the returned package and confirm"]
    end

    Op1 --> X1
    D5 --> X1

    subgraph Close["System"]
        X1["Status CANCELLED with the failure reason"]
        X2["Release slot and drone, notify customer"]
        X1 --> X2
    end

    Next --> End((End))
    X2 --> End
```

## 5. Link to use cases and requirements

| Process | Use cases | Requirements |
|---|---|---|
| Delivery lifecycle | UC-05, UC-11, UC-12, UC-18, UC-07, UC-08 | FR-08, FR-09, FR-13 to FR-15, FR-23, FR-24, FR-26, FR-27 |
| Cancellation | UC-06, UC-11 | FR-10, FR-14 |
| Failed delivery | UC-14 | FR-18 |

## 6. Open points for team review

1. **Return to origin.** The status list has no RETURNED value, so a returned package ends as CANCELLED with the reason recorded. Add a status only if the team needs to report returns separately.
2. **Cancellation window.** The customer may cancel in PENDING and APPROVED. Confirm that cancellation after READY_FOR_TAKEOFF is dispatcher-only.
3. **Collection timeout.** The time allowed before an ARRIVED order becomes FAILED is not defined yet.
4. **Slot reservation point.** The diagram reserves the destination slot when the drone is assigned. Confirm whether the origin slot should be reserved too.
5. **Automatic transitions.** IN_TRANSIT and ARRIVED are driven by telemetry. The rule that decides "landed" must be defined with the drone data source.
