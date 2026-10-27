# Release notes

De opmaak volgt [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versienummering [SemVer](https://semver.org/lang/nl/).

## [0.4.5] - 2026-10-28: Bugfixes na de Windows-test

### Opgelost
- In een lege boekhouding toonde het tabblad Boekhouding de startkaart niet, dus ook niet de knoppen *Standaardschema gebruiken*, *Sjabloonbestand*, *Overnemen uit Excel* en *Leeg beginnen*. Alleen *Bestand › Importeren* werkte. Oorzaak: `IsLeeg` gaf geen melding als het laden klaar was. Dit speelde sinds v0.4.0 en is gevonden door de FlaUI-test op Windows.
- De kolom *Soort* in Boekhouding › Relaties werkte pas na herladen bij als *Debiteur* of *Crediteur* werd aangevinkt.
- Beide met een regressietest. Bij een controle van alle berekende eigenschappen in de ViewModels kwamen geen andere gebonden gevallen naar voren.
- 640 geautomatiseerde tests (was 638).

## [0.4.4] - 2026-10-28: Standaardinrichting en sjablonen

### Toegevoegd
- **Standaardschema voor scoutinggroepen** (besluiten 114, 115, 117 en 118), zodat een groep zonder eigen Excel-boekhouding kan beginnen:
  - Vereniging: 27 rekeningen in 9 rubrieken, plus een contributierekening per speltak (vanaf 8010).
  - Stichting: 27 rekeningen in 8 rubrieken, met gebouw, inventaris, afschrijvingen en huur van de Vereniging.
  - De dagboeken Memoriaal en Verkoop worden aangemaakt, en bij de Vereniging ook Kas.
- **Wizard *Boekhouding inrichten met een sjabloon*:**
  - **Speltakken:** Bevers, Welpen, Scouts, Explorers en Roverscouts staan klaar; vink aan wat uw groep heeft en voeg eigen speltakken toe (besluit 119).
  - **Rekeningen:** zet rekeningen aan of uit en pas namen aan. De reserverekening blijft altijd aan, want de jaarafsluiting heeft haar nodig.
  - **Beginsaldi:** per balansrekening, met de controle debet = credit. Een verschil kan net als bij de Excel-overname alleen bewust op een tussenrekening (besluiten 73 en 120).
  - Vooraf wordt een back-up gemaakt; daarna is het boekjaar open.
- **Sjablonen delen met andere groepen** (besluit 116):
  - *Bestand › Exporteren › Rekeningschema als sjabloon* maakt een `.vsjabloon`-bestand.
  - *Bestand › Importeren › Rekeningschema-sjabloon* en de knop *Sjabloonbestand...* richten een lege administratie ermee in.
  - Een sjabloon bevat alleen nummers, namen, rubrieken en rollen, nooit bedragen of persoonsgegevens.
- Het startscherm van een lege boekhouding biedt nu vier keuzes: *Standaardschema gebruiken*, *Sjabloonbestand*, *Overnemen uit Excel* en *Leeg beginnen*.
- 638 geautomatiseerde tests (was 623).

### Gewijzigd
- PDFsharp-MigraDoc 6.2.4 vastgelegd als besluit 121.

### Nog niet functioneel
- De begrotingsindeling in het sjabloon volgt in fase 5 (besluit 114).

## [0.4.3] - 2026-10-28: Fase 4c-2 "Relaties en verkoopfacturen"

### Toegevoegd
- **Subtabblad Boekhouding › Relaties** (besluiten 105 en 108):
  - Huurders, sponsors en andere relaties met adres, e-mail, telefoon, IBAN en notitie; debiteur, crediteur of allebei.
  - Gedeeld door beide administraties en los van de leden. Een ongeldig IBAN-controlegetal wordt direct gemeld. Opslaan met Ctrl+S.
- **Subtabblad Boekhouding › Facturen** (besluiten 66, 94, 106, 107, 109 en 111):
  - Conceptfactuur met regels (omschrijving, aantal, prijs, opbrengstrekening), factuurdatum en betaaltermijn. Geen btw.
  - *Definitief maken* kent het nummer toe (`V2026-001`, per administratie en jaar, zonder gaten) en boekt de factuur in het nieuwe dagboek *VRK Verkoop*: 1310 *Debiteuren overig* aan de opbrengst.
  - Een definitieve factuur is onveranderlijk; corrigeren gaat met een **creditfactuur**, die eerst als concept verschijnt.
  - Filter *Open*, *Vervallen*, *Concept* en *Alle*; vervallen facturen staan vet.
  - Een nieuwe factuur alleen via de knop; Ctrl+N blijft *Nieuwe post*.
- **PDF-factuur** (besluiten 95, 110 en 112): logo, afzender, ontvanger, regels, "geen btw", betaalinstructie en een betaal-QR-code (EPC-QR, "girocode") die bankapps herkennen.
- **Instellingen › Facturen:** naam, adres, KvK-nummer, IBAN, e-mail, telefoon, standaard betaaltermijn (14 dagen), tekst onder de factuur en een logo (PNG of JPG, maximaal 2 MB, in de versleutelde database).
- **Betalingen herkennen in de bank:** een ontvangst met het factuurnummer in de omschrijving, of van het IBAN van de relatie met precies het bedrag van één open factuur, wordt een conceptpost op 1310.
- **Afletteren in de bank** kan nu ook met een of meer open facturen (niet samen met contributie in één keer).
- 623 geautomatiseerde tests (was 596).

### Gewijzigd
- PDFsharp-MigraDoc is vastgezet op 6.2.4, de laatste versie met de MIT-licentie (besluit 95). Versie 6.5 is AGPL-3.0 en vraagt een licentie.

### Nog niet functioneel
- Bijlagen (bonnen en facturen): stap 4c-3.
- Herinneringen voor vervallen facturen per e-mail: fase 6.
- Facturen en boekhouding voor de Stichting (besluiten 61 en 107).

## [0.4.2] - 2026-10-28: Fase 4c-1 "Contributie in de boekhouding"

### Toegevoegd
- **Run boeken** (besluiten 63, 91 en 99):
  - Bij *Definitief maken* boekt de app de run automatisch op de uitvoerdatum: 1300 *Debiteuren contributie* aan *Contributie <speltak>*.
  - Ontbreekt een opbrengstrekening, dan meldt de wizard dat. Bij *Incassoruns* kunt u de run later alsnog boeken (knop *Boeken*).
  - De kolommen *Geboekt* en *Ontvangen* tonen de stand per run.
  - Rekeningen 1300, 1310 en 1600 worden aangemaakt als ze nog niet bestaan (besluit 90).
- **Opbrengstrekening per categorie** in *Contributie › Tarieven*.
- **Herkenning in de bank**, vóór kruisposten en boekingsregels:
  - **Eigen incassobatch:** herkend aan het batchkenmerk (`PmtInfId`) en het bedrag. De ontvangst boekt 1300 af en markeert alle posten van de run als betaald.
  - **Stornering:** herkend aan het transactiekenmerk (EndToEndId) van een post. De vordering staat weer open, met een leesbare reden, bijvoorbeeld "Onvoldoende saldo (AM04)".
  - **Handmatige betaling:** herkend als IBAN en bedrag precies passen bij één open post (besluit 102).
- **Afletteren in de bank:** in het detailvenster kiest u voor een ontvangst een of meer open posten. Samen moeten ze precies het ontvangen bedrag zijn.
- **Subtabblad Contributie › Openstaand:**
  - Vorderingen per betaler met de status *Verwacht*, *Betaald*, *Gestorneerd* of *Afgeboekt*, de reden en het totaal dat openstaat.
  - *Opnieuw incasseren in volgende run* (besluit 92).
  - *Afboeken als oninbaar* op een kostenrekening (besluit 101).
- **Herincasso:**
  - De volgende run neemt de gestorneerde post mee als aparte post, met "(herincasso)" in de omschrijving (besluit 100).
  - De herincasso levert geen nieuwe opbrengst op en telt niet mee voor "nooit twee keer".
  - Wordt de run geannuleerd, dan wacht de herincasso weer op de volgende run.
- **Run annuleren:** een geboekte run wordt automatisch tegengeboekt. Heeft de bank de batch al uitgekeerd, dan kan annuleren niet meer.
- Bij het opnieuw inlezen van een afschrift vult de app het batchkenmerk en de retourreden aan bij transacties die al bekend zijn.
- 596 geautomatiseerde tests (was 565).

### Opgelost
- Een nieuwe rekening gaf bij het opslaan haar Id niet terug (altijd 0).

### Nog niet functioneel
- Relaties, openstaande posten buiten contributie en verkoopfacturen: stap 4c-2.
- Bijlagen (bonnen en facturen): stap 4c-3.

## [0.4.1] - 2026-10-27: Fase 4b "Bank"

Fase 4 wordt in stappen opgeleverd (besluit 68); elke stap verhoogt het laatste cijfer.

### Toegevoegd
- **Bankafschriften inlezen** (*Bestand › Importeren › Bankafschrift (CAMT.053)…* of de knop op het subtabblad Bank).
  - Formaat CAMT.053 (versie 001.02 en 001.08). Getest op de opbouw van Rabobank; ING volgt na een voorbeeldbestand (besluit 79).
  - Meerdere bestanden tegelijk, en bestanden die elkaar overlappen. Een transactie wordt nooit twee keer ingelezen: de app vergelijkt een vast kenmerk en de database bewaakt dat met een unieke index.
  - De wizard heeft drie stappen: Bestanden → Controleren → Klaar.
    - Bij Controleren ziet u per eigen rekening hoeveel transacties nieuw zijn, al bekend zijn of buiten een open boekjaar vallen.
    - Een nieuwe eigen rekening koppelt u daar aan een grootboekrekening, en u kiest de dagboekcode (bijvoorbeeld RABO).
  - Controles op de saldi:
    - Ontbreekt er een afschrift (het beginsaldo sluit niet aan op het vorige eindsaldo), dan geeft de app een waarschuwing.
    - Het eerste beginsaldo wordt vergeleken met de beginbalans (besluit 80).
  - Vooraf wordt een back-up gemaakt.
- **Subtabblad Bank** in Boekhouding:
  - Saldo van de bank tegenover het grootboek.
  - De transacties per eigen rekening, te filteren op te verwerken, concept, geboekt of alle.
  - Een detailvenster met *Toewijzen* (Enter) en *Regel maken van deze boeking*.
  - *Concepten boeken* (Ctrl+B) boekt alle bankconcepten na een bevestiging.
- **Herkenning:**
  - Overboekingen tussen eigen rekeningen gaan automatisch via 1298 Kruisposten.
  - Anders wint de eerste passende boekingsregel. Herkende transacties worden een concept (besluit 74), de rest blijft in de werkvoorraad (besluit 75).
- **Subtabblad Boekingsregels:**
  - Een regel kan letten op tegen-IBAN, tekst in naam of omschrijving, bij of af, bedrag van–tot en transactiecode, ook de eigen bankcode zoals Rabobank 1025 (besluit 78).
  - De regels hebben een vaste volgorde, te wijzigen met Alt+↑/↓.
  - Met *Regels toepassen* werkt u de werkvoorraad bij.
- **Bankposten in het Journaal:**
  - Een bankconcept kunt u controleren en de tegenregel splitsen. De bankregel staat vast op het bedrag van het afschrift.
  - Verwijdert u een concept of boekt u een geboekte bankpost tegen, dan gaat de transactie terug naar de werkvoorraad.
  - Een nieuwe post in een bankdagboek kan niet met de hand worden gemaakt.
- Het logboek bevat alleen aantallen; nooit IBAN's, namen of omschrijvingen.
- 565 geautomatiseerde tests (was 512).

### Nog niet functioneel
- Afletteren van de contributie-incasso en storneringen per betaler: stap 4c (de incasso staat op het afschrift als één regel met het totaal).
- ING: na een (geanonimiseerd) voorbeeldbestand.

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
