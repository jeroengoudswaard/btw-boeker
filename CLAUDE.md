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
| 19 | Website | De release-workflow publiceert alleen naar de website als de repository-variabele `WEBSITE_PUBLICEREN` op `true` staat. Er is nog geen BTW-Boeker-pagina |

## Planning

1. **Hernoemen** van Verenigd naar BtwBoeker: uitgevoerd (2026-11-09). Nog te doen: bouwen en volledige testsuite draaien (in de cloudsessie is NuGet geblokkeerd, dus op Windows).
2. **Eén administratie** met een rekeningschema voor de verhuur; scouting- en niet-relevante onderdelen verwijderen. Eerst een ontwerp ter goedkeuring.
3. **Btw-integratie**, test-first: btw-code en prestatiedatum op journaalregels, opslag en migratie, btw-rekeningen en automatische boekingsregels, invoer in de posteditor, maandrapport en export.
4. Documentatie (README, CHANGELOG, installatie) en validatie op Windows.

Nog open vragen voor stap 2 en 3: welk rekeningschema (voorstel van circa 25 rekeningen, of dat uit e-Boekhouden), de indeling van de Landal-afrekening en of het huisje ook privé wordt gebruikt.

## Stand van zaken (bijwerken na elke sessie)

- **Stap 1 (hernoemen): uitgevoerd, nog niet gebouwd of getest.** 273 bestanden aangepast; de naamtest `ProductnaamTests` is geschreven maar nog niet gedraaid. Er is niets gecommit.
- **Ontwikkelomgeving:** .NET SDK 10, Visual Studio of VS Code; de gebruiker draait Windows-tests zelf.
- **Bouwen en testen (Windows):** `dotnet build`, daarna `dotnet test --blame-hang --blame-hang-timeout 4m`; UI-tests met `--filter Categorie=UI`.
- De historie van Verenigd (besluiten 1–278) staat in de repository van Verenigd en in `CHANGELOG.md`.
