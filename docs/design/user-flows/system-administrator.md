# System Administrator User Flow - SmartDroneDelivery

Screen flow for the System Administrator web portal. It covers user and role management, audit-log review, system health monitoring and database backups.

The flow uses the System Administrator use cases in [use-cases.md](../../requirements/use-cases.md), the administration requirements in [SRS.md](../../requirements/SRS.md) and the role model in [auth-api.md](../../api/auth-api.md).

## User flow

```mermaid
flowchart TB
    Start((Start)) --> Open["Open web portal"]
    Open --> Login["Log in"]
    Login --> Credentials{"Credentials valid?"}
    Credentials -->|no| Error["Show login error"]
    Error --> Login
    Credentials -->|yes| Dashboard["Administration dashboard"]

    Dashboard --> Users["Users and roles"]
    Users --> UserChoice{"Choose action"}
    UserChoice -->|create user| Create["Enter user details<br/>and assign a role"]
    Create --> Validate{"Details valid and unique?"}
    Validate -->|no| UserError["Show validation or conflict message"]
    UserError --> Create
    Validate -->|yes| Save["Create user"]
    Save --> AuditChange["System writes audit log"]
    AuditChange --> Users

    UserChoice -->|open user| UserDetail["User detail"]
    UserDetail --> Manage{"Choose update"}
    Manage -->|assign role| Role["Select one of the five roles"]
    Manage -->|deactivate| Deactivate["Deactivate user"]
    Role --> Confirm["Confirm change"]
    Deactivate --> Confirm
    Confirm --> AuditChange
    Manage -->|back| Users

    Dashboard --> Logs["Audit logs"]
    Logs --> Search["Filter by user, action and date"]
    Search --> Results["View matching audit entries"]
    Results --> LogChoice{"Change search?"}
    LogChoice -->|yes| Search
    LogChoice -->|no| Dashboard

    Dashboard --> Monitoring["System monitoring"]
    Monitoring --> Health["View service health"]
    Health --> BackupChoice{"Open database backups?"}
    BackupChoice -->|yes| Backups["View and manage backups"]
    Backups --> Monitoring
    BackupChoice -->|no| Dashboard

    Dashboard --> Logout["Log out"]
    Logout --> End((End))
```

## Main screens and decisions

| Screen or decision | Purpose | Related use case |
|---|---|---|
| Administration dashboard | Entry point for users, audit logs and system monitoring | UC-21 to UC-23 |
| Users and roles | Find staff or customer accounts and open their details | UC-21 |
| Create user | Create a staff account and assign a role | UC-21 |
| User detail | Assign a role or deactivate an account | UC-21 |
| Audit logs | Search sensitive actions by user, action and date | UC-22 |
| System monitoring | Check service health | UC-23 |
| Database backups | View and manage database backups | UC-23 |

## Flow rules

- Public registration creates Customers; staff accounts are created by a System Administrator.
- A user is assigned one of five roles: Customer, Dispatcher, Station Operator, Logistics Manager or System Administrator.
- User creation must reject invalid details and duplicate email addresses or phone numbers.
- Deactivation changes the account status rather than deleting its business history.
- User creation, role changes and account deactivation are sensitive actions and must be written to the audit log.
- Audit logs are searchable by user, action and date and are read-only in this flow.
- System monitoring covers service health and database backups as defined by the administration use case.
