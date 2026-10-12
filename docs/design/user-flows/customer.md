# Customer User Flow - SmartDroneDelivery

Screen flow for the Customer mobile application. It covers account access and profile management, saved delivery addresses and packages, delivery order creation and tracking, cancellation, pickup-code confirmation, AI Assistant, and notifications.

The flow uses the Customer use cases in [use-cases.md](../../requirements/use-cases.md), the delivery lifecycle and failure rules in [business-processes.md](../business-processes.md), the customer, package, address, delivery order, notification and AI models in [erd.md](../../architecture/erd.md), and the Customer mobile wireframes documented for LA-76. Confirm any rule marked as unresolved against the current source documents and project leader before implementation.

## User flow

```mermaid
flowchart TB
    Start((Start)) --> Open["Open Customer mobile app"]
    Open --> Login["Log in"]
    Login --> Credentials{"Credentials valid?"}
    Credentials -->|no| LoginError["Show login error"]
    LoginError --> Login
    Credentials -->|yes| Home["Customer Home"]

    Login -->|create account| Register["Register"]
    Register --> RegisterValid{"Registration data valid?"}
    RegisterValid -->|no| RegisterError["Show validation error"]
    RegisterError --> Register
    RegisterValid -->|yes| Home

    %% Section 1: Orders and tracking
    Home --> Orders["My Orders"]
    Orders --> SelectOrder["Select an order"]
    SelectOrder --> OrderDetails["Order Details and tracking"]
    OrderDetails --> OrderStatus{"Current order status"}

    OrderStatus -->|PENDING| PendingActions["Edit or cancel order"]
    PendingActions --> EditOrder["Edit delivery request"]
    EditOrder --> CreateDelivery
    PendingActions --> CancelOrder["Cancel order"]
    CancelOrder --> CancelReason["Enter cancellation reason"]
    CancelReason --> ConfirmCancel{"Confirm cancellation?"}
    ConfirmCancel -->|no| OrderDetails
    ConfirmCancel -->|yes| CancelAllowed{"Cancellation allowed?"}
    CancelAllowed -->|no| CancelError["Show cancellation unavailable"]
    CancelError --> OrderDetails
    CancelAllowed -->|yes| Cancelled["Order status: CANCELLED"]
    Cancelled --> Orders

    OrderStatus -->|APPROVED| Approved["Show approved status and tracking"]
    Approved --> CancelOrder
    OrderStatus -->|READY_FOR_TAKEOFF| Ready["Show package at origin station"]
    OrderStatus -->|IN_TRANSIT| InTransit["Show route and live tracking"]
    OrderStatus -->|ARRIVED| Arrived["Show arrived status"]
    Arrived --> EnterCode["Enter pickup secure code"]
    EnterCode --> CodeValid{"Secure code valid?"}
    CodeValid -->|no| CodeError["Show invalid code and retry"]
    CodeError --> EnterCode
    CodeValid -->|yes| Delivered["Show delivery completed"]
    Delivered --> Orders
    OrderStatus -->|DELIVERED| Delivered
    OrderStatus -->|CANCELLED| CancelledDetails["Show cancellation reason"]
    OrderStatus -->|FAILED| Failed["Show failure reason and guidance"]

    OrderDetails --> History["View order history"]
    History --> OrderDetails
    OrderDetails --> Assistant["Ask AI Assistant"]
    Ready --> Assistant
    InTransit --> Assistant
    Failed --> Assistant

    %% Section 2: Create delivery
    Home --> CreateDelivery["Create Delivery"]
    CreateDelivery --> SelectPackage["Select saved package"]
    SelectPackage --> PackageAvailable{"Use existing package?"}
    PackageAvailable -->|no, add new| AddPackage["Add package"]
    AddPackage --> PackageValid{"Package data valid?"}
    PackageValid -->|no| PackageError["Show package validation error"]
    PackageError --> AddPackage
    PackageValid -->|yes| SavePackage["Save package"]
    SavePackage --> SelectStations
    PackageAvailable -->|yes| SelectStations["Select origin and destination stations"]
    SelectStations --> StationsValid{"Both stations operational<br/>and different?"}
    StationsValid -->|no| StationError["Show station selection error"]
    StationError --> SelectStations
    StationsValid -->|yes| Recipient["Enter recipient details<br/>or choose saved address"]
    Recipient --> RecipientValid{"Required details valid?"}
    RecipientValid -->|no| DeliveryError["Show delivery form errors"]
    DeliveryError --> Recipient
    RecipientValid -->|yes| Estimate["Show AI delivery estimate"]
    Estimate --> Review["Review delivery"]
    Review --> ReviewChoice{"Submit request?"}
    ReviewChoice -->|edit| CreateDelivery
    ReviewChoice -->|submit| SubmitOrder["Submit delivery request"]
    SubmitOrder --> Submitted["Show tracking number and pickup code<br/>order status: PENDING"]
    Submitted --> OrderDetails

    %% Section 3: Packages and addresses
    Home --> Packages["My Packages"]
    Packages --> PackageAction{"Choose action"}
    PackageAction -->|add| AddPackage
    PackageAction -->|edit| EditPackage["Edit package"]
    EditPackage --> PackageValid
    PackageAction -->|delete| DeletePackage["Delete package"]
    DeletePackage --> PackageInUse{"Attached to unfinished order?"}
    PackageInUse -->|yes| PackageDeleteError["Show package cannot be deleted"]
    PackageDeleteError --> Packages
    PackageInUse -->|no| PackageDeleted["Delete package"]
    PackageDeleted --> Packages

    Home --> Addresses["Delivery Addresses"]
    Addresses --> AddressAction{"Choose action"}
    AddressAction -->|add| AddAddress["Add address"]
    AddressAction -->|edit| EditAddress["Edit address"]
    AddressAction -->|set default| DefaultAddress["Set default address"]
    AddressAction -->|delete| DeleteAddress["Delete address"]
    AddAddress --> AddressValid{"Address data valid?"}
    EditAddress --> AddressValid
    AddressValid -->|no| AddressError["Show address validation error"]
    AddressError --> AddAddress
    AddressValid -->|yes| SaveAddress["Save address"]
    SaveAddress --> Addresses
    DefaultAddress --> Addresses
    DeleteAddress --> Addresses

    %% Section 4: Profile, notifications and assistant
    Home --> Profile["Profile and Settings"]
    Profile --> ProfileAction{"Choose action"}
    ProfileAction -->|edit profile| EditProfile["Edit profile"]
    EditProfile --> ProfileValid{"Profile data valid?"}
    ProfileValid -->|no| ProfileError["Show validation error"]
    ProfileError --> EditProfile
    ProfileValid -->|yes| SaveProfile["Save profile"]
    SaveProfile --> Profile
    ProfileAction -->|change password| ChangePassword["Change password"]
    ChangePassword --> PasswordValid{"Current and new password valid?"}
    PasswordValid -->|no| PasswordError["Show password error"]
    PasswordError --> ChangePassword
    PasswordValid -->|yes| PasswordUpdated["Update password and revoke old refresh tokens"]
    PasswordUpdated --> Login
    ProfileAction -->|theme| Theme["Choose Light, Dark or System"]
    Theme --> Profile
    ProfileAction -->|addresses| Addresses
    ProfileAction -->|assistant| Assistant
    ProfileAction -->|notifications| Notifications["Notifications"]
    ProfileAction -->|log out| Logout["Log out"]
    Logout --> End((End))

    Home --> Notifications
    Notifications --> NotificationAction{"Choose notification"}
    NotificationAction -->|open order update| OrderDetails
    NotificationAction -->|mark as read| Notifications
    NotificationAction -->|back| Home

    Assistant --> Chat["Send question or choose suggestion"]
    Chat --> OwnOrder{"Question concerns own order?"}
    OwnOrder -->|no| AssistantDenied["Decline access to another customer's order"]
    AssistantDenied --> Assistant
    OwnOrder -->|yes or general help| AssistantReply["Show assistant response"]
    AssistantReply --> Assistant
```

