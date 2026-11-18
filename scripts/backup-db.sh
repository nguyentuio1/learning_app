#!/usr/bin/env bash
# LA-179: PostgreSQL custom-format backup script for SmartDroneDelivery
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-./backups}"
DB_NAME="${POSTGRES_DB:-drone_delivery_db}"
DB_USER="${POSTGRES_USER:-postgres}"
CONTAINER_NAME="${POSTGRES_CONTAINER:-drone_postgres}"
TIMESTAMP="$(date -u +%Y%m%d_%H%M%S)"
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}_${TIMESTAMP}.dump"

mkdir -p "${BACKUP_DIR}"

if command -v pg_dump >/dev/null 2>&1 && [[ -n "${PGHOST:-}" ]]; then
  echo "[backup] Dumping ${DB_NAME} from ${PGHOST}:${PGPORT:-5432} -> ${BACKUP_FILE}"
  pg_dump -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d "${DB_NAME}" -Fc -f "${BACKUP_FILE}"
else
  echo "[backup] Dumping ${DB_NAME} via Docker container '${CONTAINER_NAME}' (no TTY) -> ${BACKUP_FILE}"
  docker exec "${CONTAINER_NAME}" pg_dump -U "${DB_USER}" -d "${DB_NAME}" -Fc > "${BACKUP_FILE}"
fi

if [[ ! -s "${BACKUP_FILE}" ]]; then
  echo "[backup] ERROR: Backup file is empty: ${BACKUP_FILE}" >&2
  exit 1
fi

echo "[backup] Completed: ${BACKUP_FILE} ($(wc -c < "${BACKUP_FILE}") bytes)"
