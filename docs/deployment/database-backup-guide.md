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

Direct restores require the PostgreSQL client utilities `psql`, `createdb` and `pg_restore` on `PATH`. New targets are created by `createdb` using the `postgres` maintenance database. Passing the target as a separate argument lets PostgreSQL preserve mixed-case names and handle SQL keywords correctly, including when Windows PowerShell 5.1 invokes the native client.

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

Choose a fresh, unique test database for each verification. Both restore scripts reject **any existing target database**, including an existing verification database, unless the operator explicitly opts into overwriting it. The source database named by `POSTGRES_DB` (default `drone_delivery_db`) is also guarded, even if it does not currently exist. Restore targets must be ASCII identifiers beginning with a letter or underscore, followed by letters, digits or underscores, with at most 63 bytes. Invalid names are rejected before invoking PostgreSQL or Docker; quoted names, hyphens and SQL fragments are not accepted.

### PowerShell

```powershell
.\scripts\restore-db.ps1 -BackupFile .\backups\drone_delivery_db_20261117_150000.dump -TargetDatabase drone_delivery_db_verify
```

### Bash

```bash
./scripts/restore-db.sh ./backups/drone_delivery_db_20261117_150000.dump drone_delivery_db_verify
```

For an intentional destructive restore to an existing database, PowerShell requires `-AllowOverwrite`; Bash requires `ALLOW_OVERWRITE=true`. These options allow `pg_restore --clean` to replace existing objects and data, including the configured source if selected. Use them only after checking the target and retaining a separate backup:

```powershell
.\scripts\restore-db.ps1 -BackupFile .\backups\verified.dump -TargetDatabase disposable_verify_db -AllowOverwrite
```

```bash
ALLOW_OVERWRITE=true ./scripts/restore-db.sh ./backups/verified.dump disposable_verify_db
```

Lookup errors and unexpected lookup responses stop the restore; they do not trigger database creation. Both scripts use `pg_restore --exit-on-error` and report failure when restoration fails. This stops at the first SQL error but does not make the entire restore transactional; a failed target may contain partial results and should be inspected or recreated as a fresh disposable verification database.

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
| **Production** | Daily full `pg_dump -Fc` at `02:00 UTC` + automated off-host copy | Keep 30 days (aligned with [LA-165 retention policy](../decisions/assistant-retention-policy.md)) |

### Example Linux Cron Entry

```cron
0 2 * * * cd /opt/smartdronedelivery && BACKUP_DIR=/var/backups/smartdrone ./scripts/backup-db.sh >> /var/log/smartdrone-backup.log 2>&1
```

---

## 5. Security Notes

1. **Never commit `.dump` files or `.env` credentials** to Git (`backups/` and `.env` must remain gitignored).
2. Store off-site backup copies with server-side encryption (e.g., encrypted S3/MinIO bucket or encrypted volume).
3. Restrict filesystem permissions on backup directories (`chmod 700 /var/backups/smartdrone`).
4. Non-destructive guard regressions run with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/restore-db.Tests.ps1` and `bash scripts/tests/restore-db.Tests.sh`. They stub all database/Docker commands and exercise existing-target denial, source denial, invalid/oversized identifiers, fresh target creation, explicit overwrite, lookup errors and restore errors in both direct-host and Docker branches.
5. On Windows, run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/tests/restore-db.Native.Tests.ps1` too. It compiles native command stubs and exercises actual PowerShell argument marshalling for mixed-case and SQL-keyword database names, in both direct and Docker branches. It requires no database, Docker daemon or credentials.

## 6. Local verification evidence (2026-11-18)

Windows PowerShell 5.1 ran the actual backup and restore scripts against local PostgreSQL in `drone_postgres`, reading only the synthetic `drone_integration_20261118` source. The custom-format dump used a container file and `docker cp` for binary transfer; it was never passed through a PowerShell text pipeline. Restoration used a fresh `la46_verify_<GUID>_Upper` database and verified that its mixed-case name was preserved exactly. All 22 table counts and row contents, migration history, columns, constraints and indexes matched. PostgreSQL rewrites equivalent ARRAY casts when deparsing the existing `flight_missions` partial index after restore; the schema comparison normalized that expression only.

An additional restore attempt against that owned existing target was refused and preserved its sentinel table. A final read-only comparison found the source unchanged. The verification database was then dropped. Both shells also passed 16 command-stub guard checks each, and Windows PowerShell passed four compiled native argument checks. Bash was executed using Git Bash, while the live database round trip covered the PowerShell Docker path. Direct-host live restoration and a live Bash restoration were not exercised.

Local ignored evidence is retained under `artifacts/LA-46/` in the LA-46 integration worktree: `restore-verification.json`, `restore-live-powershell.txt`, source/restored schema comparison files and the custom-format dump. RED and GREEN command-stub outputs are retained there too. These results establish local synthetic backup restorability and restore safeguards. They do not establish production backup scheduling, off-host retention, a production recovery drill or deployment acceptance; the schedule above remains operational guidance.
