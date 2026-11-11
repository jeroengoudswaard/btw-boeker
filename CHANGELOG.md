# Release notes

De opmaak volgt [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versienummering [SemVer](https://semver.org/lang/nl/).

## [Onuitgebracht]

### Opgelost
- **Afsluiten na alleen bekijken**: met een paar boekingsregels vroeg de app bij het sluiten of de wijzigingen in *Boekhouding* opgeslagen moesten worden, ook als er niets was gewijzigd. Een keuzelijst in de tabel maakte de rekening van een regel leeg zodra die in beeld kwam. De keuzelijsten in de tabellen (boekingsregels, rekeningschema, posteditor) kiezen nu uit een lijst op de rij zelf.
- **Versienummer**: het programma meldde zich in het logbestand en onder *Help › Over BTW-Boeker* als 0.1.0; het volgt nu de versie van de release.

### Toegevoegd
- **Snelheidsmeting** in de echte app op een demodatabase met vijf jaar verzonnen verhuur (zie de README). Eerste meting: database openen 1,4 s, eerste keer Start 0,6 s, Boekhouding laden 60 ms, overzichten 6–25 ms, daarna vrijwel alles onder 5 ms; afsluiten 0,1 s.

### Gewijzigd
- **Accentkleur**: de app volgt de accentkleur van Windows. De eigen accentkleur per administratie is weg, ook uit *Instellingen*; de migratie *VerwijderAccentkleur* haalt de kolom uit een bestaande database en laat naam en drempel staan.
- **Instellingen**: één kaart *Administratie* met de naam en de drempel voor *Zonder bon*.
- **Paginakop**: zonder het label met de naam van de administratie; die staat al in de titelbalk en de statusbalk.
- **Startmenu**: de snelkoppeling beschrijft BTW-Boeker nu als "Boekhouding en btw voor de verhuur van een vakantiewoning".

### Verwijderd
- **Resten van het oude product** in code, tests en documentatie: navigatie en menu kennen geen keuze per administratie meer, de testgegevens gaan over verhuur, en `ProductnaamTests` controleert nu ook zichzelf en de woorden uit het oude domein.

## [0.19.0] - 2026-11-10: Eerste versie van BTW-Boeker

### Opgelost
- **Start**: een kaart met werk (bijvoorbeeld "1 conceptpost te boeken") toonde de interne naam van de administratie; die regel is weg.
- **Schermafbeeldingen voor de website**: vaste venstergrootte 1440×900 op een groot scherm (gemaximaliseerd viel de rechterkant buiten de opname); de demodatabase heet `vakantiehuis.btwdb` en bevat geen resten van het oude product meer.
- **Release-script**: het script dat de downloadknop op de website bijwerkt, zocht naar `BTW-Boeker-<versie>.msi` in plaats van `BtwBoeker-<versie>.msi`; de test ervan draait nu ook in de CI.
- **App-icoon** (besluit 20): het programma, het venster en de taakbalk toonden het standaardicoon van Windows; alleen de snelkoppeling uit de MSI had het eigen icoon. `BTW-Boeker.exe` heeft nu `BtwBoeker.ico` (groene tegel met huisje en BTW).

### Verwijderd
- **Resten van het oude product**: de Android-iconen, de Gmail-handleiding, een SEPA-voorbeeldbestand en het overdrachtsbestand `COPILOT.md`.
- **Begroting en meerjarenplan** (besluiten 6 en 28): de pagina *Begroting* (Ctrl+5), het subtabblad *Meerjarenplan*, het rapport *Begroting en werkelijk* en de afdruk voor de ALV. Ctrl+2 tot en met Ctrl+6 zijn vervallen; Ctrl+1 opent de boekhouding.
- **Kolom *begroot* in de jaarrekening**: de jaarrekening toont dit jaar en vorig jaar (besluit 27).
- **Start**: het resultaat tot vandaag staat zonder vergelijking met een begroting.
- **Excel-overname en historisch boekjaar** (besluiten 25 en 28): *Bestand › Importeren › Boekhouding uit Excel* en *Historie uit Excel*, de knop *Overnemen uit Excel* en de band *Historisch boekjaar*. Inrichten gaat met een sjabloon of leeg; XAF importeren volgt in stap 2b.
- **Ingebouwde standaardschema's** (besluit 28): de knop *Standaardschema gebruiken* en de wizardstap voor de onderdelen van het oude product. De wizard voor een sjabloonbestand heeft nu drie stappen: Rekeningen, Beginsaldi, Klaar. Sjablonen lezen en schrijven (`.btwsjabloon`) blijft.
- **Tweede administratie en wisselaar** (besluit 26): de app toont één administratie; de keuzelijst *Administratie* in de navigatiebalk en de stap *Administraties* in de startwizard zijn weg. De startwizard: Welkom → Database → Boekhouding → Bank → Klaar.

### Gewijzigd
- **Database**: de migratie *VerwijderBegrotingEnMeerjarenplan* verwijdert de tabellen van begroting en meerjarenplan uit een bestaande database. Rekeningschema, boekingen en jaarrekeningindeling blijven ongewijzigd (getest in `MigratieTests`).
- **Database**: de migratie *EenAdministratie* verwijdert de tweede administratie alleen als die leeg is; met gegevens blijft hij bewaard maar verborgen. Een startwizard op de vervallen stap gaat verder bij *Boekhouding*.
- **Titelbalk**: linksboven het app-icoon op 16 px in plaats van het woord "BTW".
- **Standaardnaam** van de administratie: *Mijn administratie* (was de naam uit het oude product). De migratie *StandaardnaamAdministratie* past alleen de oude standaardnaam aan; een zelf gekozen naam blijft.
- **Teksten en documentatie** zonder verwijzingen naar het oude product (een ledenadministratie); de README en `docs/` beschrijven BTW-Boeker. De release-workflow bouwt geen webapp meer (die map bestond niet meer, waardoor een release zou mislukken).
- **Database**: de migratie *VerwijderExcelHistorie* verwijdert de historische regels en totalen en een historisch boekjaar (dat had geen journaalposten); gewone boekjaren blijven.

## [0.18.1] - 2026-11-09: Afsplitsing

BTW-Boeker is afgesplitst van een bestaande desktopapplicatie voor ledenadministratie (versie 0.18.1) en gaat verder als eigen product met een eigen naam, database (`.btwdb`), back-up (`.btwbackup`) en installatiebestand. De versienummering loopt door. De geschiedenis van vóór de afsplitsing staat in de repository van dat product.
