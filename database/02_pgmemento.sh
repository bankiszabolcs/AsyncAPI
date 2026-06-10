#!/bin/bash
set -e

# =============================================================================
# pgMemento telepítése és audit bekapcsolása
# A postgres konténer első indulásakor fut le, a 01_schema.sql UTÁN
# (a /docker-entrypoint-initdb.d scriptek ábécé sorrendben futnak).
# A pgmemento repo a database/ mappán belül van, így a konténerben a
# /docker-entrypoint-initdb.d/pgmemento útvonalon érhető el.
# =============================================================================

# Az INSTALL script \i direktívái relatív útvonalakat használnak,
# ezért a repo gyökeréből kell futtatni
cd /docker-entrypoint-initdb.d/pgmemento

echo "Installing pgMemento schema and functions..."
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f INSTALL_PGMEMENTO.sql

echo "Initializing pgMemento auditing on public schema..."
psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" <<'EOF'
SELECT pgmemento.init(
    schemaname           := 'public',
    log_old_data         := TRUE,   -- UPDATE/DELETE előtti értékek mentése (ez a history lényege)
    log_new_data         := FALSE,  -- az új érték az élő táblában megvan, nem duplikáljuk
    log_state            := FALSE,  -- a meglévő sorok kezdőállapotát nem logoljuk (üres DB-nél felesleges)
    trigger_create_table := TRUE    -- a később létrehozott táblákra is automatikusan rákerül az audit
);
EOF

echo "pgMemento installed and initialized."
