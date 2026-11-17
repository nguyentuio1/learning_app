# LA-179: PostgreSQL Database Backup & Restore Guide

**Target Database:** `drone_delivery_db` (PostgreSQL 16, service `postgres` / container `drone_postgres` in [docker-compose.yml](../../docker-compose.yml))  
**Backup Format:** PostgreSQL Custom Format (`pg_dump -Fc`) — compressed, supports selective restore via `pg_restore`.

---

## 1. Backup & Restore Scripts

| Platform | Backup Script | Restore Script |
| --- | --- | --- |
| **Linux / macOS / Git Bash** | [scripts/backup-db.sh](../../scripts/backup-db.sh) | [scripts/restore-db.sh](../../scripts/restore-db.sh) |
| **Windows PowerShell** | [scripts/backup-db.ps1](../../scripts/backup-db.ps1) | [scripts/restore-db.ps1](../../scripts/restore-db.ps1) |

Both script pairs automatically detect whether a direct PostgreSQL host (`PGHOST` + local `pg_dump`/`pg_restore`) is configured; otherwise they execute `pg_dump` / `pg_restore` inside the `drone_postgres` Docker container.

---

## 2. Running a Manual Backup

### Option A — PowerShell (Windows + Docker Compose)

```powershell
# Default output directory: ./backups/drone_delivery_db_YYYYMMDD_HHMMSS.dump
.\scripts\backup-db.ps1
```

### Option B — Bash (Linux/macOS/WSL)

```bash
chmod +x scripts/backup-db.sh scripts/restore-db.sh
./scripts/backup-db.sh
```

### Option C — Direct PostgreSQL Host (without Docker exec)

```bash
export PGHOST=localhost
export PGPORT=5433
export PGPASSWORD="<your-postgres-password>"
./scripts/backup-db.sh
```

---

## 3. Restoring & Verifying into a Test Database

Always verify backups by restoring into a **separate test database** (`drone_delivery_db_verify` by default) before overwriting production data.

### PowerShell

```powershell
.\scripts\restore-db.ps1 -BackupFile .\backups\drone_delivery_db_20261117_150000.dump -TargetDatabase drone_delivery_db_verify
```

### Bash

```bash
./scripts/restore-db.sh ./backups/drone_delivery_db_20261117_150000.dump drone_delivery_db_verify
```

### Post-Restore Data Verification

```bash
docker exec -it drone_postgres psql -U postgres -d drone_delivery_db_verify -c "\dt"
docker exec -it drone_postgres psql -U postgres -d drone_delivery_db_verify -c "SELECT count(*) FROM delivery_orders;"
```

---

## 4. Recommended Schedule & Retention

| Environment | Frequency | Retention Policy |
| --- | --- | --- |
| **Local / Staging** | Daily at `02:00 UTC` or before schema migrations (`dotnet ef database update`) | Keep last 7 daily dumps |
| **Production** | Daily full `pg_dump -Fc` at `02:00 UTC` + automated off-host copy | Keep 30 days (aligned with [LA-165 retention policy](../api/LA-165-retention-policy-proposal.md)) |

### Example Linux Cron Entry

```cron
0 2 * * * cd /opt/smartdronedelivery && BACKUP_DIR=/var/backups/smartdrone ./scripts/backup-db.sh >> /var/log/smartdrone-backup.log 2>&1
```

---

## 5. Security Notes

1. **Never commit `.dump` files or `.env` credentials** to Git (`backups/` and `.env` must remain gitignored).
2. Store off-site backup copies with server-side encryption (e.g., encrypted S3/MinIO bucket or encrypted volume).
3. Restrict filesystem permissions on backup directories (`chmod 700 /var/backups/smartdrone`).
4. Note on verification status: Script syntax and parameters are verified in the repository; live `pg_dump`/`pg_restore` execution requires a running Docker/PostgreSQL 16 instance (`docker compose up -d postgres`).
