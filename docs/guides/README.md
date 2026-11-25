# LA-184: Installation & User Guides Index — SmartDroneDelivery

This directory consolidates the installation and role-based user guides for **SmartDroneDelivery**:

- **[Installation Guide (`LA-249`)](installation-guide.md)** — Hardware/software prerequisites, Docker Compose setup (`.env`, PostgreSQL 16, MinIO, ASP.NET Core 8 API, React Web Portal), standalone development mode, and Android (`customer` & `operator` flavors) build/installation steps.
- **[User Manual per Role (`LA-250`)](user-manual.md)** — Step-by-step operational workflows for **Customer**, **Dispatcher**, **Station Operator**, **Logistics Manager**, and **System Administrator**.
- **[Deployment Guide (`LA-182`)](../deployment/deployment-guide.md)** — Production/staging environment variable reference, health checks (`/health/live`, `/health/ready`), smoke testing, and troubleshooting.
- **[Database Backup & Restore Guide (`LA-179`)](../deployment/database-backup-guide.md)** — Automated `pg_dump`/`pg_restore` scripts and verification procedures.
