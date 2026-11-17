#!/usr/bin/env bash
# LA-179: PostgreSQL custom-format restore script for SmartDroneDelivery
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <backup_file.dump> [target_database]" >&2
  exit 1
fi

BACKUP_FILE="$1"
SOURCE_DB="${POSTGRES_DB:-drone_delivery_db}"
# Never default to SOURCE_DB so accidental restores do not overwrite live data
TARGET_DB="${2:-drone_delivery_db_verify}"
ALLOW_OVERWRITE="${ALLOW_OVERWRITE:-false}"
DB_USER="${POSTGRES_USER:-postgres}"
CONTAINER_NAME="${POSTGRES_CONTAINER:-drone_postgres}"

if [[ ! -f "${BACKUP_FILE}" ]]; then
  echo "[restore] ERROR: Backup file not found: ${BACKUP_FILE}" >&2
  exit 1
fi

if [[ "${TARGET_DB}" == "${SOURCE_DB}" && "${ALLOW_OVERWRITE}" != "true" ]]; then
  echo "[restore] ERROR: Refusing to overwrite source database '${SOURCE_DB}'. Restore into a verification database or set ALLOW_OVERWRITE=true explicitly." >&2
  exit 1
fi

if command -v pg_restore >/dev/null 2>&1 && [[ -n "${PGHOST:-}" ]]; then
  echo "[restore] Ensuring target database '${TARGET_DB}' exists on ${PGHOST}:${PGPORT:-5432}"
  psql -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d postgres -tc "SELECT 1 FROM pg_database WHERE datname='${TARGET_DB}'" | grep -q 1 || \
    psql -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d postgres -c "CREATE DATABASE \"${TARGET_DB}\";"
  pg_restore -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d "${TARGET_DB}" --clean --if-exists --no-owner "${BACKUP_FILE}"
else
  echo "[restore] Ensuring target database '${TARGET_DB}' exists in Docker container '${CONTAINER_NAME}'"
  docker exec "${CONTAINER_NAME}" psql -U "${DB_USER}" -d postgres -tc "SELECT 1 FROM pg_database WHERE datname='${TARGET_DB}'" | grep -q 1 || \
    docker exec "${CONTAINER_NAME}" psql -U "${DB_USER}" -d postgres -c "CREATE DATABASE \"${TARGET_DB}\";"
  docker exec -i "${CONTAINER_NAME}" pg_restore -U "${DB_USER}" -d "${TARGET_DB}" --clean --if-exists --no-owner < "${BACKUP_FILE}"
fi

echo "[restore] Completed restore into '${TARGET_DB}' from '${BACKUP_FILE}'"
