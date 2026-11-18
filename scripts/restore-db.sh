#!/usr/bin/env bash
# LA-179: PostgreSQL custom-format restore script for SmartDroneDelivery
set -euo pipefail
export LC_ALL=C

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

if [[ ! "${TARGET_DB}" =~ ^[a-zA-Z_][a-zA-Z0-9_]{0,62}$ ]]; then
  echo "[restore] ERROR: Target database must be an ASCII PostgreSQL identifier (maximum 63 bytes)." >&2
  exit 1
fi

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
  PSQL=(psql -X -v ON_ERROR_STOP=1 -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d postgres)
else
  echo "[restore] Ensuring target database '${TARGET_DB}' exists in Docker container '${CONTAINER_NAME}'"
  PSQL=(docker exec "${CONTAINER_NAME}" psql -X -v ON_ERROR_STOP=1 -U "${DB_USER}" -d postgres)
fi

# An unsuccessful lookup is an error, never evidence that a database is absent.
if ! exists=$("${PSQL[@]}" -tAc "SELECT 1 FROM pg_database WHERE datname='${TARGET_DB}'"); then
  echo "[restore] ERROR: Database lookup failed; restore refused." >&2
  exit 1
fi
if [[ "$exists" == 1 && "$ALLOW_OVERWRITE" != true ]]; then
  echo "[restore] ERROR: Refusing to overwrite existing database '${TARGET_DB}'. Set ALLOW_OVERWRITE=true explicitly." >&2
  exit 1
fi
if [[ -n "$exists" && "$exists" != 1 ]]; then
  echo "[restore] ERROR: Unexpected database lookup result; restore refused." >&2
  exit 1
fi
if [[ -z "$exists" ]]; then
  "${PSQL[@]}" -c "CREATE DATABASE \"${TARGET_DB}\";"
fi
if command -v pg_restore >/dev/null 2>&1 && [[ -n "${PGHOST:-}" ]]; then
  pg_restore -h "${PGHOST}" -p "${PGPORT:-5432}" -U "${DB_USER}" -d "${TARGET_DB}" --exit-on-error --clean --if-exists --no-owner "${BACKUP_FILE}"
else
  docker exec -i "${CONTAINER_NAME}" pg_restore -U "${DB_USER}" -d "${TARGET_DB}" --exit-on-error --clean --if-exists --no-owner < "${BACKUP_FILE}"
fi

echo "[restore] Completed restore into '${TARGET_DB}' from '${BACKUP_FILE}'"
