# Activity Diagrams - Analytics Dashboard & Customer AI Assistant
**Sprint:** Sprint 1-2  
**Parent Ticket:** [LA-194](https://hungndemail.atlassian.net/browse/LA-194)  
**Ticket:** [LA-215](https://hungndemail.atlassian.net/browse/LA-215) - Activity diagram for analytics and the AI assistant  
**Task Description:** Model how a manager views the dashboard and how a customer asks the AI assistant about an order.  
**Use Cases Covered:** [UC-09, UC-19, UC-20](../requirements/use-cases.md)  
**Entities Involved:** `ai_chat_sessions`, `ai_chat_messages`, `ai_eta_predictions`, `delivery_orders`, `flight_missions`, `flight_telemetry_logs`, `landing_stations`, `drones`  

---

## 1. Overview & Context

This specification models the two complementary intelligent workflows defined in **LA-215**:
1. **Workflow 1: Manager Views the Dashboard (UC-19, UC-20)**
   - Role-based operational monitoring by the Logistics Manager.
   - Dynamic metric filtering (date ranges, landing station hubs, drone models, delivery status).
   - Real-time caching, KPI calculation (success rate, average flight time, drone utilization, station turnaround), and report export to MinIO.
   - AI-driven operational synthesis and anomaly highlighting.
2. **Workflow 2: Customer Asks the AI Assistant About an Order (UC-09, UC-20)**
   - Conversational AI in-app experience for the Customer.
   - Strict data privacy: Customers can only query their own orders and packages.
   - Real-time context grounding: Augments prompt with live drone telemetry, flight status (`IN_TRANSIT`, `ARRIVED`), and AI ETA predictions.
   - Handling exceptions: No active orders, privacy denial on foreign tracking numbers, delayed flights due to weather, and seamless escalation to human dispatchers.

---

## 2. Actors & System Swimlanes

| Actor / Component | Type | Responsibility in Workflow |
|---|---|---|
| **Logistics Manager** | Human Actor | Applies filters, reviews operational KPIs, inspects charts, and requests exports / AI summaries. |
| **Customer** | Human Actor | Interacts with in-app AI chat, queries active orders, asks about ETAs, delays, or delivery policies. |
| **Client UI** | System Component | Web Portal (ReactJS) for Manager Dashboard / Mobile App (Flutter) for Customer AI Chat. |
| **Backend & Analytics API** | System Component | ASP.NET Core API (`/api/analytics`, `/api/ai/chat`), authentication, authorization, caching layer. |
| **Database & Object Storage** | System Component | PostgreSQL (orders, missions, telemetry, chat logs) and MinIO (exported PDF/CSV reports). |
| **AI Assistant & Analytics Engine** | System Component | LLM, RAG retriever, ETA prediction model, and telemetry anomaly analyzer. |
| **Dispatcher / Human Support** | Human Actor | Receives escalated chat sessions when customer requests a human or issue complexity exceeds AI threshold. |

---

## 3. Workflow 1: Logistics Manager Views the Dashboard (UC-19, UC-20)

### Specification
* **Actors:** Logistics Manager, Client Web Portal, Backend API, PostgreSQL / Redis, AI Engine, MinIO.
* **Preconditions:** Logistics Manager is logged in with `LOGISTICS_MANAGER` or `SYSTEM_ADMIN` role.
* **Postconditions:** Operational KPIs, charts, and maps are rendered; optional PDF/CSV report generated or AI executive summary generated.

### Detailed Flow & Decision Rules:
1. Manager accesses `/dashboard/analytics` and specifies filters:
   - Date range (today, last 7 days, custom month).
   - Target landing stations (all or specific hub).
   - Drone fleet selection and order status filters.
2. System checks date validity and user authorization.
3. System checks Redis cache for aggregated metrics of the selected filter key.
4. *Cache Miss:* System queries PostgreSQL read-replica:
   - Aggregates: Total Orders, On-Time Delivery Rate (`DELIVERED` within ETA / Total), Mean Flight Duration, Failure Breakdown (weather, battery abort, timeout).
   - Drone Fleet Metrics: Active vs Idle vs Charging vs Maintenance drones.
   - Station Metrics: Slot occupancy rate and pad turnaround time.
5. If result set is empty $\rightarrow$ Display "No data found for selected criteria" with filter reset button.
6. *Manager Action 1 - View Charts:* Interactive graphs (throughput by hour, heatmaps) rendered in UI.
7. *Manager Action 2 - Export Report:* System compiles PDF/CSV, persists in MinIO bucket `analytics-reports`, and returns presigned download URL.
8. *Manager Action 3 - Request AI Summary:* AI Engine synthesizes daily operational patterns, identifies bottlenecks, and highlights flight anomalies.

### UML Activity Diagram: Manager Views the Dashboard

```mermaid
flowchart TB
    Start((Start)) --> M_Open

    subgraph Manager["Logistics Manager"]
        M_Open["Open Analytics & Dashboard Portal"]
        M_Filter["Select Filters: Date Range, Landing Stations, Fleet ID"]
        M_SubmitFilter["Click 'Apply Filters'"]
        M_Open --> M_Filter --> M_SubmitFilter
        
        M_ChooseAction{"Choose Next Action"}
        M_ViewVisuals["Inspect Interactive Charts, Heatmaps & KPI Cards"]
        M_ExportDoc["Click 'Export PDF / CSV Report'"]
        M_ReqAISummary["Click 'Generate AI Operational Summary'"]
        M_Download["Download Report via Signed Link"]
        M_ReadSummary["Read AI Executive Briefing & Anomaly Alerts"]
        
        M_ViewVisuals --> M_EndDashboard([End - Monitoring Completed])
        M_Download --> M_EndDashboard
        M_ReadSummary --> M_EndDashboard
    end

    M_SubmitFilter --> S_ValFilter

    subgraph System["Backend & Analytics API (ASP.NET Core)"]
        S_ValFilter{"Date Range & Permissions Valid?"}
        S_ValFilter -->|Invalid Date / Unauthorized| S_FilterErr["Return 400 Validation Error / 403 Forbidden"]
        S_FilterErr --> M_Filter

        S_ValFilter -->|Valid| S_CheckCache{"Aggregated Data in Redis Cache?"}
        S_CheckCache -->|Cache Hit| S_FormatMetrics["Format Aggregated KPI Metrics JSON"]
        S_CheckCache -->|Cache Miss| S_QueryDB["Execute Analytical Queries on Database"]
        
        S_QueryDB --> S_HasRecords{"Matching Orders Found?"}
        S_HasRecords -->|No| S_EmptyState["Return Empty Result Set & Zero Counters"]
        S_HasRecords -->|Yes| S_ComputeKPIs["Compute On-Time %, Turnaround Time, Failure Rate"]
        S_ComputeKPIs --> S_StoreCache["Warm Redis Cache (TTL = 5 mins)"]
        S_StoreCache --> S_FormatMetrics

        S_EmptyState --> M_ViewVisuals
        S_FormatMetrics --> M_ChooseAction
        M_ChooseAction -->|Interact with Charts| M_ViewVisuals

        %% Export Branch
        M_ExportDoc --> S_BuildReport["Compile PDF Report with Charts & Data Tables"]
        S_BuildReport --> S_StoreMinIO["Upload Report to MinIO: 'analytics-reports'"]
        S_StoreMinIO --> S_PresignedURL["Generate Presigned Download URL (Expires in 15m)"]
        S_PresignedURL --> M_Download

        %% AI Summary Branch
        M_ReqAISummary --> S_FetchTelemetry["Package Order Stats, Weather & Telemetry Logs"]
        S_FetchTelemetry --> S_ForwardAIService["Invoke AI Analytics Engine Gateway"]
        S_ReturnAISummary["Return Synthesized Natural Language Summary & Risks"]
        S_ReturnAISummary --> M_ReadSummary
    end

    subgraph Storage["PostgreSQL & MinIO Storage"]
        S_QueryDB -.-> DB_Analytics[(PostgreSQL: orders, missions, logs)]
        S_StoreMinIO -.-> MinIO_Bucket[(MinIO Object Storage)]
    end

    subgraph AIEngine["AI Services Engine"]
        S_ForwardAIService -.-> AI_Analyze["Detect Flight Bottlenecks, Delays & Battery Anomalies"]
        AI_Analyze -.-> AI_Synthesize["Generate Executive Briefing & Recommendations"]
        AI_Synthesize -.-> S_ReturnAISummary
    end
```

---

## 4. Workflow 2: Customer Asks the AI Assistant About an Order (UC-09, UC-20)

### Specification
* **Actors:** Customer, Mobile App / Web Client, AI Gateway Backend, Telemetry & Order Service, AI Assistant (LLM/RAG), Human Dispatcher.
* **Preconditions:** Customer is logged in to their account.
* **Postconditions:** AI retrieves customer's order context, responds with grounded delivery information (status, ETA, live drone position, delay reasons), saves chat history, or escalates to human support.

### Detailed Flow & Decision Rules:
1. Customer opens in-app AI chat and submits a message (e.g. *"When will my order arrive?"*, *"Where is drone DR-02 right now?"*).
2. Backend validates authentication and loads active chat session from `ai_chat_sessions`.
3. System checks intent and extracts order reference if provided:
   - **Privacy Guard Check:** If customer mentions a specific tracking number, verify `delivery_orders.customer_id == authenticated_user_id`.
   - *Security Branch (Access Denied):* If tracking number belongs to another user, AI responds: *"For privacy reasons, you can only view status for orders placed by your account."*
4. **Context Grounding Retrieval:**
   - If no specific order mentioned, query active orders for this customer (`status IN ('APPROVED', 'READY_FOR_TAKEOFF', 'IN_TRANSIT', 'ARRIVED')`).
   - *Branch A (No Active Orders):* AI responds: *"You currently have no active deliveries in transit. Would you like to check past deliveries or help with creating an order?"*
   - *Branch B (Active Order Found):*
     - If `IN_TRANSIT`: Fetch current drone coordinates (`flight_telemetry_logs`), destination station, and latest AI ETA prediction (`ai_eta_predictions`).
     - If delayed: Retrieve flight incident flags (e.g. strong wind, holding pattern).
5. **Prompt Synthesis & LLM Generation:**
   - Prompt includes system instructions (role: Drone Logistics Assistant), verified order facts, ETA timestamp, and secure code instructions.
6. **Confidence & Sentiment Evaluation:**
   - If user expresses anger/frustration or explicitly asks *"I want to speak to a person"* $\rightarrow$ System triggers escalation: flags session, alerts human support / Dispatcher via SignalR, and hands over chat.
   - If normal $\rightarrow$ AI generates friendly, concise response with quick action cards (e.g. *"Track Live Drone on Map"*, *"View Pickup QR"*).
7. Persist user message and AI response into `ai_chat_messages` table.

### UML Activity Diagram: Customer Asks AI Assistant About Order

```mermaid
flowchart TB
    Start((Start)) --> C_OpenChat

    subgraph Customer["Customer"]
        C_OpenChat["Open In-App AI Assistant Chat"]
        C_TypeMessage["Type Query (e.g. 'Where is my delivery?', 'When will it arrive?')"]
        C_OpenChat --> C_TypeMessage
        C_Submit["Send Message"]
        C_TypeMessage --> C_Submit

        C_ReadResponse["Read AI Answer with Live ETA & Order Facts"]
        C_ClickAction["Click Action Button: 'Track Live Map' / 'Show Pickup Code'"]
        C_TalkHuman["Chat Directly with Human Dispatcher / Support Specialist"]
        
        C_ReadResponse --> C_ClickAction --> C_EndChat([End Session])
        C_TalkHuman --> C_EndChat
    end

    C_Submit --> S_AuthCheck

    subgraph Backend["Backend & AI Gateway (ASP.NET Core)"]
        S_AuthCheck{"Customer Authenticated?"}
        S_AuthCheck -->|No| S_401["Return 401: Please Log In"]
        S_401 --> C_OpenChat

        S_AuthCheck -->|Yes| S_LoadSession["Load or Create Session in ai_chat_sessions"]
        S_LoadSession --> S_SaveUserMsg["Insert User Message into ai_chat_messages"]
        S_SaveUserMsg --> S_ExtractIntent["Extract Intent & Order References"]

        S_ExtractIntent --> S_TrackingCheck{"Specific Tracking Number Given?"}
        
        %% Privacy Check Branch
        S_TrackingCheck -->|Yes| S_CheckOwnership{"Does Order Belong to This Customer?"}
        S_CheckOwnership -->|No - Privacy Violation| S_DenyResp["Synthesize Privacy Guard Response: 'Access Restricted'"]
        S_CheckOwnership -->|Yes| S_FetchOrderDetails["Fetch Specific Order Details"]

        %% General Active Order Query
        S_TrackingCheck -->|No| S_QueryActiveOrders["Query Customer's Active Orders in DB"]
        S_QueryActiveOrders --> S_HasActive{"Any Active Orders?"}
        
        S_HasActive -->|None Found| S_NoActiveResp["Construct Friendly Notice: 'No Deliveries Currently In-Flight'"]
        S_HasActive -->|Active Found| S_FetchOrderDetails

        S_FetchOrderDetails --> S_CheckInFlight{"Is Order Status IN_TRANSIT?"}
        S_CheckInFlight -->|Yes| S_GetTelemetry["Fetch Live GPS Position, Battery & AI ETA Prediction"]
        S_CheckInFlight -->|"No (APPROVED / ARRIVED)"| S_GetStationContext["Fetch Station Arrival Status & Pickup Code Availability"]
        
        S_GetTelemetry --> S_AssemblePrompt["Assemble Grounded RAG Prompt with Verified Facts"]
        S_GetStationContext --> S_AssemblePrompt
        S_AssemblePrompt --> S_InvokeLLM["Send Context to AI Assistant Service"]

        S_ReceiveLLM["Receive Generated AI Response"]
        S_ReceiveLLM --> S_CheckHandoff{"User Requested Human or Sentiment Urgent?"}
        
        S_CheckHandoff -->|Yes - Escalate| S_SignalRHandoff["Raise Escalation Event to Dispatcher Support Queue"]
        S_CheckHandoff -->|No| S_SaveBotMsg["Insert Bot Message into ai_chat_messages"]
        
        S_SaveBotMsg --> S_DeliverToApp["Send Formatted Message with Interactive UI Cards"]
        S_DeliverToApp --> C_ReadResponse

        S_DenyResp --> S_SaveBotMsg
        S_NoActiveResp --> S_SaveBotMsg
    end

    subgraph LLMService["AI Assistant Service (LLM & RAG Engine)"]
        S_InvokeLLM -.-> AI_Generate["Generate Grounded Natural Language Response<br/>(Include ETA, Distance, Speed, Station Name)"]
        AI_Generate -.-> S_ReceiveLLM
    end

    subgraph HumanSupport["Dispatcher / Human Support Queue"]
        S_SignalRHandoff -.-> D_Takeover["Dispatcher Claims Session & Joins Live Chat"]
        D_Takeover --> C_TalkHuman
    end
```

---

## 5. Traceability Matrix & Requirements Alignment

| Use Case | Process Name | Primary Actors | Key Decisions Modeled | Requirements |
|---|---|---|---|---|
| **UC-19** | View dashboard and reports | Logistics Manager | Date filter validation, Redis cache hit/miss, empty state, export PDF/CSV via MinIO | FR-28, FR-29 |
| **UC-20** | View ETA and delivery summary | Logistics Manager, Customer | AI operational pattern summarization, anomaly detection, live flight ETA calculation | FR-30, FR-31 |
| **UC-09** | Chat with AI assistant | Customer, Support | Privacy ownership verification, active flight telemetry grounding, escalation to human | FR-32, FR-33 |

---

## 6. Review Notes for Sprint 1-2 Delivery

1. **Strict Data Privacy Shield in AI Chat:** Prompt injection or user queries attempting to inspect another user's tracking number are blocked at the application query layer before reaching the LLM context.
2. **Telemetry Freshness:** The AI service queries the latest telemetry log from `flight_telemetry_logs` (recorded every 2 seconds during flight missions) to provide meter-accurate remaining distances.
3. **Dashboard Caching Strategy:** Aggregated statistics are cached in Redis with a 5-minute sliding window; mutations such as order completion (`DELIVERED`) or mission aborts (`FAILED`) trigger cache invalidation.
