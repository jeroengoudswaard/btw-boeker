#!/usr/bin/env bash
# Zet de nieuwe versie op de website: het label op de startpagina en de downloadknop op de pagina van
# BTW-Boeker. Gebruik: website-bijwerken.sh <map van de websiterepository> <versie, bijv. 0.19.0>
# Vindt het label of de knop niet, dan faalt het zichtbaar en wijzigt het niets, zodat de site niet ongemerkt veroudert.
set -euo pipefail

site="${1:?map van de websiterepository ontbreekt}"
versie="${2:-}"

if [[ ! "$versie" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "::error::Ongeldige versie '$versie' (verwacht x.y.z)"
  exit 1
fi

startpagina="$site/index.html"
pagina="$site/btw-boeker/index.html"
label='(<h3>BTW-Boeker <span class="tag">)v[0-9][0-9.]*'
knop='(releases/download/btw-boeker/)BtwBoeker(-[0-9.]+)?\.msi'

grep -Eq "$label" "$startpagina" || { echo "::error::Versielabel van BTW-Boeker niet gevonden in $startpagina"; exit 1; }
grep -Eq "$knop" "$pagina" || { echo "::error::Downloadknop van BTW-Boeker niet gevonden in $pagina"; exit 1; }

sed -E -i "s#$label#\1v$versie#" "$startpagina"
sed -E -i "s#$knop#\1BtwBoeker-$versie.msi#g" "$pagina"
