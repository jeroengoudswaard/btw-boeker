# Release notes

De opmaak volgt [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versienummering [SemVer](https://semver.org/lang/nl/).

## [Onuitgebracht]

### Opgelost
- **App-icoon** (besluit 20): het programma, het venster en de taakbalk toonden het standaardicoon van Windows; alleen de snelkoppeling uit de MSI had het eigen icoon. `BTW-Boeker.exe` heeft nu `BtwBoeker.ico` (groene tegel met huisje en BTW).

### Verwijderd
- **Resten van het oude product**: de Android-iconen, de Gmail-handleiding, een SEPA-voorbeeldbestand en het overdrachtsbestand `COPILOT.md`.
- **Begroting en meerjarenplan** (besluiten 6 en 28): de pagina *Begroting* (Ctrl+5), het subtabblad *Meerjarenplan*, het rapport *Begroting en werkelijk* en de afdruk voor de ALV. Ctrl+2 tot en met Ctrl+6 zijn vervallen; Ctrl+1 opent de boekhouding.
- **Kolom *begroot* in de jaarrekening**: de jaarrekening toont dit jaar en vorig jaar (besluit 27).
- **Start**: het resultaat tot vandaag staat zonder vergelijking met een begroting.
- **Excel-overname en historisch boekjaar** (besluiten 25 en 28): *Bestand › Importeren › Boekhouding uit Excel* en *Historie uit Excel*, de knop *Overnemen uit Excel* en de band *Historisch boekjaar*. Inrichten gaat met een sjabloon of leeg; XAF importeren volgt in stap 2b.
- **Ingebouwde standaardschema's en speltakken** (besluit 28): de knop *Standaardschema gebruiken* en de wizardstap *Speltakken*. De wizard voor een sjabloonbestand heeft nu drie stappen: Rekeningen, Beginsaldi, Klaar. Sjablonen lezen en schrijven (`.btwsjabloon`) blijft.
- **Tweede administratie en wisselaar** (besluit 26): de app toont één administratie; de keuzelijst *Administratie* in de navigatiebalk en de stap *Administraties* in de startwizard zijn weg. De startwizard: Welkom → Database → Boekhouding → Bank → Klaar.

### Gewijzigd
- **Database**: de migratie *VerwijderBegrotingEnMeerjarenplan* verwijdert de tabellen van begroting en meerjarenplan uit een bestaande database. Rekeningschema, boekingen en jaarrekeningindeling blijven ongewijzigd (getest in `MigratieTests`).
- **Database**: de migratie *EenAdministratie* verwijdert de tweede administratie alleen als die leeg is; met gegevens blijft hij bewaard maar verborgen. Een startwizard op de vervallen stap gaat verder bij *Boekhouding*.
- **Titelbalk**: linksboven het app-icoon op 16 px in plaats van het woord "BTW".
- **Standaardnaam** van de administratie: *Mijn administratie* (was *Vereniging*). De migratie *StandaardnaamAdministratie* past alleen de oude standaardnaam aan; een zelf gekozen naam blijft.
- **Teksten en documentatie** zonder verwijzingen naar het oude product (verenigingen, leden, contributie); de README en `docs/` beschrijven BTW-Boeker. De release-workflow bouwt geen webapp meer (die map bestond niet meer, waardoor een release zou mislukken).
- **Database**: de migratie *VerwijderExcelHistorie* verwijdert de historische regels en totalen en een historisch boekjaar (dat had geen journaalposten); gewone boekjaren blijven.

## [0.18.1] - 2026-11-09: Afsplitsing

BTW-Boeker is afgesplitst van een bestaande desktopapplicatie voor ledenadministratie (versie 0.18.1) en gaat verder als eigen product met een eigen naam, database (`.btwdb`), back-up (`.btwbackup`) en installatiebestand. De versienummering loopt door. De geschiedenis van vóór de afsplitsing staat in de repository van dat product.
