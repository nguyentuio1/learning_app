#!/usr/bin/env bash
# LA-179: PostgreSQL custom-format restore script for SmartDroneDelivery
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <backup_file.dump> [target_database]" >&2
  exit 1
fi

BACKUP_FILE="$1"
TARGET_DB="${2:-${POSTGRES_DB:-drone_delivery_db_verify}}"
DB_USER="${POSTGRES_USER:-postgres}"
CONTAINER_NAME="${POSTGRES_CONTAINER:-drone_postgres}"

if [[ ! -f "${BACKUP_FILE}" ]]; then
  echo "[restore] ERROR: Backup file not found: ${BACKUP_FILE}" >&2
  exit 1
fi

if command -v pg_restore >/dev/null 2>&1 && [[ -n "${PGHOST:-}" ]]; then
  echo "[restore] Ensuring target database '${TARGET_DB}' exists on ${PGHOST}:${PGPORT:-5432}"
  psql -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d postgres -tc "SELECT 1 FROM pg_database WHERE datname='${TARGET_DB}'" | grep -q 1 || \
    psql -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d postgres -c "CREATE DATABASE \"${TARGET_DB}\";"
  pg_restore -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d "${TARGET_DB}" --clean --if-exists --no-owner "${BACKUP_FILE}"
else
  echo "[restore] Ensuring target database '${TARGET_DB}' exists in Docker container '${CONTAINER_NAME}'"
  docker exec -i "${CONTAINER_NAME}" psql -U "${DB_USER}" -d postgres -tc "SELECT 1 FROM pg_database WHERE datname='${TARGET_DB}'" | grep -q 1 || \
    docker exec -i "${CONTAINER_NAME}" psql -U "${DB_USER}" -d postgres -c "CREATE DATABASE \"${TARGET_DB}\";"
  cat "${BACKUP_FILE}" | docker exec -i "${CONTAINER_NAME}" pg_restore -U "${DB_USER}" -d "${TARGET_DB}" --clean --if-exists --no-owner
fi

echo "[restore] Completed restore into '${TARGET_DB}' from '${BACKUP_FILE}'"
