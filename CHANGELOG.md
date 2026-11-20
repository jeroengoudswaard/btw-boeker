# Release notes

De opmaak volgt [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versienummering [SemVer](https://semver.org/lang/nl/).

## [Onuitgebracht]

### Toegevoegd
- **Btw in de boekhouding, fase 1: het datamodel** (besluiten 44–49).
  - Elke boekingsregel kan een btw-code hebben: omzet, voorbelasting of privégebruik, tegen 21, 9 of 0 %. Bestaande boekingen krijgen "geen btw"; er verandert niets aan.
  - Nieuwe rollen in het rekeningschema: *Te betalen btw*, *Voorbelasting*, *Debiteuren* en *Crediteuren*. De rolkeuze toont nu gewone namen.
  - Een regel op een btw-rekening zonder btw-code kan niet worden geboekt; een tegenboeking neemt de codes over.
  - De auditfile (XAF) bevat de btw per regel.
  - De invoer van btw in het journaal volgt in de volgende fase.

## [0.22.0] - 2026-11-19: Lijst en detail, acties volgens status, gewone taal en direct bewaren

### Gewijzigd
- **Juist bedrag van de bon** (besluit 43, uit het moederproduct).
  - "Totaal incl. btw" telt als totaal.
  - Rente, boetes en kosten bij te late betaling tellen nooit mee.
  - Bij twijfel wint het bedrag dat bij een boeking past.
  - In het detail van een bon staat *Ook op de bon:* met de andere bedragen van de PDF; één klik zet het bedrag.
- **Instellingen bewaart direct** (besluit 42), zoals Windows-Instellingen; de knop *Opslaan* is weg.
  - De naam wordt bewaard zodra u het veld verlaat of op Enter drukt. Een lege of te lange naam wordt niet bewaard: verlaat u de pagina, dan komt de vorige naam terug, met een melding.
  - Nieuw op Instellingen: *Uiterlijk*, met *Thema* (Systeem, Licht, Donker) en *Woorden* (Expert, Eenvoudig). Ze lopen gelijk met het menu Beeld.
  - De drempel *zonder bon* staat nu bij het filter in het Journaal: "vanaf € 10,00", alleen als het filter aan staat. Hij wordt ook direct bewaard.
  - De kaarten staan gelijk met de paginatitel.
- **Teksten in gewone taal** (besluit 41).
  - Het versleutelde bestand heet in de app *administratie*: *Nieuwe administratie…*, *Administratie openen…*, "Zet een back-up terug".
  - Alleen *bonnen*, geen *facturen* meer: de app heeft (nog) geen facturen.
  - Bestandsformaten en extensies (CAMT.053, XAF, .btwdb) staan alleen nog in tooltips en het bestandsfilter: het menu heet *Bankafschrift…* en *Auditfile…*.
  - Overal "…" in plaats van "...".
  - Sneltoetsen alleen in tooltips en menu's, niet meer in kaarten en meldingen.
  - Geen resten van het oude model meer ("administraties", "groepen").
  - "Kruisposten" alleen nog in de tooltip van de rol.
  - De kolommen Debet en Credit in de posteditor komen uit de woordenlijst.
- **Acties volgens status** (besluit 40): een knop staat er alleen als hij nu iets kan doen. Kan iets nog niet, dan staat er waarom, met één knop naar wat eerst moet.
  - **Boekjaren** is een lijst met detail, zonder knoppen in de rijen.
    - *Afsluiten* staat er alleen bij het oudste open jaar zonder concepten en met een reserverekening.
    - Anders staat er wat eerst moet, met een knop *Naar het journaal*, *Naar \<jaar>* of *Naar het rekeningschema*.
    - *Heropenen* staat er alleen bij het nieuwste afgesloten jaar.
    - *Auditfile (XAF)* staat in het detail en blijft in Bestand › Exporteren.
    - *Boekjaar \<jaar> openen* verschijnt pas vanaf december, of als het jaar voorbij is.
  - **Bank:** *Toewijzen* verschijnt pas als er een rekening is gekozen; een verwerkte transactie heeft een knop *Naar de post*.
  - **Bonnen:** *Koppelen* verschijnt pas als er een post is gekozen.
  - **Rapporten:** bij concepten een link *Naar het journaal*.
  - **Grootboekkaart:** zonder rekening alleen de keuze en een uitleg.
  - **Startwizard:**
    - *Vorige* en één hoofdknop (*Volgende* of *Voltooien*);
    - *Deze stap overslaan* is een link;
    - *Later verder* is weg, want sluiten (✕) onthoudt de stap ook;
    - geen uitgeschakelde knoppen meer; bij Bank staat zonder boekhouding wat eerst moet.
- **Lijst en detail** (besluit 38): Journaal, Bank, Bonnen, Boekingsregels, Relaties en Rekeningschema werken op dezelfde manier.
  - De lijst krijgt de volle breedte.
  - Het detail schuift rechts in als u een regel kiest of *Nieuw …*, en sluit met ✕ of Esc.
  - Zonder keuze staat er geen leeg vlak en geen "Selecteer een …" meer.
  - Een lege lijst toont alleen uitleg en één knop.
- **Opslaan** staat onderaan het detail; zonder detail onderaan de pagina, zodra er iets op te slaan is. Ctrl+S werkt altijd.
- **Jaar** rechts in de werkbalk, alleen bij meer dan één jaar en alleen bij Journaal, Bank, Rapporten en Grootboekkaart.
- **Journaal:**
  - een zoekveld met "Zoeken";
  - de kolom Nummer staat nu in het detail;
  - een geboekte post toont de regels als tekst, zonder invulvelden en prullenbakjes;
  - *Bijlage toevoegen* staat er één keer.
- **Bank:**
  - het saldo is één getal met de datum van het afschrift;
  - wijkt de boekhouding af van de bank, dan staat er een ⚠ met het verschil en hoeveel er nog te verwerken is;
  - *Regels toepassen* gaat vanzelf als u een boekingsregel opslaat, dus de knop is weg;
  - een transactie wordt niet meer vanzelf gekozen.
- **Bonnen:**
  - datum en bedrag bewaren zichzelf, dus de knop *Datum en bedrag bewaren* is weg;
  - bekijken met een dubbelklik;
  - verwijderen met Delete of het contextmenu;
  - de hele pagina is een sleepvlak.
- **Boekingsregels:**
  - de lijst toont per regel wanneer hij geldt en naar welke rekening het gaat ("naam bevat "energ", af → 4200 Energie");
  - in het detail staan alleen de ingevulde voorwaarden, met *Voorwaarde toevoegen ▾* voor de rest.
- **Relaties:** *Klant* en *Leverancier* in plaats van debiteur en crediteur.
- **Rekeningschema:** een lijst met nummer, naam, rubriek en saldo. Bewerken gebeurt in het detail; *Baten/lasten* staat alleen bij resultaatrekeningen en *Rol* onder *Geavanceerd*.

## [0.21.0] - 2026-11-16: Rustiger schil, platte navigatie en een zip zonder installatie

### Toegevoegd
- **Zip zonder installatie** (besluit 37): naast `BtwBoeker-<versie>.msi` staat op de release ook `BtwBoeker-<versie>-win-x64.zip`, uit dezelfde publicatie. Uitpakken en `BTW-Boeker.exe` starten werkt zonder installatie en zonder beheerdersrechten. Instellingen en logboek staan ook dan in de eigen Windows-map van de gebruiker.
- **Website:** de release-workflow zet de knop *Zip, zonder installatie* direct onder de downloadknop, of werkt hem bij als hij er al staat (`website-bijwerken.sh`, met tests).

### Gewijzigd
- **Platte navigatie** (besluit 33): Start · Journaal · Bank · Bonnen · Rapporten · Meer… · Instellingen. De onderdelen staan direct in de navigatie, zonder de tussenlaag *Boekhouding*. Elk onderdeel heeft een eigen pictogram en een eigen kleur, ook in de band boven de pagina. *Postvak bonnen* heet nu *Bonnen*.
- **De titel zegt waar u bent:** de paginatitel is het onderdeel (*Bank*, *Rapporten*), met één zin uitleg, ook in Eenvoudig.
- **Meldingsbalk in plaats van statusbalk:** meldingen staan rechts in de paginakop, met ✓, ⚠ of ✕ op een gekleurde achtergrond. Een ✓ verdwijnt na 6 seconden; ⚠ en ✕ blijven tot u ze sluit of naar een ander onderdeel gaat.
- **Titelbalk:** 🔒 *bestand – BTW-Boeker*, zonder de naam van de administratie. Het venster heeft weer de schaduw en rand van Windows, en op Windows 11 afgeronde hoeken.
- **Menubalk:** *Modules* (herhaalde de navigatie) en *Extra* (alleen Instellingen) zijn weg; *Instellingen* (Ctrl+,) staat in *Bestand*.
- **Opslaan alleen als er iets op te slaan is**, en dan als hoofdactie; Ctrl+S werkt altijd. De tekst "Niet-opgeslagen wijzigingen" is vervallen.
- **Navigatie:** één teller per regel; de focusrand verschijnt alleen bij toetsenbordgebruik. Boven de onderdelen staat geen lege lijn meer.
- **Instellingen:** de kaart lijnt uit op de paginatitel.

### Opgelost
- **Release-workflow:** het publiceren naar de website vond de MSI niet (`download-artifact` v8 met één artefact); nu met een vaste naam en map.

## [0.20.0] - 2026-11-11: Opgeruimd, en geen vraag meer om op te slaan na alleen bekijken

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
