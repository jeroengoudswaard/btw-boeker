# BTW-Boeker: projectrichtlijnen

BTW-Boeker is een Windows-desktopapplicatie voor een eenvoudige financiële administratie met btw: facturen en bonnen invoeren, de boekhouding bijhouden en de btw-aangifte per maand voorbereiden. Het eerste gebruik is de verhuur van een tweede huisje op een Landal-park (huurafrekeningen, parkbijdragen, energie). BTW-Boeker is een afsplitsing van Verenigd (de ledenadministratie van een scoutingvereniging) en een eigen product met een eigen naam, database en installatiebestand.

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
| 1 | Product | BTW-Boeker, afsplitsing van Verenigd (2026-11-09). Eigen naam, repository (`jeroengoudswaard/btw-boeker`), database en installatiebestand. Verenigd-databases worden niet geopend; importeren uit Verenigd is alleen mogelijk na een apart ontwerp |
| 2 | Tech-stack | C# / .NET 10 + WPF met Fluent-thema, MVVM (CommunityToolkit.Mvvm), EF Core met versleutelde SQLite (SQLite3MC), xUnit en FlaUI voor tests; lagen Domain, Application, Infrastructure, App, bewaakt met architectuurtests |
| 3 | Gebruiker | Eén gebruiker op één pc (desktop-app). Geen webapp, geen server |
| 4 | Opslag | Versleuteld SQLite-bestand `.btwdb` met sleutelbestand `<naam>.btwdb.sleutel` ernaast en back-up `.btwbackup` (beide bestanden erin); rekeningschema-sjabloon `.btwsjabloon` |
| 5 | Naam in code | Namespaces, projecten en bestanden heten `BtwBoeker.*`; teksten in de UI "BTW-Boeker". Een architectuurtest (`ProductnaamTests`) bewaakt dat de oude naam en extensies niet terugkomen |
| 6 | Reikwijdte | Eén administratie (één object: het huisje); geen Vereniging/Stichting, geen leden, contributie, SEPA-incasso, begroting of meerjarenplan, geen e-mail en geen webapp. Verwijdering van die onderdelen volgt in stap 2 |
| 7 | Boekhouding | Dubbel boekhouden zoals in Verenigd (grootboek, journaalposten concept → geboekt, bank- en kasdagboeken, relaties, verkoopfacturen met PDF, bijlagen bij boekingen, balans, W&V); alleen geboekte posten tellen en een geboekte post is onveranderlijk |
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
| 19 | Website | De release-workflow publiceert alleen naar de website als de repository-variabele `WEBSITE_PUBLICEREN` op `true` staat. Er is nog geen BTW-Boeker-pagina; een eigen pagina op de website mag (besluit 24) |
| 20 | App-icoon | Groene tegel met wit huisje en "BTW" (aangeleverd door de gebruiker, 2026-11-10); hoeken transparant gemaakt. `BtwBoeker.ico` heeft 16–256 px; 16 en 24 px zonder de tekst (keuze A). Bron `assets/bron/btw-boeker-icoon-1024.png`, gemaakt met `assets/bron/maak-icoon.py`; bewaakt door `IcoonTests`. De Android-iconen van het oude product zijn verwijderd (besluit 31) |
| 21 | Rekeningschema | Het schema uit e-Boekhouden (2026-11-10). De gebruiker levert een XAF-export; die wordt alleen lokaal gelezen (map `prive/`, nooit in Git) |
| 22 | Inkomende stukken | De Landal-afrekening komt als PDF; daarnaast andere rekeningen, zoals voorschotnota's voor energie. De indeling volgt uit een voorbeeld-PDF van de gebruiker (lokaal, nooit in Git) |
| 23 | Privégebruik | Het huisje wordt ook privé gebruikt; dat staat apart vermeld op de nota's (aanvulling op besluit 15) |
| 24 | Websitepagina | Er mag een eigen pagina voor BTW-Boeker op de website komen; inhoud en moment volgen in stap 4 |
| 25 | XAF importeren | Een XAF-bestand (uit e-Boekhouden) wordt volledig overgenomen: rekeningschema, beginsaldi (`openingBalance`) en alle boekingen van het jaar (keuze 1C). Inlezen gebeurt XXE-veilig en wordt gecontroleerd tegen het XSD. Vervangt de Excel-overname, het historisch boekjaar en de ingebouwde standaardschema's |
| 26 | Eén administratie | Het datamodel per administratie blijft bestaan, maar de app toont één administratie, zonder wisselaar (keuze 2A). Een tweede object blijft later mogelijk |
| 27 | Jaarrekeningindeling | Blijft bestaan (keuze 3A) |
| 28 | Ontwerp stap 2 | Goedgekeurd (2026-11-10): begroting, meerjarenplan, Excel-overname, historisch boekjaar en de standaardschema's Vereniging/Stichting verdwijnen; startwizard Welkom → Database → Rekeningschema (XAF) → Bank → Klaar; navigatie Start · Boekhouding · Instellingen. Eerst verwijderen met de suite groen, daarna de XAF-import. `.gitignore` bevat de extensies `.btwdb`, `.btwdb.sleutel`, `.btwbackup` en `.xaf` (keuze 4A; een sjabloon `.btwsjabloon` bevat geen gegevens en mag wel in Git; geanonimiseerde XAF-testbestanden krijgen de extensie `.xml`) |
| 29 | Linux | Wens, geen prioriteit (2026-11-10). WPF draait alleen op Windows; de kern (Domain, Application, Infrastructure) is al platformonafhankelijk. Als het later toch moet: eerst een ontwerp voor Blazor Server lokaal (de app draait op de eigen pc, bediening in de browser; één gebruiker en het versleutelde bestand blijven), pas na stap 3 |
| 30 | Standaardnaam administratie | *Mijn administratie* (keuze A, 2026-11-10). Een bestaande database met de oude standaardnaam krijgt die naam via de migratie *StandaardnaamAdministratie*; een zelf gekozen naam blijft |
| 31 | Opruimen oude product | Alles van het oude product weg (2026-11-10): Android-iconen, `COPILOT.md`, Gmail-handleiding, SEPA-voorbeeld, webapp-job in de release-workflow, verwijzingen naar besluitnummers van het oude product in code en documentatie, scouting-namen in tests en de demodatabase. CHANGELOG begint bij de afsplitsing. `ProductnaamTests` bewaakt naam en domein; de mappen `prive/` en `voorbeelden/` leest geen test |

