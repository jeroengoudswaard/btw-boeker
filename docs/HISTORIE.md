# BTW-Boeker: historie

Wat per versie en per stap is gebouwd, getest en uitgebracht, verplaatst uit CLAUDE.md (2026-11-17). Nieuw afgerond werk komt hier bovenaan bij; de huidige stand staat in CLAUDE.md.

## Stand per versie en stap

- **v0.19.0** uitgebracht (2026-11-10): tag `v0.19.0`, MSI op de release `btw-boeker` van de website (met de hand geüpload, want `WEBSITE_TOKEN` ontbrak nog), websitepagina live.
- **v0.20.0** uitgebracht (2026-11-12): opruimen (PR #3), snelheidsmeting (PR #4), release (PR #5), tag `v0.20.0`. `WEBSITE_TOKEN` bestaat. De workflow bouwde de MSI, maar het publiceren faalde: `download-artifact` v8 zette de bestanden direct in `uit/`. MSI, versielabel, downloadknop en schermafbeeldingen zijn met de hand op de website gezet; de workflow is hersteld (vaste `name` en `path`) en wordt bij de volgende release getest.
- **v0.21.0** (2026-11-16): herstel van de release-workflow (PR #6), zip zonder installatie (PR #8, besluit 37), rustiger schil en platte navigatie (PR #7). Release via tag `v0.21.0`; de workflow publiceert MSI, zip, schermafbeeldingen, versielabel en de knoppen op de website. Nagaan: `VerbindingTests.Een_nieuwe_database_aanmaken_blijft_onder_het_budget` overschreed op de GitHub-runner één keer het budget (2839 ms, budget 2500 ms); na een herhaling groen.
- **Btw fase B2: invoer (2026-11-20, besluit 50, keuzes 1A 2A 3B; branch `btw`).** Zie CHANGELOG.md en docs/ARCHITECTUUR.md, sectie *Btw invoeren*.
  - Tests eerst, in twee delen (posteditor, daarna de bank), steeds eerst rood en daarna groen. Nieuw: `BtwInvoerTests` (8), 2 domeintests in `BankTests` en 2 in `BankDienstTests`; de migratietest controleert ook de nieuwe kolom. De UI-test van een lege boekhouding kiest nu 21 % incl. in de posteditor en ziet de onderregel en de knop naar het rekeningschema.
  - Onderweg: de UI-test sloot de app eerst niet af, omdat de proefinvoer de nieuwe post "gewijzigd" maakte en afsluiten om opslaan vroeg; de test zet de invoer nu terug.
  - Windows (2026-11-20): zonder UI-tests 615/615 groen (Domain 90, Application 170, Architectuur 49, Infrastructure 67, App 239), UI-tests 12/12.
- **Btw fase B1: datamodel (2026-11-19, besluiten 44–49; branch `btw`).** Plannen voor de btw-integratie (B1–B4) en facturen met sjablonen (F1–F5) vastgelegd in besluit 44, met de keuzes in 45–48. Zie CHANGELOG.md en docs/ARCHITECTUUR.md, sectie *Btw per regel*.
  - Tests eerst: eerst rood (de build faalde), daarna groen. Nieuw: `BtwPerRegelTests` (domein, 7), rollen en concept met btw in `BoekhoudingDienstTests` (5), XAF met btw (1), opslag (1), migratie (1) en `RekeningRolTekstTests` (9).
  - Windows (2026-11-19): zonder UI-tests 603/603 groen (Domain 88, Application 168, Architectuur 49, Infrastructure 67, App 231), UI-tests 12/12.
  - De teksttest (besluit 41) ving "facturen" in de nieuwe toelichting bij de rollen; dat is "openstaande bedragen van klanten en leveranciers" geworden.
- **v0.22.0** (2026-11-19): UX-review ronde 2, stappen 3 t/m 6: lijst en detail (PR #10, besluit 38), acties volgens status (PR #11, besluit 40), teksten (PR #12, besluit 41) en direct bewaren op Instellingen (PR #13, besluit 42). Daarna, nog vóór de tag, het juiste bedrag van de bon uit het moederproduct (branch `bonbedrag`, besluit 43). Release via tag `v0.22.0`.
  - Bonbedrag: tests eerst (7 overgenomen en aangepast: `BonHerkenningTests` 4, `BijlagenTests` 2, `BijlagenViewModelTests` 1), eerst rood en daarna groen. Testgegevens zonder woorden uit het oude domein; de IBAN-regel uit het voorbeeld is weggelaten. Windows: zonder UI-tests 579/579 groen; UI-tests in de eerste run 11/12 (`Eenvoudig_verbergt_de_menubalk…`, het bekende punt onder *Nagaan*; los groen), de tweede volledige run 12/12.
- **UX-review ronde 1 (2026-11-15): vastgelegd** in de sectie *UX-review* hieronder (branch `ux-review`). Open vragen beantwoord (1A 2A 3A 4A → besluiten 32–35).
- **Ronde 2, stap 0: gereed (2026-11-15).**
  - Ontwerpcanvas (privé): https://claude.ai/artifact/XutibZfpyGC37Nw5NeWXiF. Het heeft 14 borden: overzicht, schets van de nieuwe schil met Journaal, schets van Start na stap 3, en 11 schermborden met opname, bevindingen en voorstel.
  - Nieuwe bevindingen staan in de sectie *UX-review*, onder *Stap 0*.
  - Opnames gemaakt met een tijdelijke UI-test `Schermafbeeldingen_voor_de_ontwerpreview`, niet gecommit:
    - alleen met `BTWBOEKER_REVIEW_SCHERMAFBEELDINGEN`;
    - op `Meerjarendemo`;
    - het testproces DPI-bewust (`SetProcessDpiAwarenessContext(-4)`);
    - het venster op 1920×1200 en vóór elke opname naar de voorgrond (`SetForeground`).
  - **Let op:** zonder `SetForeground` stonden er andere vensters op de opnames, met mappen- en bestandsnamen van de gebruiker. Ook daarna kwam er nog een venster van een andere app op het welkomstscherm. Bekijk elke opname vóór publicatie, en gebruik alleen opnames zonder andere vensters.
  - Eén keer sloot de app in Eenvoudig niet binnen de tijd af na het rondkijken; de tweede keer wel *(nagaan: mogelijk weer een vraag om op te slaan)*.
  - Volgende stap: stap 1 (schil), met een eigen voorstel ter goedkeuring.
- **Ronde 2, stappen 1 en 2: gebouwd (2026-11-16, keuze C: samen; branch `ux-review`).** Rustiger schil uit het moederproduct (besluit 32) en platte navigatie (besluit 33). Zie CHANGELOG.md en docs/ARCHITECTUUR.md, sectie *Rustiger schil en platte navigatie*.
  - Tests eerst: eerst rood (de build faalde), daarna groen. Nieuw: `MeldingsbalkTests` (7), `VensterrandTests` (5), `ShellSchilTests` (16), `SchilTests` (4, architectuur). Aangepast: navigatie, tellers, kleuren, weergave en UI-tests (titels, menu's, Opslaan en de meldingsbalk na Ctrl+S in Instellingen).
  - Windows (2026-11-16): de suite zonder UI-tests is groen (Domain 81, Application 156, Architectuur 40, Infrastructure 65, App 189). De UI-tests: 12/12 groen. De snelheidsmeting en de schermafbeeldingen slagen elk afzonderlijk. Samen in één run faalden ze één keer: de schermafbeeldingen zagen de database niet openen, en de meting vond *Grootboekkaart* niet. *(Nagaan; waarschijnlijk de volgorde of de pc.)*
  - Bewust nog niet gedaan: de boekjaarkeuze in de paginakop, een lege boekhouding die toch alle onderdelen toont, en de lege ruimte boven *Nieuwe post* waar de boekjaarkeuze staat. Die horen bij stap 3 (lijst en detail).
- **Ronde 2, stap 3: lijst en detail gebouwd (2026-11-17, besluit 38, keuzes 1A 2A 3A 4B; branch `lijst-en-detail`).** Zie CHANGELOG.md en docs/ARCHITECTUUR.md, sectie *Lijst en detail*.
  - Tests eerst: eerst rood (de build faalde), daarna groen. Nieuw: `LijstEnDetailTests` (13) en 2 architectuurtests in `SchilTests` (3 gevallen). Aangepast: één banktest en twee UI-tests, die op de vanzelf gekozen eerste regel, de knop *Regels toepassen* en een lege tabel rekenden.
  - Windows (2026-11-17): zonder UI-tests 547/547 groen (Domain 81, Application 156, Architectuur 43, Infrastructure 65, App 203), UI-tests 12/12. In de eerste UI-run faalden er twee met een UIA-time-out; los en in de volgende volledige run waren ze groen.
  - Afwijking van het ontwerp: de ⚠ bij een bankverschil staat in de pagina en niet in de meldingsbalk (die kan geen knop dragen).
- **Ronde 2, stap 6: instellingen (2026-11-18, besluit 42, keuzes 1A 2B 3A; branch `instellingen`, bouwt op `teksten`).** Zie CHANGELOG.md en docs/ARCHITECTUUR.md, sectie *Direct bewaren*.
  - Tests eerst: eerst rood (de build faalde), daarna groen. `InstellingenViewModelTests` herschreven (6). Nieuw: 3 shelltests over sluiten en verlaten, 1 over de keuzerondjes, 2 over de drempel bij het filter, en de architectuurtest `Instellingen_bewaart_direct_en_de_drempel_staat_bij_het_filter`. Vervangen: de drie tests die bij sluiten om opslaan vroegen. Aangepast: één UI-test, die nu het thema kiest in plaats van de drempel op te slaan.
  - Onderweg gevonden: een keuzerondje met een `Command` reageert alleen op een klik, niet op kiezen via UI Automation of een schermlezer. De rondjes zijn daarom gebonden aan `GekozenThema` en `GekozenWeergave`.
  - Windows (2026-11-18): zonder UI-tests 572/572 groen (Domain 81, Application 156, Architectuur 49, Infrastructure 65, App 221). UI-tests: in de eerste volledige run faalden er drie, doordat de app één keer niet afsloot en de volgende tests daarop vastliepen (bekend punt onder *Nagaan*). Los waren ze groen, en de tweede volledige run was 12/12.
- **Ronde 2, stap 5: teksten (2026-11-18, besluit 41, keuzes 1A 2A 3A; branch `teksten`).** Zie CHANGELOG.md.
  - Tests eerst: `TekstTests` (4) waren eerst rood met ruim 80 bevindingen, daarna groen. De test bewaakt de zichtbare XAML-teksten, de woordenlijst en de C#-teksten die de gebruiker ziet. Aangepast: één UI-test (titel van het wachtwoordvenster).
  - Windows (2026-11-18): zonder UI-tests 566/566 groen (Domain 81, Application 156, Architectuur 48, Infrastructure 65, App 216), UI-tests 12/12.
  - Stap 4 (PR #11) en stap 3 (PR #10) zijn op 2026-11-18 in `main` gemerged.
- **Ronde 2, stap 4: acties volgens status gebouwd (2026-11-18, besluit 40, keuzes 1A 2A 3A 4A; branch `acties-volgens-status`, bouwt op `lijst-en-detail`).** Zie CHANGELOG.md en docs/ARCHITECTUUR.md, sectie *Acties volgens status*.
  - Tests eerst: eerst rood (de build faalde), daarna groen. Nieuw: `ActiesVolgensStatusTests` (11), 2 startwizardtests en de architectuurtest `Acties_staan_in_het_detail_en_niet_in_de_rijen`; `Lijst_en_detail_zonder_leeg_vlak` telt nu 7 detailpanelen. Aangepast: één UI-test, die op een uitgeschakelde knop *Volgende* rekende.
  - Windows (2026-11-18): zonder UI-tests 562/562 groen (Domain 81, Application 156, Architectuur 44, Infrastructure 65, App 216), UI-tests 12/12.
- **Opruimen (2026-11-11, branch `opruimen`):** interne naam `AdministratieSoort.Standaard`; `TabbladViewModel` heet `PaginaViewModel`; navigatie, menu en instellingen zonder administratiekeuze; accentkleur per administratie weg (migratie *VerwijderAccentkleur*, getest in `MigratieTests`); een instellingenbestand van v0.19.0 blijft leesbaar (`JsonInstellingenOpslagTests`); testgegevens gaan over verhuur; de MSI-snelkoppeling had nog een omschrijving uit het oude domein.
- **Snelheid (2026-11-11, branch `snelheidsmeting`, keuze C):** meting in de echte app op `Meerjarendemo` (5 jaar, 359 posten, 357 banktransacties): openen 1,4 s, eerste keer Start 0,6 s, Boekhouding laden 60 ms, overzichten 6–25 ms, daarna vrijwel alles onder 5 ms, afsluiten 0,1 s. Geen knelpunt; gevonden en opgelost: alleen bekijken maakte boekingsregels "gewijzigd" (keuzelijst in een tabel schreef `null` terug), en het programma meldde versie 0.1.0. Uitvoeren: zie README, *Snelheidsmeting*.
- **XAF-import (stap 2b):** de besluiten over jaren, btw per regel, het koppelen van de bank en 2026 staan als stash op branch `xaf-import` en komen in deze tabel bij het hervatten. Ze waren genummerd 32–37; die nummers zijn intussen gebruikt (besluiten 32–35, UX-review), dus ze krijgen bij het hervatten de volgende vrije nummers.

## Planning bij de start

1. **Eigen naam en opruimen**: gereed (2026-11-11).
2. **Eén administratie** met een rekeningschema voor de verhuur. 2a verwijderen: gereed. 2b XAF importeren (besluit 25): ontwerp goedgekeurd, branch `xaf-import`.
3. **Btw-integratie**, test-first: btw-code en prestatiedatum op journaalregels, opslag en migratie, btw-rekeningen en automatische boekingsregels, invoer in de posteditor, maandrapport en export.
4. Documentatie (README, CHANGELOG, installatie) en validatie op Windows.
5. (Later, geen prioriteit) Linux via Blazor Server lokaal (besluit 29): eerst een ontwerp en een schatting.

Snelheid (keuze C, 2026-11-11): een meting in de echte app met de tijden uit het logbestand, op een demodatabase met een paar jaar huurinkomsten.
