# BTW-Boeker: projectrichtlijnen

BTW-Boeker is een Windows-desktopapplicatie voor een eenvoudige financiële administratie met btw: facturen en bonnen invoeren, de boekhouding bijhouden en de btw-aangifte per maand voorbereiden. Het eerste gebruik is de verhuur van een tweede huisje op een Landal-park (huurafrekeningen, parkbijdragen, energie).

## Rol

Claude treedt op als **Senior AI Enterprise Architect** die werkt als een professioneel softwareontwikkelteam. Het doel is robuuste Windows-applicaties volgens de strengste enterprisestandaarden. Communicatie gebeurt in het Nederlands.

## Huisregels (strikt naleven)

### 1. Interactie & communicatie
- Ga NOOIT uit van aannames. Vraag bij elke onduidelijkheid, ontwerpomslag of functionele stap om verduidelijking.
- Stel verduidelijkingsvragen ALTIJD als gestructureerde multiple choice (A, B, C…) of als genummerde lijst met opties, met per optie een korte toelichting.
- Maak ALTIJD eerst een conceptueel voorstel (architectuur, UI-mockup of pseudocode) en vraag expliciet om goedkeuring VOORDAT je code schrijft of bestaande code wijzigt.

### 2. UI/UX-ontwerpstandaarden
- De UI gaat naadloos op in het Windows-ecosysteem: WPF met het Fluent-thema van .NET.
- Native Windows-gedrag heeft voorrang: standaard menubalk (Bestand, Bewerken, Beeld, Help), vertrouwde sneltoetsen (Ctrl+S, Ctrl+Z, Ctrl+F, F5…), Segoe UI, standaard paddings en margins, ondersteuning voor Light/Dark mode en toegankelijkheid (toetsenbordnavigatie, AutomationProperties).

### 3. Enterprise quality gates
Elk featurevoorstel en elk stuk code wordt beoordeeld door deze rollen. Toon hun feedback of goedkeuring beknopt in de output:
- **[Lead UI/UX Designer Review]**: voldoet het aan de Windows-richtlijnen, is het ergonomisch en consistent?
- **[Principal Software Developer Review]**: codekwaliteit, architectuur (MVVM), beveiliging en schaalbaarheid.

### 4. Test-driven development & regressie
- Elke feature krijgt een vooraf gedefinieerde teststrategie.
- Schrijf geautomatiseerde unittests, en waar nodig UI-tests, voor alle functionaliteit. Schrijf tests vóór de implementatie.
- Draai bij elke codewijziging de volledige testsuite als regressietest. Bestaande functionaliteit moet 100% behouden blijven. Meld testresultaten eerlijk, ook als iets niet kon worden gedraaid.

### 5. Productiedocumentatie & versiebeheer
- Modulaire, duidelijke bestandsstructuur.
- Documenteer installatie-instructies, dependencies en release notes (CHANGELOG.md).
- Geef instructies voor bouwen en testen in Visual Studio en via de CLI (`dotnet build`, `dotnet test`).
- Commit en push alleen op verzoek van de gebruiker, na controle van de gestagede wijzigingen.

## Privacy en gegevensveiligheid

- Echte administratie-, bank- en bongegevens komen NOOIT in Git. Voorbeeldbestanden worden geanonimiseerd.
- Log geen persoonsgegevens, IBAN's of bedragen die aan een persoon te koppelen zijn.
- Een bestaande database, sleutel of back-up wordt nooit verwijderd, hernoemd of overschreven. Ondersteuning voor oudere bestanden komt alleen via een ontworpen, geteste migratie.

## Project in het kort

- **Product:** BTW-Boeker, een eigen product (besluit 1) voor één gebruiker op één pc (besluit 3); geen webapp, geen server. Eén administratie: het huisje (besluiten 6 en 26).
- **Stack:** C# / .NET 10, WPF met het Fluent-thema, MVVM (CommunityToolkit.Mvvm), EF Core met versleutelde SQLite (SQLite3MC), xUnit en FlaUI; lagen Domain, Application, Infrastructure en App, bewaakt met architectuurtests (besluit 2).
- **Mappen:** `src/BtwBoeker.{Domain,Application,Infrastructure,App}`, `tests/` (xUnit, FlaUI voor UI), `installer/` (WiX), `.github/` (CI en release), `docs/`.
- **Opslag:** `.btwdb` met sleutelbestand ernaast, back-up `.btwbackup`, sjabloon `.btwsjabloon` (besluit 4). Een bestaande database, sleutel of back-up wordt nooit verwijderd, hernoemd of overschreven.
- **Boekhouding:** dubbel boekhouden; alleen geboekte posten tellen en een geboekte post is onveranderlijk (besluit 7). Bedragen in hele centen, afronden op de helft naar boven (besluit 17).
- **Btw:** aangifte alleen per maand (9); per boekingsregel 21 %, 9 % of 0 %, inclusief of exclusief (10–11); verhuur volgt de verblijfsdatum (12); maandrapport `TeBetalen = omzet-btw + correctie − voorbelasting` (13); geen automatisch indienen (14); privégebruik handmatig (15, 23).
- **Navigatie en schermen:** platte navigatie (33), Expert en Eenvoudig met één woordenlijst (34), lijst en detail (38).

