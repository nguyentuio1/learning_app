#!/usr/bin/env bash
# All database commands are stubs; no Docker daemon or database is contacted.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
FIXTURE_DIR="$(mktemp -d)"
trap 'rm -f -- "$FIXTURE_DIR/stub" "$FIXTURE_DIR/docker" "$FIXTURE_DIR/psql" "$FIXTURE_DIR/createdb" "$FIXTURE_DIR/pg_restore" "$FIXTURE_DIR/backup.dump" "$FIXTURE_DIR/output" "$FIXTURE_DIR/invocations"; rmdir -- "$FIXTURE_DIR"' EXIT
touch "$FIXTURE_DIR/backup.dump"
cat > "$FIXTURE_DIR/stub" <<'STUB'
#!/usr/bin/env bash
printf '%s %s\n' "${0##*/}" "$*" >> "$RESTORE_TEST_LOG"
if [[ "$*" == *"SELECT 1 FROM pg_database"* ]]; then
  [[ "$RESTORE_TEST_CASE" != lookup-error ]] || exit 2
  [[ "$RESTORE_TEST_CASE" == missing ]] || echo 1
fi
if [[ "${0##*/}" == pg_restore || "$*" == *pg_restore* ]]; then
  [[ "$RESTORE_TEST_CASE" != restore-error ]] || exit 3
fi
exit 0
STUB
for command in docker psql createdb pg_restore; do cp "$FIXTURE_DIR/stub" "$FIXTURE_DIR/$command"; chmod +x "$FIXTURE_DIR/$command"; done
export PATH="$FIXTURE_DIR:$PATH" POSTGRES_DB=source_db
export RESTORE_TEST_LOG="$FIXTURE_DIR/invocations"
failures=0
for mode in direct docker; do
  export PGHOST=''
  [[ "$mode" != direct ]] || export PGHOST=stub-host
  for case_name in existing source invalid long missing allow lookup-error restore-error; do
    export RESTORE_TEST_CASE="$case_name" ALLOW_OVERWRITE=false
    target=verify_db
    [[ "$case_name" != source ]] || target=source_db
    [[ "$case_name" != invalid ]] || target="verify'; DROP DATABASE source_db;--"
    [[ "$case_name" != long ]] || target="$(printf 'a%.0s' {1..64})"
    [[ "$case_name" != allow && "$case_name" != restore-error ]] || export ALLOW_OVERWRITE=true
    : > "$RESTORE_TEST_LOG"
    result=0
    bash "$SCRIPT_DIR/../restore-db.sh" "$FIXTURE_DIR/backup.dump" "$target" > "$FIXTURE_DIR/output" 2>&1 || result=$?
    bad=''
    case "$case_name" in
      missing|allow) [[ "$result" == 0 ]] || bad+=' expected success;';;
      *) [[ "$result" != 0 ]] || bad+=' expected refusal/failure;';;
    esac
    case "$case_name" in
      invalid|long|source) [[ ! -s "$RESTORE_TEST_LOG" ]] || bad+=' commands before validation;';;
      existing|lookup-error) ! grep -Eq 'createdb|pg_restore|docker cp' "$RESTORE_TEST_LOG" || bad+=' unsafe command after denial;';;
    esac
    if [[ "$case_name" == missing ]]; then grep -Eq 'createdb .*--maintenance-db=postgres verify_db' "$RESTORE_TEST_LOG" || bad+=' missing create;'; fi
    if [[ "$case_name" == allow ]]; then ! grep -q 'createdb' "$RESTORE_TEST_LOG" || bad+=' recreated existing database;'; fi
    case "$case_name" in
      missing|allow|restore-error) grep -Eq 'pg_restore .*--exit-on-error' "$RESTORE_TEST_LOG" || bad+=' restore must stop on SQL errors;';;
    esac
    if [[ -n "$bad" ]]; then echo "FAIL $mode/$case_name:$bad"; failures=$((failures+1)); else echo "PASS $mode/$case_name"; fi
  done
done
[[ "$failures" == 0 ]]
