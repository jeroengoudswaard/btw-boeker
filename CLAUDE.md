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

## Vastgestelde besluiten

| # | Onderwerp | Besluit |
|---|-----------|---------|
| 1 | Product | BTW-Boeker is een eigen product (2026-11-09) met een eigen naam, repository (`jeroengoudswaard/btw-boeker`), database en installatiebestand |
| 2 | Tech-stack | C# / .NET 10 + WPF met Fluent-thema, MVVM (CommunityToolkit.Mvvm), EF Core met versleutelde SQLite (SQLite3MC), xUnit en FlaUI voor tests; lagen Domain, Application, Infrastructure, App, bewaakt met architectuurtests |
| 3 | Gebruiker | Eén gebruiker op één pc (desktop-app). Geen webapp, geen server |
| 4 | Opslag | Versleuteld SQLite-bestand `.btwdb` met sleutelbestand `<naam>.btwdb.sleutel` ernaast en back-up `.btwbackup` (beide bestanden erin); rekeningschema-sjabloon `.btwsjabloon` |
| 5 | Naam in code | Namespaces, projecten en bestanden heten `BtwBoeker.*`; teksten in de UI "BTW-Boeker". `ProductnaamTests` bewaakt in alle bestanden (ook in zichzelf) dat oude namen, extensies en woorden uit het oude domein niet terugkomen; alleen de migraties mogen de oude standaardnaam bevatten, omdat ze die moeten herkennen |
| 6 | Reikwijdte | Eén administratie (één object: het huisje); geen ledenadministratie, incasso, begroting of meerjarenplan, geen e-mail en geen webapp |
| 7 | Boekhouding | Dubbel boekhouden (grootboek, journaalposten concept → geboekt, bank- en kasdagboeken, relaties, verkoopfacturen met PDF, bijlagen bij boekingen, balans, W&V); alleen geboekte posten tellen en een geboekte post is onveranderlijk |
| 8 | Bank | CAMT.053 (Rabobank), elke transactie apart te boeken met boekingsregels |
| 9 | Aangifteperiode | Alleen per maand; geen kwartaal of jaar |
| 10 | Btw-tarieven | Per boekingsregel een keuze uit 21%, 9% en 0%, met de keuze of het ingevoerde bedrag inclusief of exclusief btw is; de app rekent grondslag, btw en totaal uit |
| 11 | 0% | Eén keuze "0%": het bedrag staat zonder btw in de boeking en telt in het rapport op een aparte regel mee (keuze A, 2026-11-09) |
| 12 | Tarief verhuur | Logies in een vakantiewoning: 9% voor verblijfsdata tot en met 31-12-2025, 21% vanaf 1-1-2026. Het tarief volgt de verblijfsdatum (prestatiedatum), niet de factuurdatum; ontvangen voorschotten voor een verblijf in 2026 vallen onder 21%. De app stelt het tarief voor, de gebruiker kan het overschrijven. Bij kosten stelt de app geen tarief voor |
| 13 | Rapport | Maandrapport met omzet en voorbelasting per tarief, plus privégebruik-correctie; `TeBetalen = omzet-btw + correctie − voorbelasting` |
| 14 | Aangifte | Geen automatisch indienen bij de Belastingdienst. De app levert een overzicht en export waarmee de gebruiker zelf aangifte doet (Mijn Belastingdienst Zakelijk, zonder certificaat) |
| 15 | Privégebruik | Correcties voor privégebruik worden handmatig ingevoerd |
| 16 | Buiten beeld | Geen buitenlandse platformkosten, geen verleggingsregeling en geen EU-transacties. Een all-in prijs wordt niet automatisch gesplitst |
| 17 | Bedragen | Altijd in hele centen (`Bedrag.Centen`); afronden op de helft naar boven (`MidpointRounding.AwayFromZero`) |
| 18 | Installatie | MSI (WiX 5), self-contained win-x64, `BtwBoeker-<versie>.msi`, eigen UpgradeCode; geen codesigning in de bèta |
| 19 | Website | De release-workflow publiceert naar de website als de repository-variabele `WEBSITE_PUBLICEREN` op `true` staat (aan sinds 2026-11-11) en het geheim `WEBSITE_TOKEN` bestaat (zie `docs/PUBLICEREN.md`) |
| 20 | App-icoon | Groene tegel met wit huisje en "BTW" (aangeleverd door de gebruiker, 2026-11-10); hoeken transparant gemaakt. `BtwBoeker.ico` heeft 16–256 px; 16 en 24 px zonder de tekst (keuze A). Bron `assets/bron/btw-boeker-icoon-1024.png`, gemaakt met `assets/bron/maak-icoon.py`; bewaakt door `IcoonTests` |
| 21 | Rekeningschema | Het schema uit e-Boekhouden (2026-11-10). De gebruiker levert een XAF-export; die wordt alleen lokaal gelezen (map `prive/`, nooit in Git) |
| 22 | Inkomende stukken | De Landal-afrekening komt als PDF; daarnaast andere rekeningen, zoals voorschotnota's voor energie. De indeling volgt uit een voorbeeld-PDF van de gebruiker (lokaal, nooit in Git) |
| 23 | Privégebruik | Het huisje wordt ook privé gebruikt; dat staat apart vermeld op de nota's (aanvulling op besluit 15) |
| 24 | Websitepagina | Eigen pagina `https://jeroengoudswaard.github.io/btw-boeker/` (live sinds 2026-11-10, v0.19.0), met een kaart op de startpagina; doelgroep: verhuur van een vakantiewoning |
| 25 | XAF importeren | Een XAF-bestand (uit e-Boekhouden) wordt volledig overgenomen: rekeningschema, beginsaldi (`openingBalance`) en alle boekingen van het jaar (keuze 1C). Inlezen gebeurt XXE-veilig en wordt gecontroleerd tegen het XSD. Vervangt de Excel-overname, het historisch boekjaar en de ingebouwde standaardschema's |
| 26 | Eén administratie | Het datamodel per administratie blijft bestaan (`AdministratieSoort.Standaard`), maar de app toont één administratie, zonder wisselaar (keuze 2A). Navigatie, menu en pagina's kennen geen administratie meer en er is geen accentkleur per administratie: de app volgt de accentkleur van Windows (2026-11-11, migratie *VerwijderAccentkleur*). Een tweede object blijft later mogelijk |
| 27 | Jaarrekeningindeling | Blijft bestaan (keuze 3A) |
| 28 | Ontwerp stap 2 | Goedgekeurd (2026-11-10): begroting, meerjarenplan, Excel-overname, historisch boekjaar en de ingebouwde standaardschema's verdwijnen; startwizard Welkom → Database → Rekeningschema (XAF) → Bank → Klaar; navigatie Start · Boekhouding · Instellingen (gewijzigd door besluit 33). `.gitignore` bevat de extensies `.btwdb`, `.btwdb.sleutel`, `.btwbackup` en `.xaf` (keuze 4A; een sjabloon `.btwsjabloon` bevat geen gegevens en mag wel in Git; geanonimiseerde XAF-testbestanden krijgen de extensie `.xml`) |
| 29 | Linux | Wens, geen prioriteit (2026-11-10). WPF draait alleen op Windows; de kern (Domain, Application, Infrastructure) is al platformonafhankelijk. Als het later toch moet: eerst een ontwerp voor Blazor Server lokaal (de app draait op de eigen pc, bediening in de browser; één gebruiker en het versleutelde bestand blijven), pas na stap 3 |
| 30 | Standaardnaam administratie | *Mijn administratie* (keuze A, 2026-11-10). Een bestaande database met de oude standaardnaam krijgt die naam via de migratie *StandaardnaamAdministratie*; een zelf gekozen naam blijft |
| 31 | Geen resten van het oude product | Geen code, teksten, testgegevens of documentatie uit het product waarvan BTW-Boeker is afgeleid (2026-11-11, keuzes 1A 2B 3A 4A): testgegevens gaan over verhuur. De bestaande migraties blijven (keuze 2B), omdat bestaande databases ermee worden bijgewerkt. De mappen `prive/` en `voorbeelden/` leest geen test |
| 32 | Overnemen uit het moederproduct (verduidelijkt besluit 31) | Generieke UI-onderdelen (meldingsbalk, vensterrand, focusrand, paginakop en dergelijke) mogen als code uit het moederproduct worden overgenomen, zonder namen, teksten of woorden uit het oude domein; `ProductnaamTests` blijft dat bewaken (keuze 1A, 2026-11-15) |
| 33 | Platte navigatie (wijzigt besluit 28) | Start · Journaal · Bank · Bonnen · Rapporten · Meer… · Instellingen, elk met een eigen kleur; na stap 3 komt *Btw-aangifte* direct onder Start (keuze 2A, 2026-11-15) |
| 34 | Weergave | Expert en Eenvoudig blijven, met één woordenlijst (keuze 3A, 2026-11-15) |
| 35 | Volgorde | Eerst UX-ronde 2 (stappen 0–6 in de sectie *UX-review*), daarna stap 2b (XAF-import), die op het nieuwe patroon bouwt (keuze 4A, 2026-11-15) |
| 36 | Hulp per onderdeel | Een hulppaneel rechts, te openen met F1 of een ⓘ in de paginakop. Het toont de handelingen van het huidige onderdeel stap voor stap, met *Aanwijzen* (een element even oplichten). De teksten staan als gegevens in Application, in Expert- en Eenvoudig-woorden. Een test bewaakt dat elk onderdeel hulp heeft en dat elk aangewezen element bestaat (keuze 1A). Bouwen na UX-stap 3 (lijst en detail; keuze 2A); daarna ook in het moederproduct (keuze 3A). 2026-11-16 |
| 38 | Lijst en detail (UX-stap 3) | Eén patroon voor Journaal, Bank, Bonnen, Boekingsregels, Relaties en Rekeningschema. De werkbalk staat op één regel: *Nieuw …*, zoeken, filters, en rechts het jaar (alleen Journaal, Bank, Rapporten en Grootboekkaart, en alleen bij meer dan één jaar). De lijst neemt de volle breedte. Het detail is een paneel dat rechts inschuift bij selectie of *Nieuw* en sluit met ✕ of Esc (keuze 1A). Opslaan blijft één opslag per pagina met Ctrl+S, maar de knop staat onderaan het detail (keuze 2A). *Regels toepassen* gaat vanzelf; de knop vervalt (keuze 3A). Alles in één keer (keuze 4B). 2026-11-16 |
| 37 | Zip zonder installatie (aanvulling op besluit 18) | Naast de MSI een zip (self-contained win-x64) die je uitpakt en start, overgenomen uit het moederproduct (2026-11-16) |

