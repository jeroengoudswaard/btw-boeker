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
| 28 | Ontwerp stap 2 | Goedgekeurd (2026-11-10): begroting, meerjarenplan, Excel-overname, historisch boekjaar en de ingebouwde standaardschema's verdwijnen; startwizard Welkom → Database → Rekeningschema (XAF) → Bank → Klaar; navigatie Start · Boekhouding · Instellingen. `.gitignore` bevat de extensies `.btwdb`, `.btwdb.sleutel`, `.btwbackup` en `.xaf` (keuze 4A; een sjabloon `.btwsjabloon` bevat geen gegevens en mag wel in Git; geanonimiseerde XAF-testbestanden krijgen de extensie `.xml`) |
| 29 | Linux | Wens, geen prioriteit (2026-11-10). WPF draait alleen op Windows; de kern (Domain, Application, Infrastructure) is al platformonafhankelijk. Als het later toch moet: eerst een ontwerp voor Blazor Server lokaal (de app draait op de eigen pc, bediening in de browser; één gebruiker en het versleutelde bestand blijven), pas na stap 3 |
| 30 | Standaardnaam administratie | *Mijn administratie* (keuze A, 2026-11-10). Een bestaande database met de oude standaardnaam krijgt die naam via de migratie *StandaardnaamAdministratie*; een zelf gekozen naam blijft |
| 31 | Geen resten van het oude product | Geen code, teksten, testgegevens of documentatie uit het product waarvan BTW-Boeker is afgeleid (2026-11-11, keuzes 1A 2B 3A 4A): testgegevens gaan over verhuur. De bestaande migraties blijven (keuze 2B), omdat bestaande databases ermee worden bijgewerkt. De mappen `prive/` en `voorbeelden/` leest geen test |

## Planning

1. **Eigen naam en opruimen**: gereed (2026-11-11).
2. **Eén administratie** met een rekeningschema voor de verhuur. 2a verwijderen: gereed. 2b XAF importeren (besluit 25): ontwerp goedgekeurd, branch `xaf-import`.
3. **Btw-integratie**, test-first: btw-code en prestatiedatum op journaalregels, opslag en migratie, btw-rekeningen en automatische boekingsregels, invoer in de posteditor, maandrapport en export.
4. Documentatie (README, CHANGELOG, installatie) en validatie op Windows.
5. (Later, geen prioriteit) Linux via Blazor Server lokaal (besluit 29): eerst een ontwerp en een schatting.

Snelheid (keuze C, 2026-11-11): een meting in de echte app met de tijden uit het logbestand, op een demodatabase met een paar jaar huurinkomsten.

## Stand van zaken (bijwerken na elke sessie)

- **v0.19.0** uitgebracht (2026-11-10): tag `v0.19.0`, MSI op de release `btw-boeker` van de website (met de hand geüpload, want `WEBSITE_TOKEN` ontbrak nog), websitepagina live.
- **v0.20.0** (2026-11-11): opruimen (PR #3) en snelheidsmeting (PR #4) samengevoegd in `main`; versie, CHANGELOG en deze stand op branch `release-0.20.0`. Nog te doen: `WEBSITE_TOKEN` zetten (gebruiker), release-PR samenvoegen en tag `v0.20.0` op `main`; de release-workflow publiceert dan naar de website.
- **Opruimen (2026-11-11, branch `opruimen`):** interne naam `AdministratieSoort.Standaard`; `TabbladViewModel` heet `PaginaViewModel`; navigatie, menu en instellingen zonder administratiekeuze; accentkleur per administratie weg (migratie *VerwijderAccentkleur*, getest in `MigratieTests`); een instellingenbestand van v0.19.0 blijft leesbaar (`JsonInstellingenOpslagTests`); testgegevens gaan over verhuur; de MSI-snelkoppeling had nog een omschrijving uit het oude domein.
- **Snelheid (2026-11-11, branch `snelheidsmeting`, keuze C):** meting in de echte app op `Meerjarendemo` (5 jaar, 359 posten, 357 banktransacties): openen 1,4 s, eerste keer Start 0,6 s, Boekhouding laden 60 ms, overzichten 6–25 ms, daarna vrijwel alles onder 5 ms, afsluiten 0,1 s. Geen knelpunt; gevonden en opgelost: alleen bekijken maakte boekingsregels "gewijzigd" (keuzelijst in een tabel schreef `null` terug), en het programma meldde versie 0.1.0. Uitvoeren: zie README, *Snelheidsmeting*.
- **XAF-import (stap 2b):** de besluiten over jaren, btw per regel, het koppelen van de bank en 2026 staan als stash op branch `xaf-import` en komen in deze tabel bij het hervatten.
- **Ontwikkelomgeving:** .NET SDK 10, Visual Studio of VS Code; de gebruiker draait Windows-tests zelf.
- **Bouwen en testen (Windows):** `dotnet build`, daarna `dotnet test --blame-hang --blame-hang-timeout 4m`; UI-tests met `--filter Categorie=UI`. Tijdens UI-tests niets met muis of toetsenbord doen: wisselende UI-fouten kwamen tot nu toe door gebruik van de pc.