## Main screens and decisions

| Screen or decision | Purpose | Related use case |
|---|---|---|
| Customer Login | Authenticate the Customer and provide an entry to registration | Customer authentication use case |
| Customer Registration | Create a Customer account and validate required fields | Customer registration use case |
| Customer Home | Central entry point for delivery summary, orders, packages, addresses, notifications and profile | Customer home/dashboard use case |
| My Orders | List the signed-in Customer's orders; search by tracking number and filter by status | UC-01 to UC-10 mapping to be verified against `use-cases.md` |
| Order Details and tracking | Show current order status, route, tracking timeline and status-dependent actions | Order tracking use case |
| Order History | Show order status changes, timestamps, actor and notes | Order history use case |
| Create Delivery | Select a package, origin/destination stations and recipient details | Create delivery use case |
| AI delivery estimate | Display the estimate described by the source, such as distance, flight duration and weather | AI delivery estimate use case |
| Review Delivery | Let the Customer review details and return to edit before submission | Delivery review/submission use case |
| Order Submitted | Show the tracking number, pickup secure code and pending status | Create delivery use case |
| Cancel Order | Collect a cancellation reason and confirm cancellation when the current status allows it | Cancel delivery use case |
| My Packages | List and manage saved packages | Package management use case |
| Add/Edit Package | Capture package name, weight, dimensions, description, fragile flag and optional image | Package management use case |
| Delivery Addresses | List and manage saved addresses; choose a default address where supported | Address management use case |
| Add/Edit Address | Capture label, recipient name, phone and address | Address management use case |
| Confirm Delivery | Allow the Customer to enter the pickup secure code after the order reaches `ARRIVED` | Delivery confirmation use case |
| Invalid Secure Code | Explain failed verification and allow retry | Delivery confirmation use case |
| Notifications | Show order and delivery updates and support read-state actions | Notification use case |
| AI Assistant | Answer questions about the Customer's own order or provide general delivery support | AI Assistant use case |
| Profile and Settings | Provide access to profile editing, password, theme, addresses, notifications, assistant and logout | Profile management use case |
| Edit Profile | Update editable profile details; email remains read-only if required by the source | Profile management use case |
| Change Password | Validate current/new password and end old sessions as specified by the source | Password management use case |
| Order status decision | Determine available actions from the current delivery-order status | Order lifecycle rules |