## Planning

1. **Eigen naam en opruimen**: gereed (2026-11-11).
2. **Eén administratie** met een rekeningschema voor de verhuur. 2a verwijderen: gereed. 2b XAF importeren (besluit 25): ontwerp goedgekeurd, branch `xaf-import`.
3. **Btw-integratie**, test-first: btw-code en prestatiedatum op journaalregels, opslag en migratie, btw-rekeningen en automatische boekingsregels, invoer in de posteditor, maandrapport en export.
4. Documentatie (README, CHANGELOG, installatie) en validatie op Windows.
5. (Later, geen prioriteit) Linux via Blazor Server lokaal (besluit 29): eerst een ontwerp en een schatting.

Snelheid (keuze C, 2026-11-11): een meting in de echte app met de tijden uit het logbestand, op een demodatabase met een paar jaar huurinkomsten.

## Stand van zaken (bijwerken na elke sessie)

- **v0.19.0** uitgebracht (2026-11-10): tag `v0.19.0`, MSI op de release `btw-boeker` van de website (met de hand geüpload, want `WEBSITE_TOKEN` ontbrak nog), websitepagina live.
- **v0.20.0** uitgebracht (2026-11-12): opruimen (PR #3), snelheidsmeting (PR #4), release (PR #5), tag `v0.20.0`. `WEBSITE_TOKEN` bestaat. De workflow bouwde de MSI, maar het publiceren faalde: `download-artifact` v8 zette de bestanden direct in `uit/`. MSI, versielabel, downloadknop en schermafbeeldingen zijn met de hand op de website gezet; de workflow is hersteld (vaste `name` en `path`) en wordt bij de volgende release getest.
- **v0.21.0** (2026-11-16): herstel van de release-workflow (PR #6), zip zonder installatie (PR #8, besluit 37), rustiger schil en platte navigatie (PR #7). Release via tag `v0.21.0`; de workflow publiceert MSI, zip, schermafbeeldingen, versielabel en de knoppen op de website. Nagaan: `VerbindingTests.Een_nieuwe_database_aanmaken_blijft_onder_het_budget` overschreed op de GitHub-runner één keer het budget (2839 ms, budget 2500 ms); na een herhaling groen.
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
- **Opruimen (2026-11-11, branch `opruimen`):** interne naam `AdministratieSoort.Standaard`; `TabbladViewModel` heet `PaginaViewModel`; navigatie, menu en instellingen zonder administratiekeuze; accentkleur per administratie weg (migratie *VerwijderAccentkleur*, getest in `MigratieTests`); een instellingenbestand van v0.19.0 blijft leesbaar (`JsonInstellingenOpslagTests`); testgegevens gaan over verhuur; de MSI-snelkoppeling had nog een omschrijving uit het oude domein.
- **Snelheid (2026-11-11, branch `snelheidsmeting`, keuze C):** meting in de echte app op `Meerjarendemo` (5 jaar, 359 posten, 357 banktransacties): openen 1,4 s, eerste keer Start 0,6 s, Boekhouding laden 60 ms, overzichten 6–25 ms, daarna vrijwel alles onder 5 ms, afsluiten 0,1 s. Geen knelpunt; gevonden en opgelost: alleen bekijken maakte boekingsregels "gewijzigd" (keuzelijst in een tabel schreef `null` terug), en het programma meldde versie 0.1.0. Uitvoeren: zie README, *Snelheidsmeting*.
- **XAF-import (stap 2b):** de besluiten over jaren, btw per regel, het koppelen van de bank en 2026 staan als stash op branch `xaf-import` en komen in deze tabel bij het hervatten. Ze waren genummerd 32–37; die nummers zijn intussen gebruikt (besluiten 32–35, UX-review), dus ze krijgen bij het hervatten de volgende vrije nummers.
- **Ontwikkelomgeving:** .NET SDK 10, Visual Studio of VS Code; de gebruiker draait Windows-tests zelf.
- **Bouwen en testen (Windows):** `dotnet build`, daarna `dotnet test --blame-hang --blame-hang-timeout 4m`; UI-tests met `--filter Categorie=UI`. Tijdens UI-tests niets met muis of toetsenbord doen: wisselende UI-fouten kwamen tot nu toe door gebruik van de pc.

## UX-review (ronde 1, 2026-11-15)

**Opdracht van de gebruiker.** Kijk als een topontwerper naar de hele app en noteer wat goed is en wat te ingewikkeld is voor een gemiddelde gebruiker. De doelen:
- geen bedieningselementen die op dat moment geen functie hebben;
- zoveel mogelijk harmonie;
- de eenvoudigste versie die dezelfde functionaliteit geeft.

Neem zoveel mogelijk over uit het moederproduct (het product waarvan BTW-Boeker is afgeleid). Daar zit al veel goed werk in.

In ronde 2 wordt de app vereenvoudigd en geharmoniseerd. Volgens de huisregels gebeurt dat stap voor stap, met per stap een ontwerpvoorstel ter goedkeuring.

**Bron en beperking.** Bekeken zijn:
- alle XAML-schermen;
- de schermafbeeldingen van v0.20.0 uit de demodatabase: Start, Journaal en Rapporten, licht, 1024×768.

Niet live bekeken: Bank, Postvak, de onderdelen onder *Meer…*, de startwizard, de dialogen, de weergave Eenvoudig en donker. Ronde 2 begint daarom met schermafbeeldingen van elk scherm. Punten met *(nagaan)* zijn uit de code afgeleid en nog niet op het scherm gezien.

### Wat we overnemen uit het moederproduct
Het moederproduct had dezelfde review (ronde 1, 2026-11-15) en heeft stap A al gebouwd en uitgebracht: een *rustiger schil*. BTW-Boeker is van een eerdere versie afgesplitst en heeft die verbeteringen nog niet.

Over te nemen:
- **Vensterrand.** Windows tekent de schaduw en de rand van het hoofdvenster: `GlassFrameThickness` is niet 0, en `VensterrandDwm` geeft afgeronde hoeken en een accentrand op Windows 11. Bewaakt met een architectuurtest.
  - BTW-Boeker heeft nog `GlassFrameThickness="0"`, dus het venster heeft geen schaduw en is slecht te zien boven andere vensters.
- **Meldingsbalk in plaats van de statusbalk.** Meldingen staan rechts in de paginakop met ✓/⚠/✕ op een gekleurde achtergrond. ✓ verdwijnt na 6 s; ⚠ en ✕ blijven tot ze gesloten worden of tot een andere pagina.
- **Paginatitel.** De titel is het onderdeel waar de gebruiker is (*Journaal*), met de module klein erboven als kruimel, plus één zin uitleg per onderdeel uit de woordenlijst.
- **Venstertitel.** 🔒 *bestand – BTW-Boeker*, zonder de naam van de administratie.
- **Opslaan alleen bij wijzigingen.** De knop staat er alleen als er iets op te slaan is, en is dan de hoofdactie. Ctrl+S werkt altijd.
- **Menubalk zonder *Modules* en *Extra*.** *Instellingen* (Ctrl+,) gaat naar *Bestand*.
- **Eén teller per regel**, een **focusrand** alleen bij toetsenbordgebruik, en een **onderdeelpaneel** zonder lege lijn.
- **De leidende principes** van die review (hieronder, aangevuld voor BTW-Boeker).
- **Lessen uit dat werk:**
  - schermafbeeldingen maken met een niet-gemaximaliseerd venster en een DPI-bewust testproces;
  - een melding niet dubbel tonen: onderaan de pagina én in de meldingsbalk.

We nemen de UI-onderdelen en het ontwerp over, nooit namen, teksten of woorden uit het oude domein; `ProductnaamTests` blijft dat bewaken. **Raakt besluit 31** ("geen code uit het product waarvan BTW-Boeker is afgeleid"). Daarvoor is eerst een besluit van de gebruiker nodig (open vraag 1).

### Leidende principes voor ronde 2
1. **Eén ding één keer.** Elk gegeven en elke actie staat op één plek.
2. **Alleen wat nu iets doet.** Geen uitgeschakelde knoppen, lege detailvlakken, lege werkbalken of tellers met 0.
3. **De lijst is het scherm.** De lijst gebruikt de volle breedte. Een detail verschijnt pas bij selectie of bij *Nieuw…*, en dan schuift de lijst in.
4. **Eén patroon per soort handeling.** "Nieuw …" staat linksboven; bevestigen (opslaan, boeken) staat rechtsonder in het vlak waar het bij hoort. Er is één manier van opslaan.
5. **De status bepaalt de acties.** Een concept toont *Boeken* en *Verwijderen*, een geboekte post *Tegenboeken*; nooit allebei. (In de posteditor gaat dit al goed.)
6. **Gewone taal in de interface.** Geen bestandsformaten (CAMT.053, XAF 3.2, .btwdb), sneltoetsen of interne begrippen in lopende tekst. Die horen in tooltips, menu's of de wizard.
7. **Alles leidt naar de maandaangifte.** De app bestaat voor de btw-aangifte per maand (besluiten 9 en 13). Start en navigatie maken zichtbaar wat er deze maand nog moet gebeuren voordat de aangifte klopt. Dat principe is nieuw voor BTW-Boeker.

### Wat goed is (behouden)
- **Het welkomstscherm.** Drie tegels, waarvan één gekleurd, met *Laatst geopend* eronder. Rustig en duidelijk.
- **Start.** De *Te doen*-kaarten verschijnen alleen als er iets te doen is, elk met één vervolgknop. Bij niets te doen staat er "✓ Alles is bij".
- **Het gedeelde onderdeel `LeegScherm`.** Een uitleg en één knop; het juiste patroon voor elke lege lijst.
- **Wat al alleen verschijnt als het nodig is:**
  - de boekjaarkeuze bij meer dan één jaar;
  - de bankrekeningkeuze bij meer dan één rekening;
  - het filter *Zonder bon* bij posten zonder bon;
  - *N concepten boeken* bij concepten;
  - het invoerveld *+ Rubriek* pas na een klik.

  Dit is precies principe 2.
- **Navigatie links.** Gekleurde pictogrammen, tellers, en de inrichting weggestopt onder *Meer…*.
- **Toegankelijkheid.** Labels met toegangstoetsen, AutomationIds en LiveSetting. Die mogen bij het vereenvoudigen niet verloren gaan.
- **De rapporten.** Een rustige tabel met afdrukken en exporteren bovenaan; de saldibalans is overzichtelijk.

### Bevindingen: schil (geldt voor elke pagina)
- **Naam en bestand staan er twee keer:** in de titelbalk (*Vakantiehuis De Voorbeeld – vakantiehuis.btwdb*) en in de statusbalk. Er is maar één administratie (besluit 26). → Venstertitel zoals in het moederproduct, statusbalk weg.
- **De statusbalk** toont vooral vaste gegevens: bestand, naam, boekjaar en "Gereed". → Meldingsbalk (zie hierboven). Het boekjaar komt in de paginakop.
- **De paginatitel is de module ("Boekhouding"), niet de plek ("Journaal").** → Titel en uitleg per onderdeel.
- **Opslaan staat altijd rechtsonder**, ook uitgeschakeld op Rapporten, waar niets op te slaan is. Daarnaast staat de tekst "Niet-opgeslagen wijzigingen". → Alleen tonen bij wijzigingen; de tekst vervalt.
- **De menubalk.** *Modules* herhaalt de navigatie; *Extra* bevat alleen *Instellingen*. → Beide weg. *Thema* gaat naar Instellingen; *Beeld* houdt *Weergave* en *Vorige/Volgende pagina*.
- **Twee tellers voor hetzelfde:** Boekhouding (1) en Journaal (1). → Bij een uitgeklapt onderdeel alleen de teller bij het subitem.
- **De focusrand.** Op Start staat een zwarte rechthoek om de hele navigatielijst, en het gekozen item heeft een gestippelde rand. → Een focusrand alleen bij toetsenbordgebruik, om de regel zelf.
- **Een lege werkbalk.** Boven het Journaal staat een lijn waar de verborgen boekjaarkeuze en de uitvoerknoppen horen. → Geen lijn zonder inhoud.
- **De navigatie heeft één niveau te veel.** Er is één administratie en vrijwel één module (Boekhouding). *Boekhouding* is dus alleen een map om Journaal, Bank, Bonnen en Rapporten.
  - → Plat maken: **Start · Journaal · Bank · Bonnen · Rapporten · Meer… · Instellingen**, elk met een eigen kleur.
  - Na stap 3 komt **Btw-aangifte** erbij, direct onder Start.
  - Raakt besluit 28 (navigatie Start · Boekhouding · Instellingen); open vraag 2.
- **Puntjes.** "…" en "..." staan door elkaar (*Afdrukken...* naast *Indeling bewerken…*). → Overal "…".
- **Sneltoetsen in lopende tekst.** Voorbeelden:
  - "(Ctrl+B)" op een startkaart;
  - "Maak een post (Ctrl+N)";
  - "Opslaan met Ctrl+S." onder Boekingsregels, Relaties en Rekeningschema.

  → Alleen in tooltips en menu's.
- **Toelichtingen onderaan pagina's** (Postvak, Boekingsregels, Relaties, Rekeningschema, Boekjaren). → In `LeegScherm` bij een lege lijst, anders een ⓘ-tooltip bij de kop.
- **Weergave Expert/Eenvoudig.** Twee woordenlijsten verdubbelen elke tekst en elke test. Met één gebruiker en één administratie is er misschien één taal genoeg. → Open vraag 3.

### Bevindingen per scherm
- **Welkom**
  - De ondertitel belooft "facturen", maar er is geen facturenonderdeel *(nagaan; besluit 7 noemt verkoopfacturen)*. → Weglaten, of de belofte waarmaken.
  - "Kies een bestaand .btwdb-bestand" en "Herstel een .btwbackup-back-up" noemen extensies. → "Open een bestaande administratie" en "Zet een back-up terug".
- **Startwizard**
  - Teksten uit het oude model: "database, **administraties**, boekhouding en bank". Ook de tooltip van *Startwizard…* in het menu.
  - Vijf knoppen onderaan: *Later verder*, *Overslaan*, *Vorige*, *Volgende* en *Voltooien*; op de laatste stap ook nog *Volgende* *(nagaan)*. → *Vorige* en één hoofdknop die *Volgende* of *Voltooien* heet; *Overslaan* als link bij een stap die overgeslagen mag worden. *Later verder* kan vervallen, omdat sluiten hetzelfde doet.
  - "Een bankafschrift in CAMT.053-formaat" → "Een bankafschrift van de Rabobank (download als CAMT.053)", met de uitleg in een ⓘ.
  - De stap *Boekhouding* biedt *Sjabloon…* en *Leeg beginnen*. Volgens besluit 28 wordt dat *Rekeningschema (XAF)*, en dat hoort bij stap 2b.
  - Op *Klaar* staat "via Instellingen of Boekhouding". Dat moet kloppen na de nieuwe navigatie.
- **Start**
  - De kerncijfertitel is afgekapt ("Resultaat 2026 t/…"). Er zijn vier kolommen voor één kaart. → Korte titel; het raster volgt het aantal kaarten.
  - *Inrichting afmaken* toont twee getallen: het tegeltje "3" en de tekst "2 van 5 stappen klaar". → Eén getal: "Nog 3 stappen".
  - Er is nog niets over btw. → Na stap 3 een hoofdkaart **"Btw <maand>: € … te betalen, aangeven vóór <datum>"**, met wat er nog openstaat: ongeboekte bankregels, bonnen en concepten van die maand (principe 7).
- **Journaal**
  - De lijst is 2/5 van de breedte, met een horizontale scrollbalk en drie zichtbare kolommen. Rechts staat een leeg vlak met "Selecteer een post…".
  - Het zoekveld heeft geen label of placeholder: een leeg vak onder *Nieuwe post*.
  - → De lijst over de volle breedte, een zoekveld met de placeholder "Zoeken", en de posteditor als paneel bij selectie of bij *Nieuwe post*.
  - De kolom *Nummer* (MEM-2026-0031) is voor een gebruiker niet belangrijk. → Datum, Omschrijving, Bedrag en Status. Het nummer staat in het detail.
- **Posteditor**
  - "Debet … · credit …" en de kolomkoppen Debet/Credit staan vast in de XAML, buiten de woordenlijst *(nagaan in Eenvoudig)*.
  - *Bijlage toevoegen* staat er twee keer: als link zonder bijlage en als knop met bijlage. → Eén knop, plus slepen.
  - Voor de btw (stap 3) komt er per regel een tarief bij (besluit 10). De editor wordt daardoor breder. → Bedrag en tarief invoeren; grondslag en btw berekent de app en toont ze klein onder de regel, niet in eigen kolommen.
- **Bank**
  - Twee werkbalkregels:
    - de rekening en het saldo;
    - *boeken*, *regels toepassen*, *inlezen* en het filter *Tonen*.
  - → Eén regel: het filter, *Inlezen* en het saldo. *N concepten boeken* blijft als hoofdactie zodra er concepten zijn. *Regels toepassen* gebeurt vanzelf na het wijzigen van een regel.
  - In het detail staan *Soort* (met code), *Toewijzen* en *Regel maken van deze boeking…*, ook bij een al verwerkte transactie *(nagaan)*. → Bij te verwerken: rekening, omschrijving en *Toewijzen*. *Regel maken* als link eronder. Bij verwerkt: alleen de verwijzing naar de post.
- **Postvak bonnen**
  - Vier knoppen per bon: *Koppelen*, *Tonen…*, *Datum en bedrag bewaren* en *Uit postvak verwijderen…*. → *Koppelen* als hoofdactie. Datum en bedrag worden vanzelf bewaard. Tonen via dubbelklik of een voorbeeld. Verwijderen in het contextmenu.
  - De vaste uitleg onderaan ("Sleep bonnen op deze lijst…") → in `LeegScherm` en als placeholder van de lijst.
  - Naam: in de navigatie *Postvak bonnen* → *Bonnen*.
- **Rapporten**
  - De ⚠-melding over concepten is afgekapt ("Boek ze in het Journaal om :"). → Meldingsbalk, of een eigen regel met een knop *Naar het journaal*.
  - De rapportkeuze is een keuzelijst met veel soorten. Na stap 3 komt de **maandaangifte** bovenaan, met de rubrieken zoals in Mijn Belastingdienst (besluit 14).
  - *Rekeningen tonen* en *Indeling bewerken…* verschijnen alleen bij de jaarrekening. Dat is goed.
- **Onder *Meer…*: Boekingsregels, Relaties, Rekeningschema, Grootboekkaart, Boekjaren**
  - *Boekingsregels*: een tabel met 11 bewerkbare kolommen, met keuzelijsten en vinkjes in elke rij. Dit is het drukste scherm van de app.
    - → Een lijst met de naam en "wanneer → rekening", en een detailpaneel voor de voorwaarden.
    - Het gewone pad blijft *Regel maken* vanuit Bank.
    - De toelichting noemt "Kruisposten" en "Ctrl+S".
  - *Relaties*: de vinkjes *Debiteur* en *Crediteur* zijn jargon. Bij verhuur zijn er maar een paar relaties (verhuurorganisatie, energie, water).
    - → *Klant* / *Leverancier*.
    - Is er geen facturenonderdeel, dan is het de vraag of Relaties een eigen onderdeel nodig heeft *(nagaan waarvoor het nu dient)*.
  - *Rekeningschema*: een tabel met 9 bewerkbare kolommen (Soort, Kant, Rol: jargon). Na de XAF-import (besluit 25) wordt het schema zelden gewijzigd.
    - → Alleen-lezen lijst (nummer, naam, saldo) met bewerken in een detailpaneel.
    - *Rol* en *Kant* alleen bij de rekeningen waar ze iets doen.
  - *Grootboekkaart* → geen eigen item, maar doorklikken vanuit Rapporten en Rekeningschema.
  - *Boekjaren*:
    - drie knoppen per rij;
    - *Volgend boekjaar openen* staat er altijd;
    - *Auditfile (XAF)…* staat er dubbel met *Bestand › Exporteren*.

    → Acties alleen bij het gekozen jaar, volgens de status; exporteren op één plek.
  - **Tooltip uit het oude domein:** *Bestand › Exporteren › Rekeningschema als sjabloon* zegt "om te delen met andere groepen". Herschrijven.
- **Instellingen**
  - Eén kaart (naam en drempel) met een eigen *Opslaan* en "Niet-opgeslagen wijzigingen". → Direct bewaren bij wijzigen, zoals in Windows-Instellingen. *Thema* en *Weergave* horen hier ook.
  - De *drempel "zonder bon"* hoort bij het filter in het Journaal, niet bij de naam van de administratie *(keuze in ronde 2)*.

### Stap 0: nieuw gevonden in de opnames (2026-11-15)
- **Start**
  - "Resultaat 2026 t/m vandaag € 0,00" staat naast 62 concepten; de gebruiker ziet zijn werk niet terug. → Ook de stand met concepten tonen.
  - "356 boekingen zonder bon" bij een drempel van € 10 is ruis en geen taak.
- **Journaal**
  - Bij een geboekte post staan de prullenbakjes per regel er nog, plus een lege kolom Omschrijving.
  - De kop zegt "Geboekt op 15-11-2026": dat is de boekdatum, niet de datum van de post.
  - Kolommen afgekapt ("Be", "Geboel").
- **Bank**
  - Er staat "Saldo bank … · grootboek (met concepten) … — verschil € 77.981,32": boekhoudtaal en een alarmerend getal, zonder uitleg of actie.
  - *Toewijzen* staat uitgeschakeld tot er een rekening is gekozen.
  - De uitleg onderaan het detail is afgekapt en heeft een eigen scrollbalk.
- **Donker:** de gekozen rij in een tabel is lichtcyaan met donkere tekst, en valt uit de toon.
- **Lege lijsten (Bonnen, Relaties)** tonen vijf à zes elementen tegelijk:
  - de knop twee keer (werkbalk en `LeegScherm`);
  - een teller "0 …";
  - een lege tabel met koppen;
  - "Kies een … in de lijst";
  - de uitleg onderaan, bij Relaties dezelfde tekst als in `LeegScherm`.
- **De boekjaarkeuze** staat boven elk onderdeel, ook waar ze niets doet: Boekingsregels, Relaties, Rekeningschema, Boekjaren.
- **Boekingsregels**
  - Koppen afgekapt ("Naan", "Omsc", "Trans", "Let o").
  - Meestal lege kolommen: Tegen-IBAN, bedragen, transactiecode, Let op.
- **Rekeningschema**
  - De kolommen Rubriek en Let op zijn leeg.
  - Saldo staat hoger in de cel dan de rest van de rij.
  - Bank (1100) toont € 0,00 terwijl de bank € 92.665,07 laat zien, zonder uitleg.
- **Grootboekkaart**
  - Opent zonder gekozen rekening: een lege tabel en "Beginsaldo · eindsaldo" zonder bedragen.
  - Boekjaar én Van/Tot en met: twee manieren om de periode te kiezen.
- **Boekjaren**
  - *Afsluiten…* staat ook bij 2026, met 62 concepten, terwijl 2025 nog open is: een knop die niet kan.
- **Instellingen**
  - De kaart staat 24 px links van de paginatitel.
- **Startwizard**
  - Er staan vijf knoppen onderaan, waarvan op stap 1 en stap 5 twee of drie uitgeschakeld.
  - Op stap 5 staat Welkom als niet gedaan (○).
- **Welkom**
  - Zonder database staan er een navigatiekolom met alleen Start, en een statusbalk "Geen database geopend · Gereed".
- **Eenvoudig**
  - "Postvak bonnen" is niet vertaald.
  - Debet en Credit staan vast in de posteditor; dat is nu bevestigd.

### Voorgestelde volgorde voor ronde 2 (elk een eigen voorstel ter goedkeuring)
0. **Schermafbeeldingen van elk scherm**, licht en donker, uit de demodatabase, met een niet-gemaximaliseerd venster. Daarbij een ontwerpcanvas met per scherm de bevindingen en een schets, zoals in het moederproduct.
1. **Schil, overgenomen uit het moederproduct:**
   - vensterrand;
   - meldingsbalk in plaats van de statusbalk;
   - paginatitel met kruimel;
   - venstertitel;
   - Opslaan alleen bij wijzigingen;
   - menubalk zonder *Modules* en *Extra*;
   - één teller, focusrand, geen lege lijn.

   Weinig risico en overal zichtbaar.
2. **Platte navigatie**: Start · Journaal · Bank · Bonnen · Rapporten · Meer… · Instellingen. Na open vraag 2.
3. **Lijst en detail**: één patroon voor Journaal, Bank, Bonnen, Boekingsregels, Relaties en Rekeningschema.
4. **Acties volgens status**: Bank, Bonnen en Boekjaren.
5. **Teksten**:
   - jargon, extensies en sneltoetsen uit de lopende tekst;
   - "…";
   - resten van het oude model (de startwizard, "groepen");
   - de belofte "facturen".
6. **Instellingen**: direct bewaren, met thema, weergave en drempel op de juiste plek.

Stap 3 van de planning (btw-integratie) bouwt daarna op dit patroon: de maandaangifte als hoofdkaart op Start en als eerste rapport.

### Open vragen aan de gebruiker (beantwoord 2026-11-15: 1A 2A 3A 4A, zie besluiten 32–35)
1. **Besluit 31 verduidelijken.** Mogen generieke UI-onderdelen (code) uit het moederproduct worden overgenomen, zonder namen en woorden uit het oude domein? Of alleen het ontwerp, zodat de code opnieuw wordt geschreven?
2. **Platte navigatie** in plaats van Start · Boekhouding · Instellingen (besluit 28)?
3. **Weergave Expert/Eenvoudig** houden, of één taal? En zo ja, welke?
4. **Volgorde.** Eerst ronde 2 (stappen 0–6), of eerst stap 2b (XAF-import) en daarna ronde 2?
