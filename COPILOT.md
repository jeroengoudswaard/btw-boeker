# BTW-Boeker — overdracht voor Copilot

Bijgewerkt: 2026-11-09

## Productrichting

BTW-Boeker is een Windows-desktopapp voor de btw- en boekhoudadministratie van één Nederlandse vakantiewoning. De productnaam is **BTW-Boeker**. De gebruiker heeft gekozen voor `.btwdb` als herkenbare database-extensie. Behoud de bestaande dubbelboekhoudkern en versleutelde SQLite-opslag; verwijder scouting-, leden-, contributie-, incasso-, e-mail- en webappfunctionaliteit die niet bij dit product hoort.

De goedgekeurde btw-scope is Nederlandse kortdurende vakantieverhuur, met maandrapportage. Btw-aangiften worden niet automatisch ingediend. Privégebruikcorrecties blijven handmatig. Buitenlandse platformkosten, verlegging en EU-transacties worden niet automatisch verwerkt. De toepasselijke logiesdatum bepaalt het tarief: 9% tot en met 2025 en 21% vanaf 1 januari 2026; een afzonderlijke faciliteit kan 9% hebben. Een all-inprijs wordt niet automatisch naar marktwaarde gesplitst.

## Huidige werkstaat

- Repository: `jeroengoudswaard/btw-boeker`; branch `main`; laatste basiscommit `38cacf6` (`v0.18.1`), op dat moment gelijk aan `origin/main`.
- De werkboom bevatte bij deze overdracht 341 gewijzigde, verwijderde of nieuwe paden (329 tracked bestanden gewijzigd en 12 untracked bestanden). Dit is omvangrijk werk uit eerdere sessies; controleer en behoud het zorgvuldig.
- De opschoning verwijdert scoutingledenadministratie, contributie/incasso, e-mail, facturen en de Supabase-webapp; de financiële boekhoudkern, bankimport, rapporten, relaties, bijlagen en versleutelde opslag blijven behouden.
- De app-, bedrijfs- en assemblyweergave en de app-datamap zijn gedeeltelijk hernoemd naar BTW-Boeker. De namespace-/project-/mapnamen `Verenigd.*` zijn nog niet volledig hernoemd; ook resteert minstens één productfoutmelding met de oude naam. Zoek alle code, tests, resources, installer-, workflow- en documentatiereferenties voordat je die opruimt. Bewaar geen oude naam in nieuwe productteksten, maar behoud waar nodig expliciete compatibiliteits-/migratie-informatie.
- De databasekeuzedialoog gebruikt `.btwdb`; de sleutel blijft naast de database als `.sleutel`. De back-up gebruikt `.btwbackup`. `DatabaseBeheer` weigert momenteel databases met de oude productidentiteit. **Verwijder, hernoem of overschrijf geen bestaande databases, sleutels of back-ups.** Werk eerst een veilige compatibiliteits- of migratiestrategie uit.
- De nieuwe btw-berekening staat in `src/Verenigd.Domain/Financien/Btw/BtwBerekening.cs`. Het maandrapport is een pure rekenfunctie in `src/Verenigd.Application/Btw/BtwRapportRekenaar.cs`. Beide hebben tests, maar btw is nog niet geïntegreerd met journaalregels, opslag, de post-editor of rapportnavigatie.
- De baseline EF-migratie `BtwBoekerBasis` is toegevoegd. Het model bevat nog de Vereniging/Stichting-structuur; de omzetting naar één instelbare objectadministratie en een verhuurgericht rekeningschema is niet af.
- Nieuwe relatiebestanden en een migratie zijn aanwezig in de werkboom. Beoordeel die samen met de andere wijzigingen; voeg geen echte persoonsgegevens, bankgegevens of wachtwoorden toe aan Git.

## Validatie in deze overdracht