## Planning

1. **Hernoemen** van Verenigd naar BtwBoeker: gereed (2026-11-10), gebouwd en getest op Windows.
2. **Eén administratie** met een rekeningschema voor de verhuur; scouting- en niet-relevante onderdelen verwijderen. Ontwerp goedgekeurd (besluit 28). 2a verwijderen: gereed. 2b XAF importeren (besluit 25): wacht op een XAF-export van de gebruiker in `prive/`.
3. **Btw-integratie**, test-first: btw-code en prestatiedatum op journaalregels, opslag en migratie, btw-rekeningen en automatische boekingsregels, invoer in de posteditor, maandrapport en export.
4. Documentatie (README, CHANGELOG, installatie) en validatie op Windows.
5. (Later, geen prioriteit) Linux via Blazor Server lokaal (besluit 29): eerst een ontwerp en een schatting.

Antwoorden op de open vragen (2026-11-10): rekeningschema uit e-Boekhouden (besluit 21), Landal-afrekening als PDF plus andere nota's (besluit 22), ook privégebruik (besluit 23). Nog te leveren door de gebruiker: een XAF-export uit e-Boekhouden en een voorbeeld-PDF van de Landal-afrekening en een energienota, in `prive/`.

## Stand van zaken (bijwerken na elke sessie)

