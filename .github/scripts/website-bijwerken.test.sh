#!/usr/bin/env bash
# Test voor website-bijwerken.sh (besluiten 274 en 275). Draait in de CI; lokaal: bash .github/scripts/website-bijwerken.test.sh
set -u

script="$(dirname "$0")/website-bijwerken.sh"
map="$(mktemp -d)"
trap 'rm -rf "$map"' EXIT
fouten=0

bevat() { # omschrijving, bestand, tekst
  if ! grep -qF -- "$3" "$2"; then
    echo "FOUT: $1: '$3' niet gevonden in $2"
    fouten=$((fouten + 1))
  fi
}

bevat_niet() { # omschrijving, bestand, tekst
  if grep -qF -- "$3" "$2"; then
    echo "FOUT: $1: '$3' staat nog in $2"
    fouten=$((fouten + 1))
  fi
}

faalt() { # omschrijving, site, versie
  if bash "$script" "$2" "$3" > /dev/null 2>&1; then
    echo "FOUT: $1: het script had moeten falen"
    fouten=$((fouten + 1))
  fi
}

maak_site() { # map, regeleinde
  mkdir -p "$1/btw-boeker"
  printf '<h3>TraceLab <span class="tag">v0.1b · beta</span></h3>%b<h3>BTW-Boeker <span class="tag">v0.16 · beta</span></h3>%b' "$2" "$2" > "$1/index.html"
  printf '<a class="btn primary" href="https://github.com/x/x.github.io/releases/download/btw-boeker/BtwBoeker.msi">Download</a>%b<a class="btn" href="https://github.com/x/x.github.io/releases/tag/btw-boeker">Notes</a>%b' "$2" "$2" > "$1/btw-boeker/index.html"
}

# Eerste keer: vaste link en kort label worden de volledige versie.
site="$map/eerste"
maak_site "$site" '\n'
bash "$script" "$site" 0.19.0 || { echo "FOUT: eerste keer faalde"; fouten=$((fouten + 1)); }
bevat "label" "$site/index.html" '<span class="tag">v0.19.0 · beta</span>'
bevat "andere app ongewijzigd" "$site/index.html" 'TraceLab <span class="tag">v0.1b · beta</span>'
bevat "downloadlink" "$site/btw-boeker/index.html" 'releases/download/btw-boeker/BtwBoeker-0.19.0.msi"'
bevat_niet "oude link" "$site/btw-boeker/index.html" 'btw-boeker/BtwBoeker.msi'
bevat "releaseinformatie ongewijzigd" "$site/btw-boeker/index.html" 'releases/tag/btw-boeker"'

# Volgende versie, ook een patch: de vorige versie wordt vervangen.
bash "$script" "$site" 0.19.1 || { echo "FOUT: patchversie faalde"; fouten=$((fouten + 1)); }
bevat "patch label" "$site/index.html" '<span class="tag">v0.19.1 · beta</span>'
bevat "patch link" "$site/btw-boeker/index.html" 'BtwBoeker-0.19.1.msi"'
bevat_niet "vorige link" "$site/btw-boeker/index.html" 'BtwBoeker-0.19.0.msi'

# Twee keer dezelfde versie verandert niets.
cp "$site/index.html" "$map/label-voor"
cp "$site/btw-boeker/index.html" "$map/link-voor"
bash "$script" "$site" 0.19.1 || { echo "FOUT: tweede keer faalde"; fouten=$((fouten + 1)); }
cmp -s "$site/index.html" "$map/label-voor" || { echo "FOUT: label veranderde bij dezelfde versie"; fouten=$((fouten + 1)); }
cmp -s "$site/btw-boeker/index.html" "$map/link-voor" || { echo "FOUT: link veranderde bij dezelfde versie"; fouten=$((fouten + 1)); }

# Regeleinden blijven zoals ze waren.
site="$map/crlf"
maak_site "$site" '\r\n'
bash "$script" "$site" 0.19.0 || { echo "FOUT: CRLF faalde"; fouten=$((fouten + 1)); }
if [ "$(grep -c $'\r$' "$site/index.html")" != "2" ]; then
  echo "FOUT: CRLF ging verloren"
  fouten=$((fouten + 1))
fi

# Label of knop niet gevonden: zichtbaar falen, en niets half wijzigen.
site="$map/geen-label"
maak_site "$site" '\n'
printf '<h3>Iets anders</h3>\n' > "$site/index.html"
faalt "geen label" "$site" 0.19.0
bevat "link niet half gewijzigd" "$site/btw-boeker/index.html" 'btw-boeker/BtwBoeker.msi'

site="$map/geen-knop"
maak_site "$site" '\n'
printf '<a href="elders">Download</a>\n' > "$site/btw-boeker/index.html"
faalt "geen knop" "$site" 0.19.0
bevat "label niet half gewijzigd" "$site/index.html" 'v0.16 · beta'

# Een ongeldige versie wordt geweigerd.
site="$map/versie"
maak_site "$site" '\n'
faalt "versie met v" "$site" v0.19.0
faalt "korte versie" "$site" 0.19
faalt "geen versie" "$site" ""
bevat "niets gewijzigd" "$site/index.html" 'v0.16 · beta'

if [ "$fouten" -ne 0 ]; then
  echo "$fouten fout(en)"
  exit 1
fi

echo "website-bijwerken: alle tests geslaagd"