- De gerichte test `Volgende_en_vorige_pagina_lopen_door_de_navigatie` slaagde (1/1).
- De volledige .NET-suite is op 2026-11-09 groen gedraaid met `dotnet test Verenigd.slnx --no-restore --configuration Debug --blame-hang --blame-hang-timeout 4m`: Domain 117, Application 195, Infrastructure 62, Architectuur 24 en App 182; **580/580 geslaagd**, geen buildwaarschuwingen getoond.
- Een eerdere App-run had één UI-test die de voorgrond niet kon krijgen omdat Microsoft Word voorgrond had. In de volledige run hierboven slaagden alle UI-tests.
- `git diff --check` slaagde. De scan van toegevoegde regels in de tracked diff vond geen herkenbare credentialpatronen. Scan ook alle untracked bestanden vóór commit; dit is geen garantie dat er geen privacygevoelige gegevens zijn.
- Bij relevante codewijzigingen opnieuw `dotnet build Verenigd.slnx --no-restore --configuration Debug` en `dotnet test Verenigd.slnx --no-restore --configuration Debug` draaien. Als een UI-test door de voorgrond faalt, leg dat apart vast en rapporteer het.

## Vervolgplan

1. **Controleer de volledige werkboom vóór commit.** Bekijk tracked en untracked wijzigingen, verwijderingen, gegenereerde bestanden en diff-checks. Scan nieuwe/toegevoegde inhoud op credentials, echte leden-/bankgegevens en andere niet te publiceren data. Verwijder niets wat niet aantoonbaar gegenereerd of buiten scope is.
2. **Maak de BTW-Boeker-identiteit consequent.** Inventariseer alle resterende `Verenigd`-referenties in code, tests, XAML/resources, project-/assemblynamen, oplossing, installer, workflows, website-instellingen en documentatie. Hernoem gecontroleerd en werk build-, migratie- en architectuurtests bij. De namespace- en projecthernoeming is nog niet uitgevoerd.
3. **Behoud databaseveiligheid.** Bevestig `.btwdb`, sleutelbestand, back-up/restore, filters, tests en herstelgedrag. Bestaande databasebestanden en back-ups blijven onaangeroerd; ondersteun oude bestanden alleen via een expliciet ontworpen, geteste migratie.
4. **Voltooi het één-objectmodel.** Vervang de Vereniging/Stichting-wisselaar en seeds door één instelbare objectadministratie; voeg een passend verhuurrekeningschema toe en verwijder overgebleven scoutingteksten en niet-relevante modules.
5. **Integreer btw testgericht.** Ontwerp en test btw-velden/codes en prestatiedatum voor journaalregels, opslag en EF-migratie, btw-rekeningen en automatische boekingsregels, invoer in de post-editor en maandrapportage/export. Bewaar handmatige privégebruikcorrecties en voorkom automatische aangifte.
6. **Werk documentatie en releasebestanden bij.** Houd README, changelog, installatie-/buildinstructies en toepasselijke architectuurdocumentatie in lijn met BTW-Boeker en `.btwdb`.
7. **Valideer op Windows.** Bouw de oplossing en draai alle .NET-tests; draai ook eventuele toepasselijke installer-, UI- en webapp-tests. Noteer afzonderlijk omgevingsfouten en los productregressies op.
8. **Commit en push pas na controle.** Controleer de exacte staged diff en de remote/branch. Commit alleen de bedoelde wijzigingen; push naar `origin/main` als de gebruiker daarom vraagt.

## Bronnen en randvoorwaarden

- Belastingdienst — btw op logies: https://www.belastingdienst.nl/wps/wcm/connect/nl/btw/content/btw-logies
- Belastingdienst — wijziging btw-tarief logies: https://www.belastingdienst.nl/wps/wcm/connect/bldcontentnl/berichten/nieuws/btw-logies
- Communiceer in het Nederlands. Gebruik TDD, de bestaande .NET/WPF/MVVM-patronen en de bestaande tests.
- Privacy: echte gegevens en credentials horen nooit in Git of logs. Gebruik uitsluitend verzonnen of geanonimiseerde testdata.