- **Stap 1 (hernoemen): gereed.** 273 bestanden aangepast.
- **Windows-test (2026-11-10, Windows 11, branch `hernoemen-btwboeker`):** build zonder waarschuwingen. Eerste run 584/586: `ProductnaamTests` vond (1) lokale `bin`/`obj`-restanten in mappen `Verenigd.*` van de oude checkout (verwijderd; geen fout in Git) en (2) de omgevingsvariabelen `VERENIGD_SCREENSHOTS` en `VERENIGD_WEBSITE_SCHERMAFBEELDINGEN`. Die heten nu `BTWBOEKER_*` (commit `2ba9443`). Daarna **586/586 geslaagd** (Domain 117, Application 195, Architectuur 30, Infrastructure 62, App 182), met `--blame-hang --blame-hang-timeout 4m`.
- **Eerste build op Windows (2026-11-09, branch `hernoemen-btwboeker`):** 0 waarschuwingen en 57 compilefouten in `BtwBoeker.Infrastructure.Tests` en `BtwBoeker.App.Tests`; de overige projecten bouwden.
  - Oorzaak: de zoek-en-vervangslag zette "Verenigd" ook in namen om naar "BTW-Boeker"; het koppelteken is ongeldig in een C#-namespace of -methodenaam. Het ging om `BTW-Boeker.Tests.Gedeeld` (4 bestanden) en de methode `Standaardpaden_zijn_gescheiden_van_BTW-Boeker`.
  - Opgelost: namespace `BtwBoeker.Tests.Gedeeld`; de paden-test heet nu `Standaardpaden_staan_in_de_eigen_btwboeker_map`, en zijn twee `DoesNotContain("BTW-Boeker")`-regels zijn vervallen (ze spraken zichzelf tegen). Nieuwe test in `ProductnaamTests` verbiedt een koppelteken in namespace-, using- en methoderegels.
  - Ook gecorrigeerd: de snelkoppeling in `installer/Package.wxs` wees naar `BtwBoeker.exe`, maar de assemblynaam van de app is `BTW-Boeker`, dus het uitvoerbestand is `BTW-Boeker.exe`.
  - Daarna gebouwd en getest op Windows (zie hierboven).
- **Stap 2a (verwijderen): gereed** (2026-11-10, branch `hernoemen-btwboeker`), in vier delen met elk een eigen commit:
  1. begroting en meerjarenplan (`aad1699`, migratie *VerwijderBegrotingEnMeerjarenplan*);
  2. Excel-overname en historisch boekjaar (`a18c78b`, migratie *VerwijderExcelHistorie*); de inrichtingsroute heet nu `Inrichtvoorstel`/`Inrichtrekening`;
  3. ingebouwde standaardschema's en speltakken (`2d81f15`); de wizard heet `SjablooninrichtingViewModel`;
  4. tweede administratie en wisselaar, startwizard Welkom → Database → Boekhouding → Bank → Klaar (migratie *EenAdministratie*: alleen een lege tweede administratie verdwijnt).
  - `ReikwijdteTests` bewaakt dat de onderdelen niet terugkomen; `MigratieTests` dat bestaande databases zonder verlies worden bijgewerkt.
  - Ook: app-icoon in programma, venster en taakbalk (`5f10014`; de test verbood het na het hernoemen) en in de titelbalk op 16 px (`13360d8`, keuze A).
  - **Windows-test (2026-11-10):** na elk deel de volledige suite; laatste stand **506/506** geslaagd, build zonder waarschuwingen. Na deel 4 faalden in twee runs wisselende UI-tests (database niet geopend, *Startkaarten* en *Boekingsregels* niet gevonden); los gedraaid slaagden ze (5 en 2 keer), en de volgende volledige run was groen. Bevestigd door de gebruiker: de pc was tijdens die UI-tests in gebruik. Tijdens UI-tests niets met muis of toetsenbord doen.
  - Standaardnaam: *Mijn administratie* (besluit 30).
- **Ontwikkelomgeving:** .NET SDK 10, Visual Studio of VS Code; de gebruiker draait Windows-tests zelf.
- **Bouwen en testen (Windows):** `dotnet build`, daarna `dotnet test --blame-hang --blame-hang-timeout 4m`; UI-tests met `--filter Categorie=UI`.
- De historie van Verenigd (besluiten 1–278) staat in de repository van Verenigd en in `CHANGELOG.md`.
