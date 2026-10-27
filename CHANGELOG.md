# Release notes

De opmaak volgt [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versienummering [SemVer](https://semver.org/lang/nl/).

## [0.4.0] - 2026-10-27: Fase 4a "Grootboek"

### Toegevoegd
- **Tabblad Boekhouding** per administratie (Ctrl+4, of via *Modules*). Het heeft vijf subtabbladen:
  - **Journaal:**
    - Journaalposten met dagboek, datum, omschrijving en regels (rekening, omschrijving, debet of credit).
    - Een post is *Concept* (opslaan met Ctrl+S) of *Geboekt* (Ctrl+Enter). Boeken kan pas als debet en credit gelijk zijn.
    - Geboekte posten krijgen een doorlopend nummer per dagboek en jaar (`MEM-2026-0001`) en zijn onveranderlijk. Corrigeren gaat via *Tegenboeken*.
    - Ctrl+N maakt binnen dit tabblad een nieuwe post (besluit 72). Ctrl+F zoekt op nummer of omschrijving.
  - **Rekeningschema:** rekeningen (nummer, naam, rubriek, soort balans/resultaat, rol, actief) en rubrieken. Een rekening met de rol *Kas* krijgt automatisch het dagboek Kas.
  - **Grootboekkaart:** alle mutaties van één rekening over een periode, met begin-, lopend en eindsaldo.
  - **Rapporten:** saldibalans, balans en staat van baten en lasten, per rubriek. Alleen geboekte posten tellen mee.
  - **Boekjaren:**
    - *Afsluiten*: het resultaat gaat naar de reserverekening, het jaar wordt vergrendeld en de openingsbalans van het volgende jaar wordt gemaakt. Vooraf wordt een back-up gemaakt.
    - *Heropenen* zet dat terug.
    - *Volgend boekjaar openen* maakt het mogelijk om alvast in het nieuwe jaar te boeken.
- **Overname uit de huidige Excel-boekhouding** (*Bestand › Importeren › Boekhouding uit Excel…*, of de knop in een lege administratie). De wizard heeft vier stappen:
  1. **Bestand:** het werkblad *Voorblad* wordt gelezen.
  2. **Rekeningen:** per rekening de soort controleren; rekeningen kunnen worden overgeslagen (9990 Memoriaal standaard).
  3. **Beginbalans:** controle debet = credit.
     - Een verschil wordt geweigerd, tenzij u het bewust op een tussenrekening boekt (standaard *1299 Te onderzoeken verschillen*, besluit 73).
     - Het verschil staat dan als aparte regel op de openingsbalans en in het logboek.
  4. **Klaar:** vooraf is een back-up gemaakt.

  De overname kan één keer per administratie. Het Excel-bestand wordt alleen gelezen en niet bewaard.
- **Afdrukken en exporteren** (besluit 67):
  - *Bestand › Afdrukken…* (Ctrl+P) drukt het rapport of de grootboekkaart af via het Windows-afdrukvenster, ook naar PDF.
  - Exporteren kan naar Excel (.xlsx, via ClosedXML) en naar CSV (`;`, UTF-8, Nederlandse bedragnotatie).
- Het logboek bevat alleen aantallen, bedragen en jaartallen; nooit namen of IBAN's.
- 512 geautomatiseerde tests (was 443).

### Nog niet functioneel
- Bankimport (CAMT.053) en boekingsregels: stap 4b.
- Contributie boeken, storneringen, debiteuren/crediteuren en verkoopfacturen: stap 4c.
- De boekhouding van de Stichting kan al worden geopend, maar wordt pas later uitgewerkt (besluit 61).

## [0.3.0] - 2026-10-27: Fase 3 "Contributie & SEPA-incasso"

### Toegevoegd
- **Tabblad Contributie · Vereniging** (Ctrl+3) met vier subtabbladen:
  - **Leden & categorie:**
    - Per lid een contributiecategorie met ingangsdatum; de historie blijft bewaard.
    - De app stelt *Jeugdlid <speltak>* of *Leiding* voor. Met *Voorstellen overnemen* neemt u alle voorstellen in één keer over.
    - Een waarschuwing verschijnt bij geen categorie, geen betaler of geen geldige machtiging.
  - **Betalers & machtigingen:**
    - IBAN (met controle), rekeninghouder, mandaatkenmerk, ondertekenings- en intrekkingsdatum.
    - Leden koppelen en ontkoppelen.
  - **Tarieven:** maandbedrag per categorie per boekjaar, met *Kopiëren van vorig jaar*. Leiding en Vrijgesteld staan vast op € 0,00.
  - **Incassoruns:** historie met status. Het bestand kan opnieuw worden opgeslagen (altijd identiek), en een run die de bank niet heeft uitgevoerd kan worden geannuleerd.
- **Incassowizard** (*Bestand › Nieuwe incassorun…* of de knop in het tabblad), in vier stappen:
  1. **Maand:** de maand kiezen.
  2. **Berekening:** posten per betaler en de lijst "zelf te innen".
  3. **Controleren:** controle en *Definitief maken*. Eerst wordt een back-up gemaakt.
  4. **Klaar:** het pain.008-bestand en de CSV "zelf te innen" opslaan.
- **Rekenregel:**
  - Een maand telt als het lid op de 1e ingeschreven is.
  - Gemiste maanden uit het lopende boekjaar worden nageheven, oudere komen op de lijst "zelf te innen".
  - Een maand wordt nooit twee keer geïncasseerd; de database bewaakt dat ook.
  - Eén post per betaler, zonder gezinskorting.
- **SEPA-bestand pain.008.001.08:**
  - CORE, altijd RCUR, uitvoerdatum op een TARGET-werkdag.
  - De tekst wordt omgezet naar de toegestane SEPA-tekens.
  - Het bestand wordt gecontroleerd tegen het officiële ISO 20022-schema voordat het kan worden opgeslagen.
- **Instellingen › Incasso:** incassant-ID (met controlegetal), naam, IBAN, incassodag, eerste incassomaand en het omschrijvingssjabloon.
- Het logboek bevat alleen de maand, het aantal posten en het totaal; nooit IBAN's of namen.
- 443 geautomatiseerde tests (was 313).

### Nog niet functioneel
- Boeken in het grootboek, storneringen en terugboekingen (CAMT.053): fase 4.
- Afdracht aan Scouting Nederland: fase 4.

## [0.2.0] - 2026-10-27: Fase 2 "Leden"

### Toegevoegd
- **SOL3-import** (Bestand › Importeren › SOL3-leden, of de knop in het tabblad Leden), als wizard in vier stappen:
  1. **Bestanden:** de vier eigen SOL3-exports kiezen of in het venster slepen. De app herkent elke export aan zijn kopregel; de volgorde van bestanden en kolommen doet er niet toe. Alle vier zijn verplicht.
  2. **Groepen:** nieuwe groepen indelen als speltak, bestuur, technisch of overig. Groepen met "(Migratie)" staan standaard op technisch.
  3. **Controleren:** voorbeeldoverzicht met de tabbladen Nieuw, Gewijzigd (oud → nieuw per veld), Uitgeschreven (per lid aan te vinken) en Waarschuwingen (op te slaan als CSV). Er is dan nog niets gewijzigd.
  4. **Klaar:** de import wordt in één transactie uitgevoerd, na een automatische back-up (`<naam>-voor-import-<datum-tijd>.vbak` naast de database). Mislukt er iets, dan blijft de database ongewijzigd.
- Importregels:
  - SOL3 is leidend.
  - Identieke dubbele regels worden ontdubbeld.
  - Telefoonnummers worden opgeslagen als `+31…` en getoond als `06 …`; postcodes krijgen de notatie `1234 AB`; plaatsnamen in hoofdletters worden genormaliseerd.
  - IBAN's worden vergeleken zonder spaties en hoofdletters. Leden met hetzelfde IBAN horen bij één betaler.
  - Dubbele functies worden samengevoegd met de vroegste startdatum.
  - Voogdnamen als "Voogd 1" betekenen dat de naam onbekend is.
  - Leden die uit de export verdwijnen worden na bevestiging uitgeschreven, nooit verwijderd.
  - Dezelfde import twee keer uitvoeren verandert niets.
- Waarschuwingen bij:
  - een onbekende status of een onbekend lidtype;
  - een onleesbare datum of een onherkend telefoonnummer;
  - een ongeldige postcode of een ongeldig IBAN-controlegetal;
  - een Contactpersoon-ID dat niet 1-op-1 bij het lidnummer hoort;
  - een lid dat ontbreekt in een van de exports;
  - mogelijk dubbele personen.
- **Tabblad Leden** (Ctrl+2):
  - Ledenlijst met zoeken (Ctrl+F) op naam, lidnummer of e-mail, en filters op groep, lidtype en "Toon uitgeschreven".
  - Vernieuwen met F5.
  - Gegevens van het geselecteerde lid: persoon en adres, "Lid sinds", functies (actief, beëindigd, technisch) en voogden.
  - **Notities** zijn het enige bewerkbare veld; ze worden opgeslagen met Ctrl+S. De overige gegevens komen uit SOL3.
- Datamodel:
  - Ledengegevens (personen, functies, groepen, voogden) en financiële gegevens (betalers) staan in gescheiden tabellen, alleen gekoppeld via het lidnummer.
  - Per persoon worden een vaste ID, een tijdstip "laatst gewijzigd" en de herkomst per veld bijgehouden, als voorbereiding op de webapp voor groepsleiders (fase 7).
- Architectuurtests (nieuw project `Verenigd.Architectuur.Tests`) bewaken de lagen en de scheiding leden–financiën.
- 313 geautomatiseerde tests (was 124).

### Nog niet functioneel
- IBAN invullen of wijzigen in Verenigd, SEPA-machtigingen en contributiecategorie: fase 3.
- De groepsindeling kan alleen bij een import worden gekozen.

## [0.1.0] - 2026-10-26: Fase 1 "Fundament"

### Toegevoegd
- Hoofdvenster met klassieke menubalk (Bestand, Bewerken, Beeld, Modules, Extra, Help), tabbladen en statusbalk.
- Tabbladen per administratie. Dezelfde module kan voor Vereniging én Stichting open staan. De administratie is altijd herkenbaar aan een gekleurde markering, een band en de statusbalk.
- Sneltoetsen: Ctrl+N/O/S/W, Ctrl+F4, Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+1 t/m 6 (modules), Ctrl+, (instellingen). Een tabblad sluit u ook met de middelste muisknop.
- Thema Systeem/Licht/Donker (WPF Fluent), wordt onthouden.
- Versleutelde database (`.vdb`) met wachtwoord (minimaal 12 tekens) en eenmalig getoonde herstelsleutel.
- Wachtwoord vergeten: openen met de herstelsleutel en een nieuw wachtwoord instellen.
- Back-up maken en terugzetten (`.vbak`). Bestaande databases worden nooit overschreven.
- Instellingen: naam en accentkleur per administratie, met kleurvoorstellen en live voorbeeld.
- Open tabbladen worden bij de volgende start hersteld.
- Logging naar `%LOCALAPPDATA%\Verenigd\logs` zonder persoonsgegevens.
- Contributie is alleen beschikbaar voor de Vereniging; het menu toont geen keuze en Ctrl+3 opent altijd de Vereniging.
- 124 geautomatiseerde tests (unit, integratie, UI) en een CI-pipeline (GitHub Actions).

### Nog niet functioneel
- De modules Leden, Contributie, Boekhouding, Begroting en E-mail tonen een plaatshouder. Ze volgen in fase 2 t/m 6.
