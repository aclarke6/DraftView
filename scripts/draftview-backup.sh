#!/usr/bin/env bash
#
# Nightly pg_dump backup for the DraftView PostgreSQL database, with
# daily/weekly/monthly retention.
#
# Runs as the OS/DB user "postgres" over the local Unix socket, using
# peer authentication (pg_hba.conf: "local all all peer"). No password
# or PGPASSFILE is needed or used.
#
# Cron: 0 2 * * * postgres /usr/local/bin/draftview-backup.sh

set -euo pipefail

PGUSER="postgres"
PGDATABASE="draftview"

BACKUP_ROOT="/var/backups/draftview"
DAILY_DIR="$BACKUP_ROOT/daily"
WEEKLY_DIR="$BACKUP_ROOT/weekly"
MONTHLY_DIR="$BACKUP_ROOT/monthly"

DAILY_RETENTION=7
WEEKLY_RETENTION=4
MONTHLY_RETENTION=12

DATE_STAMP="$(date +%Y%m%d)"
DAY_OF_WEEK="$(date +%u)"   # 1=Mon ... 7=Sun
DAY_OF_MONTH="$(date +%d)"

FILENAME="draftview_${DATE_STAMP}.dump"
DAILY_PATH="${DAILY_DIR}/${FILENAME}"

GLOBALS_FILENAME="draftview_globals_${DATE_STAMP}.sql"
GLOBALS_DAILY_PATH="${DAILY_DIR}/${GLOBALS_FILENAME}"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"
}

prune_dir() {
    local dir="$1"
    local pattern="$2"
    local keep="$3"
    local count
    count=$(find "$dir" -maxdepth 1 -name "$pattern" -printf '.' | wc -c)
    if [ "$count" -le "$keep" ]; then
        return 0
    fi
    find "$dir" -maxdepth 1 -name "$pattern" -printf '%f\n' \
        | sort \
        | head -n "$((count - keep))" \
        | while read -r old_file; do
            log "Pruning ${dir}/${old_file}"
            rm -f "${dir:?}/${old_file}"
        done
}

log "Starting DraftView backup for ${DATE_STAMP}"

pg_dump -U "$PGUSER" -d "$PGDATABASE" -F c -f "$DAILY_PATH"
log "Daily database backup written to ${DAILY_PATH}"

# Roles/passwords/permissions aren't part of a single-database pg_dump.
# Captured separately so a from-scratch restore doesn't depend on the
# draftview role already existing on the target server.
pg_dumpall -U "$PGUSER" --globals-only -f "$GLOBALS_DAILY_PATH"
log "Daily globals backup written to ${GLOBALS_DAILY_PATH}"

if [ "$DAY_OF_WEEK" = "7" ]; then
    cp "$DAILY_PATH" "${WEEKLY_DIR}/${FILENAME}"
    cp "$GLOBALS_DAILY_PATH" "${WEEKLY_DIR}/${GLOBALS_FILENAME}"
    log "Promoted to weekly: ${WEEKLY_DIR}/${FILENAME} (+ globals)"
fi

if [ "$DAY_OF_MONTH" = "01" ]; then
    cp "$DAILY_PATH" "${MONTHLY_DIR}/${FILENAME}"
    cp "$GLOBALS_DAILY_PATH" "${MONTHLY_DIR}/${GLOBALS_FILENAME}"
    log "Promoted to monthly: ${MONTHLY_DIR}/${FILENAME} (+ globals)"
fi

prune_dir "$DAILY_DIR" 'draftview_globals_*.sql' "$DAILY_RETENTION"
prune_dir "$DAILY_DIR" 'draftview_*.dump' "$DAILY_RETENTION"
prune_dir "$WEEKLY_DIR" 'draftview_globals_*.sql' "$WEEKLY_RETENTION"
prune_dir "$WEEKLY_DIR" 'draftview_*.dump' "$WEEKLY_RETENTION"
prune_dir "$MONTHLY_DIR" 'draftview_globals_*.sql' "$MONTHLY_RETENTION"
prune_dir "$MONTHLY_DIR" 'draftview_*.dump' "$MONTHLY_RETENTION"

log "Backup complete."
