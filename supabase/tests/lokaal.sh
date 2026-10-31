#!/usr/bin/env bash
# Start een tijdelijke lokale PostgreSQL (zonder Docker) en draait de beveiligingstests.
# Gebruik: supabase/tests/lokaal.sh   (vereist de PostgreSQL-serverbestanden, bijv. pakket postgresql-16)
# In CI levert een service-container de database; daar zet de workflow PG_URL zelf.
set -euo pipefail
cd "$(dirname "$0")"

BIN=$(ls -d /usr/lib/postgresql/*/bin 2>/dev/null | sort -V | tail -1)
MAP=$(mktemp -d)
POORT=${POORT:-54329}
chown postgres "$MAP" 2>/dev/null || true
ALS=""
if [ "$(id -u)" = "0" ]; then ALS="runuser -u postgres --"; fi

$ALS "$BIN/initdb" -D "$MAP/data" -U postgres --auth=trust >/dev/null
$ALS "$BIN/pg_ctl" -D "$MAP/data" -o "-p $POORT -k $MAP" -l "$MAP/log" -w start >/dev/null
trap '$ALS "$BIN/pg_ctl" -D "$MAP/data" -m fast stop >/dev/null; rm -rf "$MAP"' EXIT

export PG_URL="postgresql://postgres@localhost:$POORT/postgres"
npx vitest run "$@"
