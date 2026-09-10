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

require_env PETWORK_BACKUP_DATABASE_URL
require_env RESTIC_REPOSITORY

if [[ -z "${RESTIC_PASSWORD:-}" && -z "${RESTIC_PASSWORD_FILE:-}" && -z "${RESTIC_PASSWORD_COMMAND:-}" ]]; then
  echo "Set RESTIC_PASSWORD, RESTIC_PASSWORD_FILE, or RESTIC_PASSWORD_COMMAND." >&2
  exit 2
fi

for command_name in pg_dump pg_restore restic sha256sum hostname; do
  require_command "$command_name"
done

daily="${PETWORK_BACKUP_KEEP_DAILY:-30}"
weekly="${PETWORK_BACKUP_KEEP_WEEKLY:-12}"
monthly="${PETWORK_BACKUP_KEEP_MONTHLY:-12}"

for value in "$daily" "$weekly" "$monthly"; do
  if [[ ! "$value" =~ ^[1-9][0-9]*$ ]]; then
    echo "Backup retention values must be positive integers." >&2
    exit 2
  fi
done

tmp_root="${TMPDIR:-/tmp}"
staging="$(mktemp -d "${tmp_root%/}/petwork-backup.XXXXXX")"

cleanup() {
  case "$staging" in
    "${tmp_root%/}"/petwork-backup.*) rm -rf -- "$staging" ;;
    *) echo "Refusing to remove unexpected staging path: $staging" >&2 ;;
  esac
}
trap cleanup EXIT

mkdir -p "$staging/database" "$staging/metadata"
dump_file="$staging/database/petwork.dump"
created_at="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
backup_host="$(hostname)"

echo "Creating PostgreSQL backup at ${created_at}."
pg_dump \
  --dbname="$PETWORK_BACKUP_DATABASE_URL" \
  --format=custom \
  --compress=9 \
  --no-owner \
  --no-privileges \
  --file="$dump_file"

# A truncated or structurally invalid custom dump must never be committed.
pg_restore --list "$dump_file" >/dev/null
sha256sum "$dump_file" >"$staging/metadata/SHA256SUMS"

{
  echo "created_at_utc=$created_at"
  echo "source_host=$backup_host"
  echo "database_format=postgresql-custom"
  echo "media_included=$([[ -n "${PETWORK_MEDIA_PATH:-}" ]] && echo true || echo false)"
} >"$staging/metadata/backup.properties"

restic backup \
  "$staging/database" \
  "$staging/metadata" \
  --host "$backup_host" \
  --tag petwork-db

if [[ -n "${PETWORK_MEDIA_PATH:-}" ]]; then
  if [[ "$PETWORK_MEDIA_PATH" != /* || "$PETWORK_MEDIA_PATH" == "/" || ! -d "$PETWORK_MEDIA_PATH" ]]; then
    echo "PETWORK_MEDIA_PATH must be an existing absolute directory other than /." >&2
    exit 2
  fi

  restic backup \
    "$PETWORK_MEDIA_PATH" \
    --host "$backup_host" \
    --tag petwork-media
fi

# Keep recent recovery points plus increasingly sparse long-term history.
restic forget \
  --host "$backup_host" \
  --tag petwork-db \
  --keep-daily "$daily" \
  --keep-weekly "$weekly" \
  --keep-monthly "$monthly" \
  --prune

if [[ -n "${PETWORK_MEDIA_PATH:-}" ]]; then
  restic forget \
    --host "$backup_host" \
    --tag petwork-media \
    --keep-daily "$daily" \
    --keep-weekly "$weekly" \
    --keep-monthly "$monthly" \
    --prune
fi

restic check
echo "Backup and repository integrity check completed."