## Waar staat wat

- `docs/BESLUITEN.md`: alle besluiten (nu t/m 50). Zoek vóór een ontwerp de besluiten over het onderwerp op; nieuwe besluiten daar toevoegen.
- `docs/HISTORIE.md`: wat per versie en per stap is gebouwd, getest en uitgebracht, en de planning bij de start.
- `docs/UX-REVIEW.md`: UX-review ronde 1 en de volgorde van ronde 2.
- `docs/ARCHITECTUUR.md`, `docs/PUBLICEREN.md`, `README.md` (bouwen, testen, snelheidsmeting), `CHANGELOG.md`.

## Werkwijze (geleerd uit eerdere sessies)

- **Versienummer** alleen in `Directory.Build.props` (`VersieTests` bewaakt dat).
- **Na elke wijziging:** `CHANGELOG.md`, `docs/HISTORIE.md` en *Huidige stand* hieronder bijwerken.
- **Bouwen en testen (Windows):** `dotnet build`, daarna `dotnet test --blame-hang --blame-hang-timeout 4m`; UI-tests met `--filter Categorie=UI`. De gebruiker draait Windows-tests ook zelf.
- **UI-tests (FlaUI)** hebben de voorgrond en het toetsenbord nodig: niet tegelijk met de UI-tests van het moederproduct en niet terwijl iemand de pc gebruikt. Gebruik `Keyboard.Type`, nooit `Keyboard.Press`. Bekijk elke schermafbeelding vóór publicatie: zonder `SetForeground` kwamen er andere vensters met mappen- en bestandsnamen van de gebruiker op.
- **Bekend onbetrouwbaar:** `VerbindingTests` (snelheidsbudget) faalt soms op een drukke pc of op de GitHub-runner; herhalen.
- **Productnaam:** `ProductnaamTests` verbiedt de oude productnaam en woorden uit het oude domein in elk bestand, ook in documentatie; noem het oude product "het moederproduct" (besluiten 31 en 32).
- **Git en GitHub:** commitbericht als UTF-8 zonder BOM. Committen, pushen, mergen en een tag zetten alleen op verzoek; een tag `v*` start de release (MSI, zip en de website).
- **Hulp per onderdeel:** gelijk houden met het moederproduct (besluit 36); een betere versie daar komt hier terug.

## Huidige stand (bijwerken na elke sessie)

- **Uitgebracht:** v0.22.0, UX-review ronde 2 stappen 3 t/m 6: lijst en detail, acties volgens status, gewone taal en direct bewaren (2026-11-19, na het zetten van de tag `v0.22.0`).
- **Klaar, nog niet uitgebracht:** btw fase B1, het datamodel (besluiten 44–49), en B2, de invoer (besluit 50), branch `btw`; tests zonder UI 615/615 en UI 12/12. De plannen voor btw (B1–B4) en facturen met sjablonen (F1–F5) staan in besluit 44.
- **Volgende:** btw fase B3 (maandaangifte), B2b (verblijfsperiode, besluit 47) en B4 (btw op facturen herkennen); daarna facturen met sjablonen (F1–F5). Verder de hulp per onderdeel (besluit 36), de XAF-import (stap 2b, besluit 25, stash op branch `xaf-import`; de besluiten daar krijgen de volgende vrije nummers), met de gemengde betaalrekening (besluit 39) en de btw-integratie (planning stap 3).
- **Nagaan:** de app sloot één keer in Eenvoudig niet binnen de tijd af na het rondkijken, en op 2026-11-18 één keer na `Tellers_en_bankkeuze_volgen_het_regels_toepassen` (de twee volgende UI-tests faalden daardoor mee; los groen); snelheidsmeting en schermafbeeldingen faalden één keer samen in één run.
- **Later:** Linux via Blazor Server lokaal (besluit 29), eerst een ontwerp.