> **Use-case IDs:** The exact UC IDs for each Customer row must be checked against `../../requirements/use-cases.md`. Do not infer that the Station Operator UC numbers apply to Customer flows.

## Flow rules

- **Authentication**
  - Valid credentials lead to the Customer Home screen; invalid credentials return an error to Login.
  - Registration must validate the required fields before creating the account.
  - Signing out returns the user to Login. Session/token behavior must follow the authentication API and project security rules.

- **Delivery creation**
  - A Customer selects a saved package or adds a new one, selects origin and destination stations, and enters recipient details before reviewing the request.
  - The origin and destination stations must be different.
  - The supplied source flow says station selection should list stations with status `OPERATIONAL`; verify this against the current requirements/API.
  - The submitted order starts in `PENDING` according to the supplied source flow.
  - The review screen must allow the Customer to return and edit the request before submission.

- **Order lifecycle and cancellation**
  - The Customer can view only their own orders.
  - The supplied source allows editing while the order is `PENDING` and cancellation while it is `PENDING` or `APPROVED`.
  - From `READY_FOR_TAKEOFF` onward, the source says the Customer cannot cancel directly and should contact the dispatcher.
  - Available details/actions must reflect the order's current status: `PENDING`, `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED` or `FAILED`.

- **Tracking and history**
  - Order Details displays the status, route and relevant timeline milestones.
  - The supplied source specifies live drone location and telemetry through SignalR for the applicable in-transit state; confirm implementation and connection behavior.
  - Order History displays status changes and associated timestamps/details.

- **Pickup secure-code confirmation**
  - The code-entry screen is available when the order is `ARRIVED`.
  - A failed code attempt shows an error and permits retry, subject to the final security policy.
  - A valid code follows the supplied Customer flow to delivery completion (`DELIVERED`).
  - **Open question:** confirm code length, failed-attempt limit, lockout duration and alert behavior. The supplied source materials describe inconsistent proposals for these values.

- **Package management**
  - Package records belong to the signed-in Customer.
  - Validate positive weight and dimensions and apply the source's stated package weight limit only after confirming it remains an approved requirement.
  - The source describes weight entry in kg and storage in grams (`weight_gram`).
  - A package attached to an unfinished order must not be deleted, according to the supplied flow.

- **Address management**
  - Saved address data can prefill recipient details during delivery creation.
  - Default-address, nearest-station and delete behavior must match the current API/ERD and approved requirements.

- **Notifications**
  - Notifications cover relevant order and delivery events described by the source.
  - Selecting an order notification opens that order's details.
  - Read/unread behavior must match the notification API.

- **AI Assistant**
  - The assistant may answer general delivery questions and questions about the signed-in Customer's own order.
  - It must not disclose another Customer's order information.

- **Profile and settings**
  - Profile editing validates fields before saving.
  - The source says email is read-only and changing a password revokes old refresh tokens; verify these behaviors against the authentication implementation.
  - Theme options are Light, Dark and System if supported by the app.

- **Scope**
  - This document describes the Customer mobile application only. Station Operator flows are documented separately.
  - The Customer app is for delivery management and tracking, not drone flight control.
