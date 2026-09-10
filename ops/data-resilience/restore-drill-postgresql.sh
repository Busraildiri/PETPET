#!/usr/bin/env bash
set -Eeuo pipefail

umask 077

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "Required environment variable is missing: ${name}" >&2
    exit 2
  fi
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command is not installed: $1" >&2
    exit 2
  fi
}

require_env PETWORK_RESTORE_DATABASE_URL
require_env RESTIC_REPOSITORY

if [[ "${PETWORK_RESTORE_CONFIRM:-}" != "NON_PRODUCTION" ]]; then
  echo "Set PETWORK_RESTORE_CONFIRM=NON_PRODUCTION after checking the target." >&2
  exit 2
fi

if [[ -z "${RESTIC_PASSWORD:-}" && -z "${RESTIC_PASSWORD_FILE:-}" && -z "${RESTIC_PASSWORD_COMMAND:-}" ]]; then
  echo "Set RESTIC_PASSWORD, RESTIC_PASSWORD_FILE, or RESTIC_PASSWORD_COMMAND." >&2
  exit 2
fi

for command_name in psql pg_restore restic find; do
  require_command "$command_name"
done

target_database="$(psql "$PETWORK_RESTORE_DATABASE_URL" -X -A -t -v ON_ERROR_STOP=1 -c 'select current_database()')"
if [[ ! "$target_database" =~ _restore_test$ ]]; then
  echo "Refusing restore: target database name must end with _restore_test (found: $target_database)." >&2
  exit 3
fi

user_table_count="$(psql "$PETWORK_RESTORE_DATABASE_URL" -X -A -t -v ON_ERROR_STOP=1 -c \
  "select count(*) from information_schema.tables where table_schema not in ('pg_catalog','information_schema')")"
if [[ "$user_table_count" != "0" ]]; then
  echo "Refusing restore: target database is not empty ($user_table_count user tables)." >&2
  exit 3
fi

tmp_root="${TMPDIR:-/tmp}"
staging="$(mktemp -d "${tmp_root%/}/petwork-restore-drill.XXXXXX")"

cleanup() {
  case "$staging" in
    "${tmp_root%/}"/petwork-restore-drill.*) rm -rf -- "$staging" ;;
    *) echo "Refusing to remove unexpected staging path: $staging" >&2 ;;
  esac
}
trap cleanup EXIT

snapshot="${PETWORK_RESTORE_SNAPSHOT:-latest}"
restic restore "$snapshot" --tag petwork-db --target "$staging"

mapfile -t dump_files < <(find "$staging" -type f -name petwork.dump -print)
if [[ "${#dump_files[@]}" != "1" ]]; then
  echo "Expected exactly one petwork.dump, found ${#dump_files[@]}." >&2
  exit 4
fi

dump_file="${dump_files[0]}"
pg_restore --list "$dump_file" >/dev/null
pg_restore \
  --dbname="$PETWORK_RESTORE_DATABASE_URL" \
  --no-owner \
  --no-privileges \
  --exit-on-error \
  "$dump_file"

restored_tables="$(psql "$PETWORK_RESTORE_DATABASE_URL" -X -A -t -v ON_ERROR_STOP=1 -c \
  "select count(*) from information_schema.tables where table_schema not in ('pg_catalog','information_schema')")"
if [[ "$restored_tables" == "0" ]]; then
  echo "Restore completed but no user tables were found." >&2
  exit 5
fi

echo "Database restore drill succeeded: $restored_tables user tables restored into $target_database."

if [[ -n "${PETWORK_RESTORE_MEDIA_DIR:-}" ]]; then
  if [[ "$PETWORK_RESTORE_MEDIA_DIR" != /* || "$PETWORK_RESTORE_MEDIA_DIR" != *restore-drill* ]]; then
    echo "PETWORK_RESTORE_MEDIA_DIR must be an absolute path containing 'restore-drill'." >&2
    exit 3
  fi
  if [[ -e "$PETWORK_RESTORE_MEDIA_DIR" && -n "$(find "$PETWORK_RESTORE_MEDIA_DIR" -mindepth 1 -print -quit)" ]]; then
    echo "Refusing media restore: destination is not empty." >&2
    exit 3
  fi

  mkdir -p "$PETWORK_RESTORE_MEDIA_DIR"
  restic restore "${PETWORK_RESTORE_MEDIA_SNAPSHOT:-latest}" \
    --tag petwork-media \
    --target "$PETWORK_RESTORE_MEDIA_DIR"
  echo "Media restore drill completed under $PETWORK_RESTORE_MEDIA_DIR."
fi
