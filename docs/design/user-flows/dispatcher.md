# Dispatcher User Flow - SmartDroneDelivery

Screen flow for the Dispatcher web portal. It covers reviewing delivery requests, scheduling deliveries, assigning drones, monitoring active deliveries and handling failures.

The flow uses the Dispatcher use cases in [use-cases.md](../../requirements/use-cases.md), the lifecycle rules in [business-processes.md](../business-processes.md) and the status values in [erd.md](../../architecture/erd.md).

## User flow

```mermaid
flowchart TB
    Start((Start)) --> Open["Open web portal"]
    Open --> Login["Log in"]
    Login --> Credentials{"Credentials valid?"}
    Credentials -->|no| Error["Show login error"]
    Error --> Login
    Credentials -->|yes| Dashboard["Dispatcher dashboard"]

    Dashboard --> Pending["Pending orders"]
    Pending --> Detail["Order detail<br/>customer, package, route and recipient"]
    Detail --> Decision{"Approve request?"}
    Decision -->|no| Reason["Enter rejection reason"]
    Reason --> Reject["Reject order<br/>status CANCELLED"]
    Reject --> NotifyReject["System logs the change<br/>and notifies the customer"]
    NotifyReject --> Pending

    Decision -->|yes| Approve["Approve order<br/>status APPROVED"]
    Approve --> Schedule["Set departure time"]
    Schedule --> Drones["View available drones"]
    Drones --> SelectDrone["Select drone"]
    SelectDrone --> Available{"Drone available for the time<br/>and suitable for the package?"}
    Available -->|no| Conflict["Show conflict or validation reason"]
    Conflict --> Change{"Change selection"}
    Change -->|time| Schedule
    Change -->|drone| Drones
    Available -->|yes| Assign["Assign drone"]
    Assign --> Mission["Create flight mission<br/>status PLANNED"]
    Mission --> Scheduled["Scheduled delivery detail"]
    Scheduled --> Dashboard

    Dashboard --> Active["Active deliveries"]
    Active --> Tracking["Live delivery detail<br/>status, position and ETA"]
    Tracking --> Outcome{"Current outcome"}
    Outcome -->|in progress| Refresh["Continue monitoring"]
    Refresh --> Tracking
    Outcome -->|delivered| Complete["View completed delivery"]
    Complete --> Dashboard
    Outcome -->|failed| Failed["Failed order detail"]
    Failed --> FailureReason["Record failure reason"]
    FailureReason --> Resolution{"Choose resolution"}
    Resolution -->|reschedule| Schedule
    Resolution -->|return| Return["Return package to origin"]
    Resolution -->|cancel| Cancel["Cancel order"]
    Return --> Close["System records outcome,<br/>releases resources and notifies customer"]
    Cancel --> Close
    Close --> Dashboard

    Dashboard --> Fleet["Drone fleet"]
    Fleet --> DroneDetail["View or maintain drone details<br/>model, payload, range and status"]
    DroneDetail --> Dashboard

    Dashboard --> Logout["Log out"]
    Logout --> End((End))
```

## Main screens and decisions

| Screen or decision | Purpose | Related use case |
|---|---|---|
| Dispatcher dashboard | Entry point for pending orders, active deliveries, failed deliveries and the drone fleet | UC-11 to UC-15 |
| Pending orders | List delivery requests with status `PENDING` | UC-11 |
| Order detail | Review customer, package, origin, destination and recipient information | UC-11 |
| Approve request? | Approve the request or reject it with a reason | UC-11 |
| Schedule and drone selection | Set departure time and select a suitable available drone | UC-12 |
| Active deliveries | Follow current mission status, position and ETA | UC-13, UC-20 |
| Failed order resolution | Reschedule, return the package or cancel the order | UC-14 |
| Drone fleet | Maintain model, payload capacity, range and operational status | UC-15 |

## Flow rules

- Approving an order changes it from `PENDING` to `APPROVED`; rejecting it records a reason and ends it as `CANCELLED`.
- A schedule is not confirmed when the selected drone is already booked for that time or is not suitable for the package.
- A successful assignment creates one `PLANNED` flight mission linked to the order.
- Active-delivery monitoring shows the current status, live position and AI-estimated arrival time.
- A failed delivery remains `FAILED` until the Dispatcher chooses to reschedule, return or cancel it.
- Every order decision and status change is recorded and the customer is notified where required by the delivery process.
