# SmartDroneDelivery - Activity Diagrams for Supplementary Use Cases
**Sprint:** Sprint 1-2  
**Ticket Ref:** Complement to [LA-12: Model the drone delivery business processes with BPMN](https://hungndemail.atlassian.net/browse/LA-12#icft=LA-12)  
**Suggested Owner:** Khai  
**Document Status:** Approved / Ready for Review  
**Project:** AI-powered Drone Delivery Management Platform (Nền tảng quản lý giao hàng bằng drone tích hợp AI)

---

## 1. Overview & Context

Ticket **LA-12** modeled the primary business processes of the **Drone Delivery Lifecycle** using BPMN:
1. Delivery Request Management
2. Package Management & Inspection
3. Delivery Execution (Scheduling, Route Planning & Dispatch)
4. Delivery Flight Tracking (Telemetry & Geofence Monitoring)
5. Delivery Confirmation & Failed Delivery Handling

To deliver a complete system specification for **Sprint 1-2**, this document defines the **supplementary business and operational use cases** outside the delivery flight lifecycle. Each use case includes:
- Formal specification (Actors, Preconditions, Postconditions, Triggers, Normal Flow, and Alternate/Exception Flows).
- Standard UML Activity Diagram with **explicit swimlanes per actor** (Customer, Dispatcher, Station Operator, Logistics Manager, System Administrator, and AI/System Services).

---

## 2. Actor Catalog

| Actor | Type | Description |
| :--- | :--- | :--- |
| **Customer** | Human | End user who places delivery orders, manages addresses, chats with AI Assistant, and picks up packages at landing stations. |
| **Dispatcher** | Human | Operational controller responsible for approving exceptions, reassigning drones, reviewing cancellations, and handling escalated inquiries. |
| **Station Operator** | Human | Field personnel stationed at drone landing hubs, managing station readiness, pad maintenance, physical loading/unloading, and package pickup verification. |
| **Logistics Manager** | Human | Management persona responsible for station configuration, service territory policies, KPI monitoring, and operational reporting. |
| **System Administrator** | Human | Technical administrator managing user accounts, role-based access control (RBAC), security audits, and system configuration. |
| **SmartDroneDelivery System** | Automated System | Core backend platform (ASP.NET Core Web API, PostgreSQL, SignalR, MinIO). |
| **AI Services Engine** | Automated System | Integrated AI modules for NLP Customer Assistance, ETA calculations, operational anomaly detection, and automated summarization. |

---

## 3. Supplementary Use Case Specifications & Activity Diagrams

---

### UC-OTH-01: User Authentication & Role-Based Access Control (RBAC)
- **Primary Actor:** Any User (Customer, Dispatcher, Station Operator, Logistics Manager, System Administrator)
- **Supporting Systems:** SmartDroneDelivery System (Auth Service), Identity/Token Service
- **Preconditions:** User has an existing active account in the system.
- **Postconditions:** User is authenticated, issued a signed JWT with role claims, and redirected to their role-specific dashboard.
- **Trigger:** User accesses the application and submits their login credentials.

#### Detailed Flow of Events:
1. **User** enters username/email and password on the login portal.
2. **System** receives credentials and performs rate-limiting & format validation.
3. **System** validates credentials against PostgreSQL database (BCrypt hash check).
4. *Alternative Flow A1 (Invalid Credentials):* If invalid, System logs the failed attempt, increments failure counter, and returns an error message. If attempts exceed threshold, account is temporarily locked.
5. **System** checks account status (Active, Suspended, or Pending Verification).
6. *Alternative Flow A2 (Inactive Account):* System notifies user of account restriction and terminates session.
7. **System** extracts user role claims (Customer, Dispatcher, Station Operator, Logistics Manager, System Admin) and generates a signed JWT Access Token and Refresh Token.
8. **System** sets up SignalR connection context for real-time notifications.
9. **User** receives token, browser/mobile app stores token securely, and user navigates to their authorized portal interface.

#### Activity Diagram: UC-OTH-01 (Authentication & RBAC)

```mermaid
flowchart TD
    subgraph UserLane["User (Any Actor)"]
        U_Start([Start]) --> U_Input["Enter Username & Password"]
        U_Input --> U_Submit["Submit Login Form"]
        U_HandleError["View Error Message / Retry"]
        U_Dashboard["Access Authorized Dashboard View"] --> U_End([End])
    end

    subgraph SystemLane["SmartDroneDelivery System (Auth API)"]
        U_Submit --> S_RateCheck{"Rate Limit Exceeded?"}
        S_RateCheck -->|Yes| S_Block["Return 429 Too Many Requests"]
        S_RateCheck -->|No| S_VerifyCreds["Verify Password Hash in Database"]
        
        S_VerifyCreds --> S_CredsValid{"Credentials Valid?"}
        S_CredsValid -->|No| S_RecordFail["Record Failed Attempt & Check Threshold"]
        S_RecordFail --> S_ErrResp["Return 401 Unauthorized Response"]
        S_ErrResp --> U_HandleError
        S_Block --> U_HandleError

        S_CredsValid -->|Yes| S_CheckStatus{"Account Status Active?"}
        S_CheckStatus -->|No| S_StatusErr["Return 403 Account Inactive / Locked"]
        S_StatusErr --> U_HandleError

        S_CheckStatus -->|Yes| S_GenToken["Generate JWT Token with Role Claims & Refresh Token"]
        S_GenToken --> S_AuditLog["Log Successful Login in Audit Table"]
        S_AuditLog --> S_ReturnSession["Return JWT & Role-Specific Redirect Info"]
        S_ReturnSession --> U_Dashboard
    end
```

---

### UC-OTH-02: Manage Users and Role Assignments
- **Primary Actor:** System Administrator
- **Supporting Systems:** SmartDroneDelivery System, Notification Service (Email/SMS)
- **Preconditions:** System Administrator is logged in with Admin privileges.
- **Postconditions:** New user is created, existing user profile/role is updated or deactivated, and audit logs are recorded.
- **Trigger:** System Administrator navigates to User Management console to onboard staff or adjust permissions.

#### Detailed Flow of Events:
1. **System Administrator** requests the user directory list with optional role/status filters.
2. **System** queries user accounts and renders directory with assigned roles and operational statuses.
3. **System Administrator** selects an action: Create New User, Modify Role, or Change Account Status (Activate/Suspend).
4. *Branch 1: Create New User:* Admin enters name, email, phone number, and selects role (Dispatcher, Station Operator, Logistics Manager, Admin).
5. *Branch 2: Modify Role / Permissions:* Admin updates role assignment and assigned stations/regions.
6. *Branch 3: Suspend/Deactivate User:* Admin marks account as suspended and provides a justification reason.
7. **System** validates inputs (email uniqueness, valid role enum, mandatory fields).
8. **System** executes transaction: updates database, revokes active refresh tokens if role changed or account suspended.
9. **System** dispatches invitation/notification email with temporary credentials or status notice.
10. **System Administrator** receives confirmation and views updated audit trail.

#### Activity Diagram: UC-OTH-02 (User & Role Management)

```mermaid
flowchart TD
    subgraph AdminLane["System Administrator"]
        A_Start([Start]) --> A_Open["Access User Management Portal"]
        A_Open --> A_ViewList["Browse & Filter User Accounts"]
        A_ViewList --> A_DecideAction{"Select Action"}
        
        A_DecideAction -->|Create New User| A_FillForm["Enter Staff Details & Select Role"]
        A_DecideAction -->|Edit Role/Station| A_ChangeRole["Update Role or Station Assignment"]
        A_DecideAction -->|Suspend Account| A_Suspend["Select Deactivate Account & Reason"]

        A_FillForm --> A_Submit["Submit Admin Action"]
        A_ChangeRole --> A_Submit
        A_Suspend --> A_Submit

        A_ReviewFeedback["View Confirmation & Audit Entry"] --> A_End([End])
    end

    subgraph SystemLane["SmartDroneDelivery System"]
        A_Submit --> S_ValAdmin["Validate Admin Permissions & Form Data"]
        S_ValAdmin --> S_DataValid{"Data Valid & Unique Email?"}
        
        S_DataValid -->|No| S_ValErr["Return Validation Errors"]
        S_ValErr --> A_ViewList

        S_DataValid -->|Yes| S_Persist["Execute DB Transaction (Create/Update User)"]
        S_Persist --> S_Audit["Write to Security Audit Log"]
        
        S_Audit --> S_CheckRevoke{"Role Changed or Suspended?"}
        S_CheckRevoke -->|Yes| S_RevokeTokens["Invalidate Active Refresh Tokens"]
        S_CheckRevoke -->|No| S_NotifyTask["Prepare Notification Payload"]
        S_RevokeTokens --> S_NotifyTask

        S_NotifyTask --> S_TriggerEmail["Send Account Notice via Notification Service"]
        S_TriggerEmail --> S_Confirm["Return 200 OK & Updated User Record"]
        S_Confirm --> A_ReviewFeedback
    end

    subgraph NotifyLane["Notification Service"]
        S_TriggerEmail -.-> N_Send["Dispatch Email with Login Credentials / Notice"]
    end
```

---

### UC-OTH-03: Manage Delivery Addresses & Coordinates
- **Primary Actor:** Customer
- **Supporting Systems:** SmartDroneDelivery System, Geocoding & Geofence Service
- **Preconditions:** Customer is logged in to the mobile or web application.
- **Postconditions:** Delivery address is validated, geocoded with coordinates (Latitude/Longitude), checked against allowed drone delivery zones, and stored in Customer's address book.
- **Trigger:** Customer wants to add, edit, or set a default landing address for future drone deliveries.

#### Detailed Flow of Events:
1. **Customer** navigates to "Saved Addresses" in the profile section.
2. **Customer** clicks "Add New Address" or selects an existing address to edit.
3. **Customer** inputs address line, building/campus details, and pins exact location on an interactive map.
4. **Customer** submits address details.
5. **System** forwards coordinates to Geofencing / GIS module to verify flight zone eligibility.
6. **System** evaluates eligibility:
   - Within operational radius of at least one drone hub.
   - Outside prohibited airspace (no-fly zones, high-voltage corridors, airport zones).
   - Safe landing clearance flag.
7. *Alternative Flow A1 (Outside Flight Zone):* If outside operational territory, System informs customer with a boundary warning, suggesting the nearest authorized Landing Station.
8. *Main Flow Success:* System saves address, sets default flag if chosen, and returns validated status.
9. **Customer** sees the verified landing pin with flight zone confirmation.

#### Activity Diagram: UC-OTH-03 (Manage Delivery Addresses)

```mermaid
flowchart TD
    subgraph CustomerLane["Customer"]
        C_Start([Start]) --> C_Open["Open Address Book"]
        C_Open --> C_Input["Input Address Text & Pin Map Coordinates"]
        C_Input --> C_Submit["Submit Address for Verification"]
        C_FixLocation["Adjust Pin Location or Select Nearby Station"]
        C_SuccessView["View Verified Address with Green Flight Zone Badge"] --> C_End([End])
    end

    subgraph SystemLane["SmartDroneDelivery System"]
        C_Submit --> S_FormatCheck{"Coordinates & Fields Valid?"}
        S_FormatCheck -->|No| S_FormErr["Return Input Format Error"]
        S_FormErr --> C_Input

        S_FormatCheck -->|Yes| S_ForwardGIS["Request Geofence & Airspace Evaluation"]
        S_ForwardGIS --> S_ReceiveGIS{"Airspace Status Safe & In-Zone?"}
        
        S_ReceiveGIS -->|No / Restricted| S_AirspaceErr["Return 'Address Outside Drone Delivery Zone' & Suggest Hubs"]
        S_AirspaceErr --> C_FixLocation
        C_FixLocation --> C_Input

        S_ReceiveGIS -->|Yes| S_CheckDefault{"Customer Checked 'Set as Default'?"}
        S_CheckDefault -->|Yes| S_UnsetDefault["Clear Previous Default Address Flag"]
        S_CheckDefault -->|No| S_SaveAddress["Insert/Update Address with Geo-Coordinates"]
        S_UnsetDefault --> S_SaveAddress

        S_SaveAddress --> S_RespOK["Return Success Response with Station Mapping"]
        S_RespOK --> C_SuccessView
    end

    subgraph GISLane["GIS & Geofencing Engine"]
        S_ForwardGIS -.-> G_Analyze["Evaluate Drone Hub Coverage & No-Fly Zones"]
        G_Analyze -.-> S_ReceiveGIS
    end
```

---

### UC-OTH-04: Cancel Delivery Order (Pre-Dispatch Exception)
- **Primary Actor:** Customer (or Dispatcher on behalf of customer)
- **Supporting Systems:** SmartDroneDelivery System, Payment Gateway, Drone Dispatch Queue
- **Preconditions:** Order has been placed and is currently in `PENDING`, `APPROVED`, or `PREPARING` status (strictly before drone takeoff/flight initiation).
- **Postconditions:** Order status is transitioned to `CANCELLED`, scheduled drone flight slot is released back to inventory, refund is initiated, and parties are notified.
- **Trigger:** Customer requests order cancellation from the order tracking screen, or Dispatcher aborts pre-flight order due to operational constraints.

#### Detailed Flow of Events:
1. **Customer / Dispatcher** views the active order and clicks "Cancel Delivery".
2. **Customer / Dispatcher** selects a cancellation reason (e.g., changed mind, wrong address, weather/operational notice).
3. **System** acquires an optimistic concurrency lock on the order and checks the current order lifecycle state.
4. *Alternative Flow A1 (Drone Already Dispatched):* If state is `IN_FLIGHT`, cancellation is rejected with message: "Drone has already departed; in-flight cancellation is not permitted. Please initiate return upon arrival."
5. *Main Flow:* If state is eligible (`PENDING`, `APPROVED`, or `PREPARING`):
   - **System** changes status to `CANCELLED`.
   - **System** cancels drone reservation and frees assigned hub pad slot.
   - **System** calls Payment Gateway to void authorization or issue automated refund.
   - **System** logs cancellation reason and triggers push notification / email.
6. **Customer** receives cancellation confirmation and refund reference ID.
7. **Dispatcher** sees updated queue with freed drone capacity.

#### Activity Diagram: UC-OTH-04 (Cancel Delivery Order)

```mermaid
flowchart TD
    subgraph ActorLane["Customer / Dispatcher"]
        O_Start([Start]) --> O_SelectOrder["Select Active Delivery Order"]
        O_SelectOrder --> O_ClickCancel["Click 'Cancel Order' & Provide Reason"]
        O_ClickCancel --> O_SubmitCancel["Submit Cancellation Request"]
        O_ViewReject["View Rejection Notice: 'Flight Already Dispatched'"] --> O_EndFail([End - Unsuccessful])
        O_ViewSuccess["Receive Cancellation Confirmation & Refund Receipt"] --> O_EndSuccess([End - Cancelled])
    end

    subgraph SystemLane["SmartDroneDelivery System (Order & Workflow Engine)"]
        O_SubmitCancel --> S_LockOrder["Acquire Lock on Order ID"]
        S_LockOrder --> S_EvalState{"Current Order Status?"}

        S_EvalState -->|IN_FLIGHT or DELIVERED| S_Reject["Reject Cancellation (Status Non-reversible)"]
        S_Reject --> S_ReleaseLock1["Release Lock"]
        S_ReleaseLock1 --> O_ViewReject

        S_EvalState -->|PENDING / APPROVED / PREPARING| S_SetCancelled["Update Status to 'CANCELLED'"]
        S_SetCancelled --> S_DeallocateDrone["Release Scheduled Drone & Station Pad Reservation"]
        S_DeallocateDrone --> S_TriggerRefund["Invoke Payment Gateway Refund"]
        
        S_TriggerRefund --> S_LogAudit["Audit Log: Cancellation Reason & User"]
        S_LogAudit --> S_BroadcastEvent["Broadcast SignalR Order Status Changed Event"]
        S_BroadcastEvent --> S_ReleaseLock2["Release Lock"]
        S_ReleaseLock2 --> O_ViewSuccess
    end

    subgraph PaymentLane["Payment Gateway"]
        S_TriggerRefund -.-> P_Process["Process Automatic Void / Refund"]
        P_Process -.-> S_LogAudit
    end
```

---

### UC-OTH-05: Configure & Manage Landing Stations
- **Primary Actor:** Logistics Manager
- **Supporting Systems:** SmartDroneDelivery System, Station Hardware Gateway
- **Preconditions:** Logistics Manager is authenticated with Manager privileges.
- **Postconditions:** Landing Station details, drone pads, charging docks, secure locker units, and operational geofences are created or updated in the platform.
- **Trigger:** Business expansion requires registering a new landing hub, or operational maintenance necessitates modifying station capacity.

#### Detailed Flow of Events:
1. **Logistics Manager** opens the "Landing Stations" management module.
2. **Logistics Manager** chooses to register a new station or edit an existing one.
3. **Logistics Manager** defines:
   - Station Name, Code, and Address.
   - GPS Boundary & Exact Pad Coordinates.
   - Number of Drone Landing Pads and Charger specifications.
   - Number of Storage Lockers and Station Operator assignment.
   - Operating Hours and Wind/Weather threshold rules.
4. **Logistics Manager** submits configuration.
5. **System** validates coordinate uniqueness, pad numbering, and operational rules.
6. **System** saves station entity and provisions station topic channels for telemetry.
7. **System** tests connectivity to the Station Hardware Gateway (ping test to station IoT controller).
8. *Alternative Flow A1 (Hardware Unreachable):* System marks station as `CREATED_OFFLINE`, notifying the manager to verify site network equipment.
9. *Main Flow Success:* System marks station as `ACTIVE_ONLINE`.
10. **Logistics Manager** sees the updated station list and operational map pin.

#### Activity Diagram: UC-OTH-05 (Manage Landing Stations)

```mermaid
flowchart TD
    subgraph ManagerLane["Logistics Manager"]
        M_Start([Start]) --> M_Open["Open Landing Station Management Module"]
        M_Open --> M_Form["Fill Station Details (Coords, Pads, Lockers, Operators)"]
        M_Form --> M_Submit["Submit Station Configuration"]
        M_FixErrors["Correct Form Errors"]
        M_ViewStatus["Review Station Deployment Status & Dashboard Pin"] --> M_End([End])
    end

    subgraph SystemLane["SmartDroneDelivery System"]
        M_Submit --> S_ValStation{"Validate Station Data & Geo-Bounds?"}
        S_ValStation -->|Invalid| S_ReturnValErr["Return Validation Errors"]
        S_ReturnValErr --> M_FixErrors
        M_FixErrors --> M_Form

        S_ValStation -->|Valid| S_SaveStation["Save Station & Pad Topology to Database"]
        S_SaveStation --> S_PingGateway["Send Health Handshake to Station IoT Gateway"]
        
        S_PingGateway --> S_GatewayResp{"IoT Gateway Responded?"}
        S_GatewayResp -->|No| S_SetOffline["Set Status = 'PENDING_OFFLINE_SETUP'"]
        S_GatewayResp -->|Yes| S_SetActive["Set Status = 'ACTIVE_READY'"]

        S_SetOffline --> S_NotifyMgr["Generate Manager Alert Notification"]
        S_SetActive --> S_NotifyMgr
        S_NotifyMgr --> M_ViewStatus
    end

    subgraph GatewayLane["Station Hardware / IoT Controller"]
        S_PingGateway -.-> G_Handshake["Perform Self-Test on Pads & Lockers"]
        G_Handshake -.-> S_GatewayResp
    end
```

---

### UC-OTH-06: Monitor Landing Station Availability & Maintenance
- **Primary Actor:** Station Operator (and Logistics Manager)
- **Supporting Systems:** SmartDroneDelivery System, Telemetry / SignalR Hub
- **Preconditions:** Station Operator is logged in and assigned to the local landing station.
- **Postconditions:** Pad occupancy, locker availability, and maintenance states (e.g., Pad Obstruction, Charger Malfunction) are tracked and updated in real-time.
- **Trigger:** Scheduled shift inspection, automated sensor alert from landing pad, or weather hazard requiring pad shutdown.

#### Detailed Flow of Events:
1. **Station Operator** opens the "Station Control Center" on their tablet or terminal.
2. **System** streams real-time status of all landing pads (Available, Reserved, Occupied, Faulted), battery chargers, and storage lockers via SignalR.
3. **Station Operator** observes a physical issue (e.g., debris on Pad 2 or malfunctioning charging contact) or receives automated sensor alarm.
4. **Station Operator** selects the affected component and toggles state to `MAINTENANCE_LOCKED`, specifying the reason code and estimated downtime.
5. **System** immediately updates component availability in the database.
6. **System** evaluates if active flight dispatches are scheduled for this pad:
   - *Alternative Flow A1 (Active Reservation Conflict):* System alerts Dispatcher to reroute approaching drone to an alternate available pad or fallback hub.
7. **System** broadcasts station capacity reduction to Dispatcher and routing engine.
8. Once repairs are completed, **Station Operator** conducts functional check and toggles status back to `OPERATIONAL`.
9. **System** re-enables pad in scheduling pool.

#### Activity Diagram: UC-OTH-06 (Monitor Station Availability & Maintenance)

```mermaid
flowchart TD
    subgraph OperatorLane["Station Operator"]
        SO_Start([Start]) --> SO_OpenDashboard["Open Station Telemetry Dashboard"]
        SO_OpenDashboard --> SO_InspectPads["Monitor Pads, Chargers & Storage Lockers"]
        SO_InspectPads --> SO_IdentifyIssue{"Issue / Hazard Detected?"}
        
        SO_IdentifyIssue -->|No| SO_NormalOps["Continue Shift Operations & Telemetry Monitoring"]
        SO_NormalOps --> SO_EndRoutine([End Routine])

        SO_IdentifyIssue -->|Yes| SO_SelectComponent["Select Pad / Locker Unit"]
        SO_SelectComponent --> SO_ToggleMaint["Toggle Status to 'MAINTENANCE_LOCKED' & Enter Reason"]
        SO_ToggleMaint --> SO_SubmitMaint["Submit Maintenance Order"]
        
        SO_Repair["Perform Physical Clearance / Repair"]
        SO_Reopen["Mark Component 'OPERATIONAL' After Inspection"] --> SO_EndRoutine
    end

    subgraph SystemLane["SmartDroneDelivery System (Realtime Core)"]
        SO_SubmitMaint --> S_UpdateDB["Update Pad/Locker State to 'UNAVAILABLE'"]
        S_UpdateDB --> S_CheckConflicts{"Approaching Drone Assigned to this Pad?"}
        
        S_CheckConflicts -->|Yes| S_TriggerAlert["Raise High-Priority Reroute Alert for Dispatcher"]
        S_CheckConflicts -->|No| S_BroadcastState["Broadcast Updated Station Capacity via SignalR"]
        S_TriggerAlert --> S_BroadcastState

        S_BroadcastState --> SO_Repair
        SO_Repair --> SO_Reopen
        SO_Reopen --> S_RestoreDB["Update State to 'AVAILABLE' & Update Dispatch Engine"]
    end

    subgraph DispatcherLane["Dispatcher"]
        S_TriggerAlert -.-> D_Reroute["Review Reroute Plan & Reassign Drone to Alternate Pad"]
    end
```

---

### UC-OTH-07: Customer Package Pickup at Landing Station
- **Primary Actor:** Customer
- **Secondary Actor:** Station Operator (for attended stations) or Smart Locker Controller (for unattended hubs)
- **Supporting Systems:** SmartDroneDelivery System (Pickup Verification Service), Notification Service
- **Preconditions:** Drone has landed safely, unloaded the package into the station holding area/locker, and customer received a "Ready for Pickup" notification containing a 6-digit OTP and QR code.
- **Postconditions:** Customer identity is verified, package is physically handed over or locker door unlocked, order status changes to `DELIVERED`, and pickup confirmation is logged.
- **Trigger:** Customer arrives at the Landing Station to retrieve their package.

#### Detailed Flow of Events:
1. **Customer** arrives at the landing station and opens the mobile app showing pickup credentials (QR Code / OTP).
2. *Branch 1 (Unattended Smart Locker):*
   - Customer scans QR code or types OTP at the locker kiosk terminal.
   - Locker controller validates code with SmartDroneDelivery API.
   - System unlocks designated locker door.
   - Customer takes package and shuts door.
3. *Branch 2 (Attended Station Hub):*
   - Customer presents QR code to **Station Operator**.
   - Station Operator scans QR code via handheld mobile scanner.
   - System displays package ID, recipient name, and shelf location.
   - Station Operator retrieves package, cross-checks recipient, and clicks "Confirm Handover".
4. *Alternative Flow A1 (Invalid / Expired OTP):*
   - System rejects code and alerts customer. Customer can request an OTP resend via SMS/Email.
5. **System** updates order status to `COMPLETED_PICKUP`.
6. **System** timestamps pickup transaction, logs photo proof or digital signature, and releases station storage capacity.
7. **Customer** and **Station Operator** receive confirmation; customer is prompted for delivery rating.

#### Activity Diagram: UC-OTH-07 (Customer Package Pickup)

```mermaid
flowchart TD
    subgraph CustomerLane["Customer"]
        PU_Start([Start]) --> PU_Arrive["Arrive at Landing Station Hub"]
        PU_Arrive --> PU_DisplayCode["Present Pickup QR Code / Enter OTP on Kiosk"]
        PU_PickupPackage["Retrieve Package from Locker or Operator"]
        PU_RateService["Submit Delivery Rating & Feedback"] --> PU_End([End])
    end

    subgraph OperatorLane["Station Operator / Kiosk Controller"]
        PU_DisplayCode --> O_Scan["Scan Customer QR Code / Read Input"]
        O_Scan --> O_ForwardVerification["Submit Token to Verification Endpoint"]
        O_OpenLocker["Unlock Locker Compartment / Hand Package to Customer"]
        O_ConfirmHandover["Confirm Physical Handover Complete"]
    end

    subgraph SystemLane["SmartDroneDelivery System (Verification API)"]
        O_ForwardVerification --> S_ValToken{"OTP / QR Token Valid & Unexpired?"}
        
        S_ValToken -->|No| S_TokenFail["Return 'Invalid / Expired Verification Code'"]
        S_TokenFail --> PU_DisplayCode

        S_ValToken -->|Yes| S_MatchOrder["Retrieve Order & Storage Slot Location"]
        S_MatchOrder --> S_TriggerUnlock["Send Unlock / Handover Authorization"]
        S_TriggerUnlock --> O_OpenLocker
        
        O_OpenLocker --> PU_PickupPackage
        PU_PickupPackage --> O_ConfirmHandover
        O_ConfirmHandover --> S_FinalizeOrder["Set Order Status = 'DELIVERED / PICKED_UP'"]
        
        S_FinalizeOrder --> S_FreeStorage["Increment Available Station Storage Capacity"]
        S_FreeStorage --> S_SendReceipt["Send Digital Receipt & Feedback Request"]
        S_SendReceipt --> PU_RateService
    end
```

---

### UC-OTH-08: Customer AI Assistant Interaction
- **Primary Actor:** Customer
- **Secondary Actor:** Human Support / Dispatcher (Escalation)
- **Supporting Systems:** SmartDroneDelivery System, AI Assistant Service (RAG + LLM Engine)
- **Preconditions:** Customer is logged in to mobile or web application.
- **Postconditions:** Customer receives instant AI-assisted responses for inquiries (order status, ETA, delivery policies, dangerous goods restrictions), or session is escalated to human support.
- **Trigger:** Customer opens the "AI Support Chat" widget and submits a question.

#### Detailed Flow of Events:
1. **Customer** types a question in the in-app chat (e.g., "Where is my package?", "What items cannot be delivered by drone?").
2. **System** receives user query, enriches prompt with customer context (active order IDs, recent destinations).
3. **AI Assistant Service** executes intent classification and semantic retrieval (RAG) against platform knowledge base:
   - *Intent: Order Status / Tracking:* AI retrieves live telemetry, current drone state, and calculated ETA.
   - *Intent: Operational / FAQs:* AI retrieves guidelines on package dimensions (< 5kg), weather policies, and operating hours.
4. **AI Assistant Service** evaluates response confidence and sentiment.
5. *Alternative Flow A1 (Low Confidence / Customer Frustration / Complex Request):*
   - If user asks for refund, reports damaged goods, or expresses high frustration, AI initiates handover.
   - **System** alerts human support / **Dispatcher** with full conversation context.
   - Dispatcher takes over chat session.
6. *Main Flow Success:* AI generates concise, helpful answer with actionable UI buttons (e.g., "Track Live Drone").
7. **Customer** views response and continues conversation or closes widget.

#### Activity Diagram: UC-OTH-08 (Customer AI Assistant)

```mermaid
flowchart TD
    subgraph CustomerLane["Customer"]
        AI_Start([Start]) --> AI_OpenChat["Open In-App AI Support Chat"]
        AI_OpenChat --> AI_TypeMsg["Submit Question / Voice Prompt"]
        AI_ViewAns["Read AI Response / Click Quick Actions"]
        AI_ChatHuman["Chat with Human Support Specialist"] --> AI_End([End])
        AI_ViewAns --> AI_End
    end

    subgraph SystemLane["SmartDroneDelivery System Backend"]
        AI_TypeMsg --> S_Sanitize["Sanitize Input & Attach User Context (Active Orders)"]
        S_Sanitize --> S_ForwardAI["Invoke AI Assistant Gateway"]
        S_ReceiveAI["Receive Formatted Message Payload"]
        S_ReceiveAI --> AI_ViewAns
    end

    subgraph AIEngineLane["AI Assistant Service (LLM & RAG)"]
        S_ForwardAI -.-> AI_Intent["Classify Intent & Retrieve Vector Embeddings"]
        AI_Intent -.-> AI_FetchState{"Requires Live Delivery Context?"}
        AI_FetchState -.->|Yes| AI_QueryTelemetry["Query Live Drone Telemetry & ETA Engine"]
        AI_FetchState -.->|No| AI_GenAns["Synthesize Response using Knowledge Base"]
        AI_QueryTelemetry -.-> AI_GenAns
        
        AI_GenAns -.-> AI_EvalEscalate{"Confidence High & Sentiment Normal?"}
        AI_EvalEscalate -.->|Yes| S_ReceiveAI
        AI_EvalEscalate -.->|No / User Requests Human| AI_RouteHuman["Flag Session for Human Handover"]
    end

    subgraph SupportLane["Human Support / Dispatcher"]
        AI_RouteHuman -.-> H_ReceiveQueue["Receive Escalated Chat with Conversation History"]
        H_ReceiveQueue --> AI_ChatHuman
    end
```

---

### UC-OTH-09: Generate Operational Analytics & Delivery KPI Reports
- **Primary Actor:** Logistics Manager
- **Supporting Systems:** SmartDroneDelivery System, Analytics Engine / Data Warehouse
- **Preconditions:** Logistics Manager is authenticated and possesses Manager or Executive permissions.
- **Postconditions:** Analytics dashboards are filtered, aggregated metrics (on-time delivery rate, drone battery cycle health, station turnaround, incident count) are rendered, and exportable reports (PDF/CSV) are generated.
- **Trigger:** Weekly/monthly operational review, performance audit, or fleet optimization requirement.

#### Detailed Flow of Events:
1. **Logistics Manager** navigates to "Analytics & Reporting" module.
2. **Logistics Manager** selects date range, target region, landing station hubs, and drone fleet IDs.
3. **Logistics Manager** specifies desired KPI metric views (e.g., On-Time Delivery Rate, Turnaround Time, Energy Consumption per KM, Aborted Missions).
4. **Logistics Manager** clicks "Generate Report" or "Export Data".
5. **System** aggregates raw telemetry, flight logs, and order completion tables from PostgreSQL / read-replicas.
6. **Analytics Engine** computes statistical aggregations, trend lines, and outlier benchmarks.
7. *Main Flow View:* System renders interactive charts, heatmaps, and summary KPI cards.
8. *Export Flow:* If manager requested export, System compiles data into structured PDF or CSV format and generates a secure download link.
9. **Logistics Manager** downloads report and reviews operational insights.

#### Activity Diagram: UC-OTH-09 (Operational Analytics & KPI Reporting)

```mermaid
flowchart TD
    subgraph ManagerLane["Logistics Manager"]
        R_Start([Start]) --> R_Nav["Navigate to Analytics & KPI Portal"]
        R_Nav --> R_SetFilters["Select Date Range, Hubs, Drones & Metrics"]
        R_SetFilters --> R_ClickGen["Click 'Generate Dashboard' or 'Export Report'"]
        R_InspectDashboard["Interact with Heatmaps & Performance Graphs"]
        R_DownloadFile["Download Exported PDF / CSV Summary"] --> R_End([End])
        R_InspectDashboard --> R_End
    end

    subgraph SystemLane["SmartDroneDelivery System (API & Reporting Engine)"]
        R_ClickGen --> S_ParseFilter["Validate Date Range & Authorization"]
        S_ParseFilter --> S_ExecQuery["Execute Aggregation Queries on Analytics Store"]
        
        S_ExecQuery --> S_ComputeKPIs["Calculate On-Time %, Mean Flight Time, Battery Wear"]
        S_ComputeKPIs --> S_CheckExport{"Export Requested?"}
        
        S_CheckExport -->|No - View Only| S_RenderJSON["Format Visual Metric Series"]
        S_RenderJSON --> R_InspectDashboard

        S_CheckExport -->|Yes - Download| S_BuildDoc["Compile PDF Report with Visuals / Generate CSV"]
        S_BuildDoc --> S_SaveMinIO["Store Generated Document in MinIO Bucket"]
        S_SaveMinIO --> S_GenLink["Generate Signed Temporary Download URL"]
        S_GenLink --> R_DownloadFile
    end

    subgraph StorageLane["MinIO Object Storage"]
        S_SaveMinIO -.-> M_Store["Persist Report Artifact"]
    end
```

---

### UC-OTH-10: AI Delivery Summarization & Anomaly Detection
- **Primary Actor:** Logistics Manager / Dispatcher
- **Supporting Systems:** SmartDroneDelivery System, AI Analytics Service
- **Preconditions:** Sufficient operational flight and order logs exist for the specified operational window.
- **Postconditions:** AI synthesizes complex multi-dimensional telemetry, detects anomalous flight events (unexpected battery drops, geofence micro-deviations, excessive pad turnaround delays), and provides natural language executive summaries with recommendations.
- **Trigger:** Automated end-of-day summary trigger or on-demand dispatch audit.

#### Detailed Flow of Events:
1. **Dispatcher / Logistics Manager** triggers "AI Operational Summary" for a specific hub or time frame.
2. **System** collects raw logs: telemetry streams, weather conditions, aborted flights, and maintenance records.
3. **AI Analytics Service** processes logs through anomaly detection algorithms:
   - Identifies pattern outliers (e.g., Drone DR-04 had 35% higher battery drain on North route due to headwind).
   - Identifies bottleneck stations where package loading exceeded target window (> 4 minutes).
4. **AI Analytics Service** constructs an executive natural language briefing summarizing key highlights, failures, and proactive operational recommendations.
5. **System** displays the AI summary card alongside severity-ranked incident lists.
6. **Dispatcher / Manager** reviews findings, acknowledges high-priority anomalies, and can assign maintenance tickets directly from recommendations.

#### Activity Diagram: UC-OTH-10 (AI Summarization & Anomaly Detection)

```mermaid
flowchart TD
    subgraph UserLane["Logistics Manager / Dispatcher"]
        AS_Start([Start]) --> AS_Request["Request Daily AI Operational Briefing"]
        AS_Request --> AS_SelectHub["Select Time Window & Fleet / Hub Scope"]
        AS_SelectHub --> AS_Submit["Execute AI Summary Analysis"]
        AS_Review["Review Executive Briefing & Anomaly Highlights"]
        AS_TakeAction["Acknowledge Anomaly & Trigger Maintenance Work Order"] --> AS_End([End])
    end

    subgraph SystemLane["SmartDroneDelivery System"]
        AS_Submit --> S_ExtractLogs["Extract Telemetry, Weather, Flight & Order Logs"]
        S_ExtractLogs --> S_SendAI["Send Structured Dataset to AI Analytics Service"]
        S_FormatCards["Assemble Briefing UI with Risk Scores & Recommendations"]
        S_FormatCards --> AS_Review
        
        AS_TakeAction --> S_CreateTicket["Generate Maintenance Ticket for Affected Drone/Pad"]
        S_CreateTicket --> AS_End
    end

    subgraph AIServiceLane["AI Analytics & Anomaly Detection Service"]
        S_SendAI -.-> AI_Detect["Run Anomaly Detection (Battery Drains, Reroutes, Delays)"]
        AI_Detect -.-> AI_Rank["Cluster Anomalies by Root Cause & Impact Score"]
        AI_Rank -.-> AI_Summarize["Generate Natural Language Synthesis & Actionable Advice"]
        AI_Summarize -.-> S_FormatCards
    end
```

---

## 4. Traceability & Acceptance Matrix

| Use Case ID | Use Case Name | Primary Actors | Diagram Type | Swimlanes Included | Acceptance Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **UC-OTH-01** | User Authentication & RBAC | Any User, System | UML Activity | User, SmartDroneDelivery System | Verified |
| **UC-OTH-02** | Manage Users & Role Assignments | System Administrator | UML Activity | System Administrator, SmartDroneDelivery System, Notification Service | Verified |
| **UC-OTH-03** | Manage Delivery Addresses | Customer | UML Activity | Customer, SmartDroneDelivery System, GIS Engine | Verified |
| **UC-OTH-04** | Cancel Delivery Order (Pre-Dispatch) | Customer, Dispatcher | UML Activity | Customer/Dispatcher, SmartDroneDelivery System, Payment Gateway | Verified |
| **UC-OTH-05** | Configure & Manage Landing Stations | Logistics Manager | UML Activity | Logistics Manager, SmartDroneDelivery System, IoT Hardware Gateway | Verified |
| **UC-OTH-06** | Monitor Station Availability & Maintenance | Station Operator, Dispatcher | UML Activity | Station Operator, SmartDroneDelivery System, Dispatcher | Verified |
| **UC-OTH-07** | Customer Package Pickup at Station | Customer, Station Operator | UML Activity | Customer, Station Operator/Kiosk, SmartDroneDelivery System | Verified |
| **UC-OTH-08** | Customer AI Assistant Interaction | Customer, Support Staff | UML Activity | Customer, SmartDroneDelivery System, AI Assistant (LLM/RAG), Human Support | Verified |
| **UC-OTH-09** | Operational Analytics & KPI Reporting | Logistics Manager | UML Activity | Logistics Manager, SmartDroneDelivery System, MinIO Storage | Verified |
| **UC-OTH-10** | AI Summarization & Anomaly Detection | Logistics Manager, Dispatcher | UML Activity | Manager/Dispatcher, SmartDroneDelivery System, AI Analytics Service | Verified |

---

## 5. Summary for Sprint 1-2 & Implementation Roadmap

- **Owner:** Khai
- **Sprint Alignment:** Sprint 1-2 (Work Package 1: Requirement Analysis & Process Modeling)
- **Downstream Artifacts:**
  1. RESTful API route mapping (`/api/v1/auth`, `/api/v1/users`, `/api/v1/addresses`, `/api/v1/stations`, `/api/v1/ai`, `/api/v1/analytics`).
  2. Database schema design (PostgreSQL tables for Users, Roles, Stations, Pads, Lockers, Addresses, AuditLogs).
  3. UI Wireframes for Customer Mobile App (Pickup QR, Address Pinning, AI Chat) and Management Web Portal (User Admin, Station Setup, Analytics).
