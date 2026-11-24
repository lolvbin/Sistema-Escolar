#!/usr/bin/env bash
# Backup do PostgreSQL do Sistema Escolar (formato custom, restaurável com pg_restore).
# Uso:  PGHOST=... PGUSER=... PGDATABASE=... PGPASSWORD=... ./scripts/backup.sh [pasta] [dias_a_guardar]
# Agende no cron, por exemplo todo dia às 2h:  0 2 * * * /caminho/scripts/backup.sh /var/backups/escola 30
set -euo pipefail
DESTINO="${1:-./backups}"
DIAS="${2:-30}"
: "${PGDATABASE:?Defina PGDATABASE (e PGHOST, PGUSER, PGPASSWORD)}"
mkdir -p "$DESTINO"
ARQ="$DESTINO/escola_$(date +%Y%m%d_%H%M%S).dump"
trap 'rm -f "$ARQ"' ERR   # não deixa arquivo pela metade
pg_dump --format=custom --no-owner --file="$ARQ" "$PGDATABASE"
# confere se o arquivo é legível antes de apagar os antigos
pg_restore --list "$ARQ" > /dev/null
find "$DESTINO" -name 'escola_*.dump' -mtime +"$DIAS" -delete
echo "Backup ok: $ARQ ($(du -h "$ARQ" | cut -f1))"
