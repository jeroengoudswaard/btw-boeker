# Architectuur

## Lagen

```
BtwBoeker.App  ──►  BtwBoeker.Application  ──►  BtwBoeker.Domain
     │                      ▲
     └──►  BtwBoeker.Infrastructure ──┘
```

- **Domain**: entiteiten en waardeobjecten (`Administratie`, `Kleur`). Geen kennis van de database of de UI.
- **Application**: interfaces en applicatieregels (`IDatabaseBeheer`, `IDatabaseSessie`, `WachtwoordBeleid`, `Herstelsleutel`, `ModuleCatalogus`).
- **Infrastructure**: implementaties (EF Core, SQLite3MC, sleutelbeheer, JSON-instellingen).
- **App**: WPF-views en ViewModels (MVVM). ViewModels praten alleen met interfaces. Alle vensters lopen via `IDialoogService`, zodat de ViewModels zonder UI te testen zijn.

## Beveiliging van de database

Hiervoor wordt *envelope encryption* gebruikt:

1. Bij het aanmaken wordt een willekeurige **databasesleutel** van 256 bits gegenereerd. Daarmee versleutelt SQLite3MC het complete databasebestand.
2. Deze sleutel wordt twee keer ingepakt met AES-256-GCM en opgeslagen in `<naam>.btwdb.sleutel`:
   - met een sleutel die met **PBKDF2-SHA256** (600.000 iteraties) van het **wachtwoord** is afgeleid;
   - met een sleutel die met **HKDF-SHA256** van de **herstelsleutel** (200 bits) is afgeleid.
3. Voordelen:
   - Wachtwoord wijzigen of herstellen pakt alleen de sleutel opnieuw in. De database hoeft niet opnieuw versleuteld te worden.
   - Het wachtwoord en de herstelsleutel worden nergens opgeslagen.
   - Een verkeerd wachtwoord wordt betrouwbaar herkend via de GCM-authenticatietag.
4. De databasesleutel staat alleen in het geheugen zolang de database open is, en wordt bij het sluiten gewist.

Back-ups gebruiken de SQLite online-backup-API. Die levert een consistente kopie, ook terwijl de database open is. De kopie wordt samen met het sleutelbestand in één `.btwbackup`-bestand (zip) gezet en blijft versleuteld.

## Tabbladen en administraties

- `ModuleCatalogus` legt per module vast:
  - de sneltoets;
  - of de module bij één administratie hoort (Contributie, Boekhouding, Begroting);
  - of de module gedeeld is (Leden, E-mail);
  - of het tabblad gesloten mag worden.
- Elk tabblad-ViewModel krijgt zijn eigen administratie-context. Er is dus géén globale "actieve administratie".
- Alle tabbladen delen per administratie één `AdministratieViewModel`. Een gewijzigde naam of kleur is daardoor direct overal zichtbaar.

## Teststrategie

| Laag | Soort | Voorbeelden |
|------|-------|-------------|
| Domain | Unit | Kleurparsing en WCAG-contrast, validatie van namen |
| Application | Unit | Herstelsleutel (Base32-roundtrip, tolerante invoer), wachtwoordbeleid, modulecatalogus |
| Infrastructure | Integratie (echte bestanden) | Bestand is echt versleuteld, verkeerd wachtwoord wordt geweigerd, herstelsleutel werkt, migraties zijn idempotent, back-up kan worden teruggezet, nooit overschrijven |
| App | ViewModel (met fakes) | Openen/annuleren/opnieuw proberen, tabbladen (dubbel openen, sluiten, Ctrl+Tab, sneltoetsen), Opslaan/Niet opslaan/Annuleren, sessie herstellen |
| App | UI (FlaUI, echte app) | Startpagina en menu's, wachtwoord → overzicht → Ctrl+4 → Ctrl+,, donker thema, netjes afsluiten |

**Regressie.** Bij elke wijziging draait de volledige suite. CI draait alle tests behalve de UI-tests. Die vereisen een interactieve sessie en draaien lokaal.

## Leden en financiën (fase 2)

Er is één versleuteld databasebestand met gescheiden tabellen (besluit 39):

| Kant | Namespace (Domain/Infrastructure) | Tabellen |
|------|-----------------------------------|----------|
| Leden | `*.Leden` | `Personen`, `Functies`, `Groepen`, `Voogden`, `Veldherkomst` |
| Financiën | `*.Financien` | `Betalers`, `BetalerLeden` |
| Gedeeld | `BtwBoeker.Domain.Gedeeld` | Alleen `Lidnummer` |

- Er is **geen** foreign key tussen beide kanten. `BetalerLeden` koppelt een lidnummer aan een betaler (besluit 36).
- `BtwBoeker.Architectuur.Tests` controleert dit op twee manieren: via de signaturen (reflectie) en via de broncode in de mappen `Leden` en `Financien`.
- De enige plek die beide kanten tegelijk aanspreekt is de SOL3-import (`BtwBoeker.Application.Import`, `LedenImportOpslag`), want export 2 levert zowel adres- als IBAN-gegevens.

Elke persoon heeft een vaste `Guid`, een tijdstip `LaatstGewijzigdUtc` en per veld een herkomst (SOL3, BTW-Boeker of Leiding). Dat is de basis voor de synchronisatie met de webapp (besluit 38).

## SOL3-import

```
4 × CSV ──► Sol3CsvLezer ──► ExportHerkenning ──► Sol3Inlezer ──► Sol3Import (per lidnummer + meldingen)
                                                                     │
                       huidige ImportStand ──► ImportPlanner ──► ImportVoorstel (voorbeeldoverzicht)
                                                                     │  keuzes van de gebruiker
            back-up ──► LedenImportOpslag.PasToeAsync (één transactie) ──► ImportToepasser
```

- **Lezer:** Windows-1252 (of UTF-8 met BOM), `;`-gescheiden, aanhalingstekens, regeleinden binnen velden. Kolommen worden op naam gezocht.
- **Inlezer:** voegt de vier exports samen per lidnummer, ontdubbelt, normaliseert (telefoon, postcode, plaats, IBAN) en verzamelt waarschuwingen. Een melding bevat nooit veldwaarden zoals IBAN of e-mail, en wordt niet gelogd.
- **Planner:** een pure functie (stand + import → voorstel). De **toepasser** maakt bij het uitvoeren opnieuw een plan op de actuele stand en voert dat uit. Daardoor geeft dezelfde import een tweede keer geen wijzigingen.
- **Logboek:** alleen aantallen (`SOL3-import uitgevoerd: n nieuw, …`).

## Teststrategie fase 2

| Laag | Soort | Voorbeelden |
|------|-------|-------------|
| Domain | Unit | IBAN (mod 97, normalisatie), telefoonnummer NL/BE, postcode, SOL3-ID hoofdlettergevoelig, persoon (vergelijken, bijwerken, uitschrijven, functies, voogden, "Lid sinds") |
| Application | Unit | CSV-lezer (Windows-1252, quotes, foutregels), datums, exportherkenning, inlezer per besluit (25–43), planner/toepasser (idempotent, uitschrijven, herinschrijven, betaler per IBAN, dubbele personen), importdienst (alle vier verplicht, back-up vóór import) |
| Infrastructure | Integratie (echte versleutelde database) | Volledige import, herhaalde import geeft 0 wijzigingen, transactie wordt bij een fout teruggedraaid, back-up terugzetten, notities, gegevens niet leesbaar in het bestand |
| Architectuur | Reflectie + broncode | Lagen, leden ↮ financiën |
| App | ViewModel + FlaUI | Wizard (stappen, herkenning, uitschrijfkeuze, CSV-export), tabblad Leden (filters, notities, F5), menu-opdracht; UI-test: geïmporteerde leden in tabblad, wizard opent |

Alle testgegevens zijn synthetisch (`tests/BtwBoeker.Application.Tests/Import/Sol3Voorbeeld.cs`, gedeeld via een gekoppeld bestand).

## Contributie en SEPA-incasso (fase 3)

```
IncassoStand (leden + financiën, alleen-lezen) ──► ContributieRekenaar ──► IncassoVoorstel (posten, zelf te innen)
                                                                               │  Definitief maken
            back-up ──► IContributieRepository.BewaarRunAsync (één transactie, opnieuw rekenen + vergelijken)
                                                                               │
                                     Incassorun (momentopname) ──► Pain008Schrijver ──► XSD-controle ──► .xml
```

- **Rekenregel** (`Application.Incasso.ContributieRekenaar`): een pure functie.
  - Een maand is verschuldigd als het lid op de 1e ingeschreven is, een categorie met tarief heeft (de categorie op de 1e van de maand) en die maand nog niet in een run zat die niet geannuleerd is.
  - Naheffing gaat tot het begin van het lopende boekjaar, nooit vóór de instelling *eerste incassomaand*. Die voorkomt dat de eerste run jaren aan naheffing bevat.
- **Momentopname.** Een `Incassopost` bewaart IBAN, naam, mandaatkenmerk en ondertekeningsdatum zoals ze naar de bank gingen. Latere wijzigingen bij de betaler veranderen een run niet.
- **Nooit dubbel.**
  - `Postregels` heeft een unieke index op (lidnummer, maand) met `WHERE IsGeannuleerd = 0`.
  - Bij *Definitief maken* rekent de app binnen de transactie opnieuw. Wijkt de uitkomst af van wat de gebruiker heeft gecontroleerd, dan wordt er niets opgeslagen.
- **Bestand.** pain.008.001.08 met CORE, RCUR, één `PmtInf`, en `NOTPROVIDED` als BIC.
  - De uitvoer is deterministisch: aanmaaktijd en kenmerken komen uit de run. De SHA-256-hash wordt bij de run opgeslagen, en opnieuw opslaan moet dezelfde hash opleveren.
  - Het schema `src/BtwBoeker.Application/Incasso/pain.008.001.08.xsd` is het inhoudelijk ongewijzigde ISO 20022-schema (door ISO gegenereerd op 2019-02-14). Het komt uit het open-source pakket *sepaxml* (MIT) en is ingebed als resource.
- **Tekens en datums.**
  - `SepaTekst` zet tekst om naar de SEPA-tekenset.
  - `Bankwerkdagen` volgt de TARGET-kalender, met de paasdatum berekend.
  - `Bedrag` rekent in hele centen en heeft een eigen notatie, die op elk systeem gelijk is.
- **Voorbeeldbestand.** `tests/BtwBoeker.Application.Tests/Incasso/Voorbeeld-pain008.xml` is een vast, nagekeken voorbeeld.

## Grootboek (fase 4a)

```
Excel (Voorblad) ──► IWerkbladLezer (ClosedXML) ──► VoorbladInlezer ──► VoorbladVoorstel ──► wizard (soort, overslaan, verschil)
                                                                                                │  back-up + één transactie
BoekhoudingDienst ──► IBoekhoudingRepository.PasToeAsync(stand → wijziging) ──► rubrieken, rekeningen, dagboeken, boekjaren, posten
        │
Grootboek (pure functies: saldo, saldibalans, balans, W&V, kaart) ──► Rapporten ──► RapportTabel ──► scherm / afdruk / CSV / xlsx
```

- **Domein** (`Domain.Financien.Boekhouding`):
  - `Journaalpost` bewaakt de regels: minstens twee regels, debet = credit, geen inactieve rekening, en een kas- of bankdagboek moet een regel op zijn eigen rekening hebben.
  - Boeken geeft een volgnummer per dagboek en jaar; de database bewaakt de uniekheid met een gefilterde unieke index.
  - Een geboekte post is onveranderlijk (besluit 71). `MaakTegenboeking` maakt het spiegelbeeld.
  - Een boekjaar is open of afgesloten. In een afgesloten jaar kan niets worden geboekt.
- **Wijzigen in één transactie.** `PasToeAsync` laadt de gevolgde stand en geeft die aan een functie. Die functie wijzigt bestaande objecten en noemt wat moet worden toegevoegd of verwijderd; daarna wordt alles in één transactie opgeslagen. Controles en wijzigingen gebeuren zo altijd op dezelfde, actuele stand.
- **Rapporten** rekenen alleen met geboekte posten. De W&V laat de afsluitpost buiten beschouwing; de balans telt het resultaat van het lopende jaar mee bij de passiva. `RapportTabel` is de gemeenschappelijke vorm voor scherm (FlowDocument), afdruk (`PrintDialog`, ook naar PDF), CSV en xlsx.
- **Jaarafsluiting.** De afsluitpost boekt de resultaatrekeningen naar de reserve (rol *Reserve*). De openingsbalans van het volgende jaar ontstaat uit de balansrekeningen. Heropenen verwijdert beide posten weer. Vooraf wordt altijd een back-up gemaakt.
- **Overname uit Excel** (besluit 62):
  - Kolom A is het rekeningnummer, B de naam. G/H zijn W&V, I/J de balans per 31-12. Kopregels zonder nummer worden rubrieken, en de `SUM`-regel sluit het schema af.
  - 9990 Memoriaal wordt overgeslagen (besluit 70). De rollen Reserve, Kruisposten en Kas worden herkend aan de naam.
  - Een beginbalans die niet in evenwicht is wordt geweigerd, tenzij de gebruiker het verschil bewust op een balansrekening boekt (besluit 73). Een resultaatrekening wordt daarvoor geweigerd.
- **Tabblad.** `BoekhoudingViewModel` met `PostEditor` (concept of alleen-lezen) en het rekeningschema als bewerkbare regels. Ctrl+N is binnen het tabblad "Nieuwe post" (besluit 72). Ctrl+Enter boekt; de view vangt die toets eerder af dan het DataGrid. Ctrl+S slaat het concept en gewijzigde rekeningen op.
- **Teststrategie 4a:**
  - Domein: posten, boeken, tegenboeken en het rekeningschema.
  - Application: saldi en rapporten op vaste testdata, de Voorblad-inlezer op synthetische werkbladen (met en zonder verschil), en de dienst (nummering, afsluiten/heropenen, import, verschilrekening).
  - Infrastructure: opslag op een echte versleutelde database en ClosedXML-lezen/schrijven op een gegenereerd xlsx-bestand.
  - App: ViewModel-tests voor tabblad, wizard en shell. Eén FlaUI-test opent het tabblad en maakt een nieuwe post.
  - Er wordt nooit een echte boekhouding gebruikt.

## Bank (fase 4b)

```
CAMT.053 (.001.02/.001.08) ──► Camt053Lezer (pure functie) ──► CamtBestand ──► BankimportPlanner (sleutels, saldo-aansluiting)
                                                                                      │  wizard: nieuwe IBAN → grootboekrekening + dagboekcode
                     back-up ──► BankDienst.ImporteerAsync (één transactie) ──► Bankafschrift, Bankmutatie
                                                                                      │
                                         Bankherkenning: eigen IBAN → Kruisposten · eerste passende Boekingsregel · anders werkvoorraad
                                                                                      │
                              conceptpost in bankdagboek (Bankmutatie.PostId) ──► Ctrl+B: BoekConceptenAsync
```

- **Lezer.**
  - Elementen worden op lokale naam gezocht, zodat 001.02 en 001.08 met dezelfde code werken (bijvoorbeeld de naam onder `Pty/Nm` of direct onder `Nm`).
  - DTD's en externe entiteiten zijn uitgeschakeld.
  - Alleen transacties met status `BOOK` tellen; andere geven een melding.
  - De afschriftdatum is de datum van het eindsaldo (`CLBD`).
- **Nooit dubbel.**
  - De sleutel is `IBAN|R|<bankreferentie>`. Zonder bankreferentie, zoals bij Rabobank-rente, is het `IBAN|D|datum|bedrag|NtryRef|EndToEndId`.
  - De planner ontdubbelt binnen en tussen bestanden. `Bankmutaties` heeft een unieke index op (administratie, sleutel), met `COLLATE BINARY`.
- **Status.** Er is geen statusveld: zonder `PostId` staat de transactie in de werkvoorraad, met een conceptpost is ze concept, met een geboekte post geboekt.
- **Vaste bankregel.** `BoekhoudingDienst` weigert:
  - een wijziging waardoor de regels op de bankrekening niet meer gelijk zijn aan het bedrag van het afschrift (eerst gecontroleerd op een proefpost);
  - een andere dagboekkeuze voor een bankpost;
  - een nieuwe post in een bankdagboek.

  Een concept verwijderen of een bankpost tegenboeken zet de transactie terug in de werkvoorraad.
- **Herkenning.** Overboekingen naar of van een eigen rekening gaan naar de rekening met rol *Kruisposten*. Anders wint de eerste actieve regel in de volgorde (`Volgorde`, dan `Id`); alle ingevulde voorwaarden moeten kloppen.
- **Aansluiting.**
  - De planner meldt een saldo-gat tussen opeenvolgende afschriften, ook tegenover eerder opgeslagen afschriften.
  - Hij meldt ook een afschrift waarvan de transacties niet optellen tot het verschil tussen begin- en eindsaldo.
  - `ControleerBeginsaldo` vergelijkt het eerste afschrift in een boekjaar met de openingsbalans plus eerder geboekte posten (besluit 80).
- **Teststrategie 4b:**
  - Domain: transactiecodes, bankpost en vaste bankregel, boekingsregels.
  - Application: de lezer op synthetische bestanden in beide versies (`CamtVoorbeeld`), planner, herkenning en dienst.
  - Infrastructure: import, herkenning en boeken op een echte versleutelde database, plus de unieke index.
  - App: ViewModel-tests voor wizard, subtabbladen en shell. De FlaUI-test opent de subtabbladen Bank en Boekingsregels.
  - Echte afschriften zijn alleen lokaal gecontroleerd op aantallen en aansluiting, en niet bewaard.

## Contributie in de boekhouding (fase 4c-1)

```
Incassorun (contributiekant) ──► ContributieboekingDienst.BoekRunAsync ──► MEM-post 1300 (D) aan 80xx (C) + Debiteurmutatie "Vordering" per post
Bankmutatie ──► Bankherkenning ──► Contributievorderingen.Herken:
                  batch (PmtInfId = kenmerk-1, bedrag = totaal)  → Ontvangst per post
                  stornering (EndToEndId van een post)           → Stornering (+ redencode)
                  ontvangst van IBAN + bedrag van één open post  → Betaling
                ──► conceptpost Bank ↔ 1300 + de bijbehorende Debiteurmutaties (zelfde PostId)
Openstaand: Contributievorderingen.Bereken(stand) = som van de effectieve mutaties per vordering (EndToEndId)
```

- **Twee kanten, één bestand.** Runs en herincasso's horen bij de contributiekant (`IContributieRepository`); posten en `Debiteurmutaties` bij de boekhouding (`IBoekhoudingRepository`).
  - De boekhouding leest runs en herincasso's alleen.
  - Een categorie verwijst naar haar opbrengstrekening met een Id, zonder databasekoppeling.
  - Boeken na *Definitief maken* is een tweede stap. Mislukt die, dan blijft de run ongeboekt en kan hij later worden geboekt (besluit 99).
- **Effectieve mutaties.** Een `Debiteurmutatie` telt alleen als haar journaalpost bestaat en niet is tegengeboekt.
  - Een concept verwijderen maakt de aflettering daardoor vanzelf ongedaan; de app verwijdert de mutaties ook (cascade).
  - Tegenboeken maakt haar vanzelf ongedaan.
  - Annuleren van een geboekte run = de runpost tegenboeken.
- **Status van een vordering.** Saldo 0 is *Betaald*, of *Afgeboekt* als er een oninbaar-mutatie is. Saldo groter dan 0 is *Gestorneerd* als er een stornering is, anders *Verwacht*.
- **Herincasso.**
  - Een `Herincasso` wacht tot de volgende run. `ContributieRekenaar` maakt er een aparte `VoorstelPost` van, met `HerincassoVan` = het EndToEndId van de oorspronkelijke post.
  - De postregels krijgen `IsHerincasso`. De unieke index op (lid, maand) slaat ze over (`IsGeannuleerd = 0 AND IsHerincasso = 0`).
  - De runboeking telt herincasso's niet als opbrengst. Ontvangst en stornering van een herincasso gaan via `Incassopost.Vordering` naar de oorspronkelijke vordering.
- **Teststrategie 4c-1:**
  - Domain: herincassopost, opbrengstrekening, verrijken van een bankmutatie, redencodes, debiteurmutatie, herincasso.
  - Application: run boeken (en weigeren zonder opbrengstrekening), batch met juist en afwijkend bedrag, stornering, handmatige betaling, afletteren van meerdere posten, oninbaar, herincasso in de volgende run, annuleren met tegenboeking, concept verwijderen.
  - Infrastructure: de hele keten op een echte versleutelde database.
  - App: opbrengstrekeningen, de wizard die automatisch boekt, Openstaand, afletteren in Bank. De FlaUI-test opent Openstaand.

## Relaties en verkoopfacturen (fase 4c-2)

```
Relatie (gedeeld, los van leden) ◄── Verkoopfactuur (concept: vrij te wijzigen)
FactuurDienst.MaakDefinitiefAsync ──► nummer V/S<jaar>-<nnn> + VRK-post 1310 (D) aan opbrengst (C) + Debiteurmutatie "Vordering" op het nummer
Creditfactuur (CreditVan) ──► VRK-post omgekeerd + Debiteurmutatie "Creditering" op het oorspronkelijke nummer
Bankmutatie ──► Bankherkenning: contributie → factuur (nummer in omschrijving, of IBAN relatie + bedrag van één open factuur) → kruisposten → boekingsregels
Factuuroverzicht.Bereken(stand, vandaag) = saldo per factuurnummer uit de effectieve Debiteurmutaties (zelfde model als 4c-1)
IFactuurPdfMaker (Application) ◄── FactuurPdfMaker (Infrastructure: MigraDoc + QRCoder)
```

- **Eén vorderingenmodel.** Facturen gebruiken dezelfde `Debiteurmutatie` als contributie; `Vordering` is het factuurnummer en `BetalerId` het Id van de relatie. Tegenboeken of een concept verwijderen maakt een aflettering dus vanzelf ongedaan.
- **Nummering.** Het volgnummer wordt pas bij *Definitief maken* bepaald, binnen dezelfde transactie als de boeking. Een unieke index op (administratie, jaar, volgnummer) bewaakt dat er geen dubbele nummers ontstaan.
- **Onveranderlijk.** Bij *Definitief maken* worden naam en adres van de relatie vastgelegd in `Ontvanger`, zodat een latere adreswijziging de factuur niet verandert.
- **PDF.**
  - PDFsharp heeft buiten Windows geen eigen lettertypen. Een eigen `IFontResolver` gebruikt Segoe UI (of Arial) uit `C:\Windows\Fonts`, en op Linux (tests) Liberation Sans of DejaVu Sans.
  - De 1-bits PNG van QRCoder kan PDFsharp niet lezen. Daarom schrijft `FactuurPdfMaker.QrPng` de QR-matrix zelf weg als 8-bits grijswaarden-PNG.
  - De QR-inhoud volgt EPC069-12 versie 002 (`EpcQr.Inhoud`): naam, IBAN, bedrag en het factuurnummer als omschrijving. Een creditfactuur krijgt geen QR-code.
- **Teststrategie 4c-2:**
  - Domain: relatie (IBAN, e-mail, rol), factuurregels en afronding, concept → definitief, creditfactuur, logo-controle, EPC-QR-inhoud.
  - Application: definitief maken en boeken, doornummeren, creditfactuur (één keer), vervallen, herkenning op nummer en op IBAN, afletteren met meerdere facturen, instellingen.
  - Infrastructure: opslag op een echte versleutelde database, de PDF (met en zonder logo, credit) en de QR-PNG. Handmatig gecontroleerd: de QR-code in de PDF wordt gelezen als de juiste EPC-inhoud.
  - App: relaties, factuur maken tot en met PDF, creditfactuur, concept verwijderen, Ctrl+N, afletteren in Bank, Instellingen › Facturen. De FlaUI-test opent Relaties en Facturen.

## Toegankelijkheid van groepen (v0.4.6)

- Een `Border` en de panels (`StackPanel`, `Grid`, `DockPanel` enz.) hebben in WPF geen automation peer. Een AutomationId of Name daarop is onzichtbaar voor schermlezers en voor FlaUI.
- Gebruik voor een herkenbare groep (editorkaart, sectie) `tg:Groepskader` (Border), `tg:Groepspaneel` (StackPanel) of `tg:Groepsraster` (Grid) uit `BtwBoeker.App.Views.Toegankelijkheid`, met `AutomationProperties.Name`.
- De architectuurtest `Xaml_zet_geen_AutomationId_op_een_element_zonder_automation_peer` bewaakt dit in alle XAML-bestanden.

## Standaardinrichting en sjablonen (besluiten 114–121)

```
Sjablonen/vereniging.json, stichting.json (embedded resource) ─┐
.btwsjabloon van een andere groep ──────────────────────────────┴► Rekeningsjablonen.Lees (controles)
   ► Rekeningsjablonen.Rekeningen(sjabloon, speltakken) ► wizard (aan/uit, namen, beginsaldi)
   ► Rekeningsjablonen.Voorstel ► VoorbladVoorstel { Herkomst = "standaardinrichting", MetVerkoopboek }
   ► BoekhoudingDienst.ImporteerAsync (dezelfde route als de Excel-overname: back-up, rubrieken, rekeningen, dagboeken, boekjaar, openingsbalans)
Ingerichte administratie ► Rekeningsjablonen.UitStand ► Schrijf ► .btwsjabloon
```

- **Formaat.** JSON met `formaat: "btw-boeker-rekeningschema"` en `versie: 1`. Een nieuwere versie wordt geweigerd met de melding de app bij te werken. Daardoor kan fase 5 de begrotingsindeling toevoegen zonder oude bestanden te breken.
- **Controles bij het lezen:**
  - Unieke nummers (1–99999), een naam per rekening en bestaande rubrieken.
  - Een rol alleen op een balansrekening, precies één reserverekening, en hooguit één kas- en één kruispostenrekening.
  - Een geldige regel voor de speltakrekeningen.
- **Speltakken.** `speltakken: { rubriek, vanaf, stap, naam: "Contributie {speltak}", standaard: [...] }`. Dubbele en lege namen worden overgeslagen. Valt een nummer samen met een bestaande rekening, dan wordt geweigerd.
- **Privacy.** Een sjabloon bevat alleen nummers, namen, rubrieken en rollen. Een export uit de eigen administratie bevat dus geen saldi.
- **Teststrategie:**
  - Application: geldige ingebouwde sjablonen, speltakrekeningen, schrijven en lezen zonder verlies, weigeren van ongeldige bestanden, inrichten met beginsaldi en terug exporteren.
  - App: de wizard voor Vereniging en Stichting (speltak toevoegen, verplichte reserve, ongeldig bedrag, verschilrekening), een waarschuwing bij een sjabloon voor de andere administratie, inrichten en exporteren vanuit het tabblad, een ongeldig sjabloonbestand.
  - FlaUI: de wizard opent vanuit het startscherm.

## Historisch boekjaar (v0.4.8, besluiten 122–126)

```
Excel (alle werkbladen) ─► IWerkbladLezer.LeesAlle ─► HistorieInlezer
    Voorblad: rekeningen + totalen (balans I/J, W&V G/H)
    overige bladen: T-rekeningen naast elkaar (rij 2: nummer · naam · "Debet" · "Credit"), SUM-regels overgeslagen
  ─► HistorieVoorstel (per rekening: regels, saldo regels, saldo Voorblad, Klopt) ─► HistorieImportViewModel (voorbeeld)
  ─► HistorieDienst.ImporteerAsync ─► Boekjaar.Historisch (afgesloten) + HistorischeRegels + HistorischeTotalen (eigen tabellen)
Grootboek bij een historisch jaar: Saldo/Saldibalans/W&V/Balans uit HistorischeTotalen; Kaart uit HistorischeRegels (+ TotaalVolgensVoorblad)
```

- **Niet dubbel geboekt.** De Excel bevat losse T-rekeningen (de bank in maandtotalen), dus er zijn geen journaalposten. Historische regels staan in eigen tabellen en zijn geen `Journaalregel`. De architectuurtest `Historische_regels_worden_alleen_gebruikt_door_model_grootboek_en_historie` houdt bank, contributie, facturen en de jaarafsluiting erbuiten.
- **Alleen-lezen.** `Boekjaar.Historisch` is afgesloten, en `Heropen` weigert. Boeken in het jaar wordt al geweigerd doordat het jaar niet open is.
- **Datums (besluit 123).** Een regel zonder datum is een `Saldoregel` op 1 januari. Een datum buiten het jaar wordt 1 januari, en de oorspronkelijke datum komt als "(dd-MM-jjjj)" in de omschrijving.
- **Teststrategie:**
  - Domain: het historische boekjaar en de regels.
  - Application: een synthetische Excel met SUM-regels, regels zonder datum en met een oude datum, een afwijking en een onbekende rekening. Getest worden de import, de rapporten van 2025, en dat er niets meetelt in 2026.
  - Infrastructure: een xlsx met ClosedXML en de opslag.
  - App: de wizard, de keten vanuit de Excel-overname, het menu, de band, de kaart en Boekjaren.
  - FlaUI: het menu-item.

## Weergaven Expert en Eenvoudig (v0.4.9, besluiten 127–133)

- **Alleen visueel.** De database, de domeinmodellen, afdrukken en export blijven formeel (besluit 131). De keuze staat in de gebruikersinstellingen (`GebruikersInstellingen.Weergave`, standaard `Expert`), niet in de database.
- **Woordenlijst** (`Application/Weergaven/Woordenlijst.cs`): per sleutel een Expert-tekst, een Eenvoudig-tekst en optioneel een uitleg. Eén record per begrip, dus elk woord staat altijd in beide lijsten.
- **Woordenboek** (`App/ViewModels/Woordenboek.cs`): één exemplaar in `BoekhoudingHulpmiddelen`. Het geeft teksten via de indexer, de uitleg (alleen in Eenvoudig), aantallen ("3 boekingen") en de kolomkoppen van een potje (besluit 133). Een wissel meldt `Item[]`.
- **XAML:** `{DynamicResource T.Sleutel}` voor teksten en `{DynamicResource U.Sleutel}` voor de uitleg als tooltip. `Diensten/WoordenResources` houdt deze app-resources gelijk aan het woordenboek, zodat de weergave direct wisselt zonder herstart. De kolomkoppen van de Grootboekkaart lopen via een `Bindingsproxy` (Freezable), omdat een DataGrid-kolom buiten de visuele boom staat.
- **ViewModels:** tabtitel, statusteksten, rapportkeuzes en meldingen komen uit het woordenboek; `BoekhoudingViewModel` ververst ze bij een wissel.
- **Bewaking:** de architectuurtest `Xaml_gebruikt_alleen_termen_uit_de_woordenlijst` controleert dat elke `T.`/`U.`-sleutel in de XAML in de woordenlijst staat. De unittests controleren dat elke term een Expert- en een Eenvoudig-tekst heeft.

## Bijlagen (v0.4.10, stap 4c-3, besluiten 83–88, 96, 97 en 135–141)

- **Domein** (`Domain/Financien/Bijlagen`): `Bijlage` (Id, administratie, `PostId` of leeg = postvak, bestandsnaam, soort, grootte, SHA-256, datum en bedrag) en `BijlageInhoud` (de bytes). Het soort wordt herkend aan de eerste bytes (`%PDF-`, JPG, PNG); maximaal 10 MB.
- **Application** (`Application/Bijlagen`): `BijlageDienst` (toevoegen, koppelen, ontkoppelen, gegevens, verwijderen, inhoud laden), `BonHerkenning` (datum en totaal uit PDF-tekst), `KoppelVoorsteller` (bedrag exact, datum ±7 dagen) en `ZonderBon`. `IPdfTekstLezer` is de poort naar de PDF-bibliotheek.
- **Opslag:** tabellen `Bijlagen` en `BijlageInhoud` (migratie *Bijlagen*). `BoekhoudingStand.Bijlagen` bevat alleen de metadata; de inhoud komt via `IBoekhoudingRepository.LaadBijlageInhoudAsync`, zodat lijsten licht blijven. De verwijzing naar de post is `ON DELETE SET NULL`: een verwijderd concept zet zijn bijlagen terug in het postvak. De inhoud wordt met `ON DELETE CASCADE` mee verwijderd.
- **PDF:** tekst lezen met PdfPig (`Infrastructure/Pdf/PdfPigTekstLezer`, regels gegroepeerd op hoogte); het voorbeeld via `Windows.Data.Pdf` (`App/Diensten/PdfPaginas`). Beide in het geheugen; de bijlage komt alleen via *Opslaan als…* op schijf. Een architectuurtest bewaakt dat PdfPig alleen in Infrastructure wordt gebruikt.
- **App:** partial `BoekhoudingViewModel.Bijlagen` (blok bij de post, postvak, filter), `BijlageRegel`, `BonRegel` (herberekent voorstellen bij elke wijziging van datum of bedrag), `BijlageVoorbeeldViewModel` en `BijlageVenster`. Slepen gaat via de code-behind van `BoekhoudingView` naar de commands.
- **SOL3-import (besluit 141):** `ImportVoorstel.IbanVerschillen` en `ImportKeuzes.IbanOvernemen`; `ImportToepasser` koppelt een lid met een afwijkend IBAN alleen om als het is aangevinkt.

## Begroting (v0.5.0, fase 5a, besluiten 146–155)

- **Domein** (`Domain/Financien/Begrotingen`): `Begroting` (administratie, jaar, versie, status *Concept*/*Vastgesteld*, omschrijving, vastgesteld op) met `Begrotingsregel` (rekening, bedrag, toelichting). Een vastgestelde versie is alleen-lezen; `Herzie()` maakt een nieuwe conceptversie als kopie. Bedragen zijn positief in de richting van de rekening.
- **Baten of lasten:** `Grootboekrekening.Kant` (`ResultaatKant`). Standaard vanaf 8000 baten; bij de Excel-overname uit het W&V-saldo van het Voorblad; bij bestaande databases via `MigratieSql.KantVoorstellen` (saldo zonder afsluitposten, plus de historie). Instelbaar in het Rekeningschema en mee in sjabloonbestanden (optioneel veld `kant`).
- **Application** (`Application/Begrotingen`): `Begrotingsrekenaar` (geldende versie = laatste vastgestelde, anders het concept; overnemen met percentage, afgerond op tientallen; overzicht per rubriek met naar rato per dag) en `BegrotingDienst`. `Grootboek.Resultaat` geeft het saldo van een resultaatrekening zonder afsluitpost. Rapporten: `Rapporten.Begroting` en `Rapporten.BegrotingAlv`.
- **Opslag:** tabellen `Begrotingen` (uniek per administratie, jaar en versie) en `Begrotingsregels` (uniek per begroting en rekening), migratie *Begroting*.
- **App:** partial `BoekhoudingViewModel.Begroting`, `BegrotingRij` en `BegrotingVersie`; Ctrl+S via `BewaarAsync`, Ctrl+P via `Afdruktabel` (op het subtabblad Begroting de ALV-afdruk).
- **Bewaking:** de architectuurtest `Begroting_en_meerjarenplan_worden_niet_gebruikt_door_grootboek_jaarafsluiting_of_bank` (sinds v0.6.0 ook voor het meerjarenplan).

## Meerjarenplan Stichting (v0.6.0, fase 5b, besluiten 159–167)

- **Domein** (`Domain/Financien/Begrotingen/Meerjarenplan.cs`): `Meerjarenplan` (administratie, startjaar, 10 jaren, versie, status *Concept*/*Vastgesteld*) met `Meerjarenpost` (rekening, toelichting) en `Meerjarenbedrag` (jaar, bedrag). `ZetBedrag` weigert een jaar buiten het plan, een balansrekening, een negatief bedrag en een vastgesteld plan; `KopieerNaarVolgendeJaren` zet het bedrag van een jaar in alle latere jaren; `Herzie()` maakt een nieuwe conceptversie als kopie. Een lege post verdwijnt.
- **Application** (`Application/Begrotingen`): `Meerjarenrekenaar` (geldende versie per startjaar; overzicht met baten, lasten en resultaat per jaar, zonder indexatie of geldprognose) en `MeerjarenplanDienst`. `Rapporten.Meerjarenplan` geeft een `RapportTabel` met `IsLiggend`; `WpfAfdrukDienst` drukt die liggend af met kleinere letters (besluit 165).
- **Opslag:** tabellen `Meerjarenplannen` (uniek per administratie, startjaar en versie), `Meerjarenposten` (uniek per plan en rekening) en `Meerjarenbedragen` (uniek per post en jaar), migratie *Meerjarenplan*.
- **App:** partial `BoekhoudingViewModel.Meerjarenplan`, `MeerjarenRij`, `JaarCel` en `MeerjarenVersie`. Het subtabblad is alleen zichtbaar bij de Stichting (`HeeftMeerjarenplanTab`); de jaarkolommen krijgen hun kop via `Bindingsproxy`. De knop → per rij kopieert vanaf het jaar bij *Kopiëren vanaf* (besluit 166). Ctrl+5 opent bij de Stichting het meerjarenplan, bij de Vereniging de begroting (besluit 163).

## Startwizard (v0.7.0, besluiten 169–175)

- **Application** (`Application/Inrichting/Startwizard.cs`): `Startstap` (de acht stappen), `Startwizardvoortgang` (laatste stap en afgerond) met `IStartwizardOpslag` op `IDatabaseSessie.Startwizard`, en `Inrichtingsstatus`: het ✓ per stap, afgeleid uit administratienamen, leden, boekhouding (rekeningen of dagboeken), bankdagboek met IBAN, incassogegevens en tarieven van het jaar.
- **Opslag:** tabel `Startwizard` (één rij), migratie *Startwizard*.
- **App:** `StartwizardViewModel` is de regisseur; het hoofdvenster implementeert `IStartwizardRegie` en voert de bestaande opdrachten uit (nieuwe database, SOL3-import, de vier inrichtingsroutes van Boekhouding, bankimport, Contributie › Tarieven via `ContributieSubtab`). Naam en kleur (`AdministratieKaart`) en incasso (`IncassoKaart`) zijn gedeelde UserControls in `Views/Gedeeld`. `StartwizardVenster` is een eigen modaal venster; ✕ werkt als *Later verder*.
- **Eerste start:** `GebruikersInstellingen.StartwizardGetoond` zorgt dat de wizard maar één keer vanzelf opent.

## Uitleg SOL3-exports (v0.7.1, besluiten 185–187)

- `Application/Import/Sol3Exportuitleg.cs`: per export de titel, het doel en de kolommen in de volgorde van SOL3. *Verplicht* komt uit `ExportHerkenning.VerplichteKolommen`; de overige kolommen zijn wat SOL3 daarnaast levert. `Sol3ExportuitlegTests` vergelijkt dit met de kopregels in `Sol3Voorbeeld`.
- `Views/Gedeeld/Sol3Uitleg.xaml`: gedeeld door de startwizard en de importwizard; *Kolomnamen kopiëren* vult alleen het klembord (code-behind, geen logica).
- `Sol3CsvLezer.Scheidingsteken` kiest `;` of `,`: het teken dat buiten aanhalingstekens het vaakst in de kopregel staat (bij gelijkspel `;`), besluit 190.

## Herinrichting (v0.8.0, besluiten 168 en 177–183, 198)

- **Schil:** `ShellViewModel` (partial `ShellViewModel.Navigatie`) houdt de navigatiebalk (`Navigatie`, `NavigatieItem` met een vaste `Sleutel`, zoals `Boekhouding.Bank`), de wisselaar (`GekozenAdministratie`) en de statusregel (`Statussoort`, `Statusteken`). De pagina's blijven `TabbladViewModel`'s (naam uit de tabbladtijd) en worden per module en administratie bewaard in `Tabbladen`; `GeselecteerdTabblad` is de zichtbare pagina.
- **Onderdelen:** `TabbladViewModel.Onderdeel` is het subtabblad als getal (Boekhouding: `BoekhoudingSubtab`, Contributie: `ContributieSubtab`). De TabControls op de pagina's verbergen hun koppen; de navigatiebalk kiest het onderdeel en volgt een onderdeel dat de pagina zelf kiest (bijvoorbeeld Ctrl+5).
- **Wisselen** (besluit 182): dezelfde module en hetzelfde onderdeel voor de andere administratie, anders Start. `LaatstePagina` in de gebruikersinstellingen (module, administratie, onderdeel) vervangt `OpenTabbladen`.
- **Meldingen:** pagina's met `IPaginameldingen` (`Melding`, `Foutmelding`) vullen de statusregel.
- **Menubalk:** `IsMenubalkZichtbaar` (Expert, of tijdelijk na Alt in Eenvoudig; `ShellWindow.OnPreviewKeyDown`).
- **Startkaarten:** `Application/Inrichting/Startkaarten.cs` bepaalt de kaarten uit de gegevens; `KaartOpenenCommand` navigeert.
- **Hoofdactie en lege schermen:** `HoofdactieStijlConverter` maakt *Opslaan* alleen blauw waar `IsOpslaanHoofdactie`; `Views/Gedeeld/LeegScherm` met `Lijst` (de naam van de lijst). Architectuurtests: `Elke_pagina_heeft_hooguit_een_hoofdactie` en `Hoofdlijst_heeft_een_leeg_scherm`.
- **Beginjaar:** `StandaardinrichtingViewModel.Beginjaar` is te kiezen (tot 5 jaar terug).

## E-mail via Gmail (v0.9.0, fase 6, besluiten 191–196 en 203–208)

- **Domain** (`Domain/Communicatie/Email.cs`): `Emailsjabloon` (soort, naam, onderwerp, tekst; standaardsjablonen zijn aan te passen, niet te verwijderen), `VerzondenBericht` (logboek zonder tekst, besluit 195), `Emailinstellingen` (afzender, client-id en -geheim van het eigen Google Cloud-project, vernieuwingssleutel) en `Emailadres` (controle en ontdubbelsleutel). Communicatie staat los van Leden en Financiën, zodat de scheiding uit besluit 36 blijft gelden.
- **Application** (`Application/Email`): `Sjablooninvuller` vult `{veld}` per ontvanger in (hoofdletterongevoelig; een onbekend veld blijft staan en de pagina waarschuwt). `Ontvangers.VoorLeden` kiest de adressen (besluit 203: standaard de voogden bij een actieve jeugdlidfunctie, anders het lid; ontdubbeld op adres, met `{voornaam}` als opsomming). `Berichten` maakt de ontvangers voor de vooraankondiging en de stornering (via de betaler, besluit 204) en voor een factuur of herinnering (met de PDF als bijlage, besluit 207). `EmailDienst` verstuurt per ontvanger één bericht (besluit 194), logt elk bericht, stopt bij de daglimiet van 500 en vult ontbrekende standaardsjablonen aan. `IEmailVerzender` en `IGoogleKoppeling` zijn de poorten naar Google.
- **Infrastructure:** tabellen `Emailsjablonen`, `VerzondenBerichten` en `Emailinstellingen` (migratie *Email*), in de versleutelde database. `GmailVerzender` bouwt het bericht met MimeKit en verstuurt via `users.messages.send` met alleen het bereik `gmail.send`; `GoogleKoppeling` doet de eenmalige toestemming in de browser (`prompt=consent`, niets op schijf). Foutmeldingen van Google worden Nederlandse meldingen zonder adressen of sleutels.
- **App:** pagina `EmailViewModel` (module E-mail, niet per administratie) met de onderdelen `EmailSubtab` *Opstellen*, *Verzonden* en *Sjablonen* in de navigatiebalk. Andere pagina's bieden een bericht aan via `TabbladViewModel.EmailAanbieden` met een `EmailVoorstel` (soort, ontvangers, aanleiding); de schil opent dan E-mail › Opstellen en roept `BereidVoor` aan. Aanbiedingen: welkomstmail na een SOL3-import met nieuwe leden (schil), vooraankondiging na *Definitief maken* van een run en *Bericht mailen* bij een stornering (Contributie), factuur na *Definitief maken*, *Mailen* en *Herinnering sturen* (Facturen). `EmailInstellingenViewModel` is het onderdeel E-mail van Instellingen. Gmail-verzender en -koppeling komen via `BoekhoudingHulpmiddelen` binnen; zonder verzender (tests) mislukt versturen met een melding.
- **Privacy:** het Gmail-adres, de client-gegevens en de vernieuwingssleutel staan alleen in de database (niet in Git, niet in logbestanden); het logboek bewaart geen berichttekst. De handleiding voor het Google Cloud-project staat in `docs/GOOGLE-CLOUD.md`.

## Stichting volwaardig, drempel en groepen (v0.10.0–v0.10.1, besluiten 209–215)

- **Per administratie:** `FactuurInstellingenViewModel` en `EmailInstellingenViewModel` krijgen de administratie mee; `InstellingenViewModel` toont lijsten (`Facturen`, `Emailkoppelingen`). Het datamodel van boekhouding, bank en facturen was al per administratie.
- **Gmail per administratie:**
  - `Emailinstellingen.Id` is de waarde van `AdministratieSoort` (1 = Vereniging, 2 = Stichting); `EmailStand.Instellingen(administratie)` geeft de koppeling of een nieuwe.
  - `VerzondenBericht.Administratie` legt de afzender vast.
  - `EmailDienst.VerstuurAsync`, `ProefAsync`, `KoppelAsync` en `VerzondenVandaag` nemen de administratie mee; de daglimiet geldt per afzender.
  - `EmailVoorstel.Administratie` kiest de afzender bij een bericht uit een pagina.
- **Startwizard:** `IStartwizardRegie.ImporteerBankafschriftAsync(administratie)`, `StartwizardViewModel.BankVoor` en `KanBankInlezen`, en `Inrichtingsstatus.BankStichting`.
- **Drempel "zonder bon":** `Administratie.ZonderBonDrempel` (centen, standaard 1000). `Startkaarten.Bepaal` krijgt de drempels per administratie; `BoekhoudingViewModel` gebruikt de drempel van de eigen administratie.
- **Groepen:** `ILedenRepository.BewaarGroepsoortenAsync` (op groepsnaam) en `GroepenInstellingenViewModel` (Instellingen › Groepen).

## Uiterlijk Fluent Mica (v0.11.0, besluiten 216–221)

- **Thema:** `Views/Thema/FluentMica.xaml` (samengevoegd in `App.xaml`).
  - Stijlen: `Paginavlak`, `Navigatieregel`, `Teller`, `Kerncijfertitel`/`Kerncijferwaarde`, `Vensterknop` en `Sluitknop`.
  - Lettertypen: `Lettertype` en `Koplettertype`.
  - Alle kleuren zijn DynamicResources van het Fluent-thema, zodat licht en donker vanzelf werken.
- **Accentkleur per administratie:** `Views/Thema/Accentkleuren.PasToe` zet de Fluent-sleutels (`AccentFillColor*Brush`, `AccentButton*`, `TextOnAccentFillColorPrimaryBrush`) op het hoofdvenster wanneer de gekozen administratie of haar kleur wijzigt. Zonder database gelden de Windows-kleuren.
- **Titelbalk:** `WindowChrome` (CaptionHeight 36) met eigen knoppen via `SystemCommands`; `ShellWindow.OnStateChanged` corrigeert de marge van een gemaximaliseerd venster en wisselt Maximaliseren en Herstellen. De AutomationIds `VensterMinimaliseren`, `VensterMaximaliseren` en `VensterSluiten` zijn voor de UI-tests.
- **Kerncijfers:** `Application/Inrichting/Kerncijfers.cs` (bankrekeningen via `Bankoverzicht.Saldi`, openstaande contributie via `Contributievorderingen`, resultaat via `Grootboek.Resultaat`, begroting naar rato via `Begrotingsrekenaar.Overzicht`; alleen-lezen en daarom toegestaan in de architectuurtest over de begroting). `OverzichtViewModel.Kerncijferregels` maakt er titel, bedrag en toelichting van.
- **Tellers:** `ViewModels/Navigatietellers.Bepaal` vertaalt de startkaarten naar sleutels van de navigatiebalk (`Boekhouding.Bank`, …) en telt per module op. `NavigatieItem.Teller` wordt gezet na het bouwen van de navigatie en na elke vernieuwing van de startkaarten (ook bij wisselen van administratie).
- **Instellingen:** `InstellingenView` is een TabControl met de tabs `SubtabAdministraties`, `SubtabIncasso`, `SubtabFacturen`, `SubtabEmail` en `SubtabGroepen`; de ViewModels zijn ongewijzigd.

## Webapp voor de leiding (v0.12.0, fase 7, besluiten 33–38 en 222–233)

```
Desktop (leidend, versleuteld)  ── ledenkopie ──▶  Supabase (EU)  ◀── lezen / voorstel ──  Webapp (telefoon)
  WebappDienst + SupabaseServer  ◀── voorstellen ──  RLS per speltak                          SvelteKit (statisch)
```

- **Server** (`supabase/`):
  - `migrations/…_webapp_leiding.sql`: tabellen `speltakken`, `leden`, `lid_speltakken`, `voogden`, `beheerders`, `leiding`, `leiding_speltakken`, `voorstellen`. Geen kolom voor adres of financiële gegevens (een test controleert de kolomnamen).
  - Row-Level Security op elke tabel: alles eist `aal2` (MFA). De leiding leest alleen leden met een functie in de eigen speltak (`mag_lid_zien`) en mag alleen `voorstellen` invoegen (alleen de kolommen `lid_id`, `veld`, `oud`, `nieuw`; status en indiener zet de server).
  - De beheerder schrijft alleen via functies met een eigen controle (`controleer_beheerder`): `synchroniseer_leden(jsonb)` (upsert; vertrokken leden en hun voorstellen verdwijnen), `handel_voorstel_af`, `zet_leiding_speltakken`.
  - `functions/leiding`: Edge Function voor uitnodigen en intrekken. Alleen hier staat de `service_role`-sleutel (besluit 37); de logica (`logica.ts`) is los getest.
  - `tests/`: 32 beveiligingstests tegen een echte PostgreSQL (`lokaal.sh` start er een zonder Docker; CI gebruikt een service-container), met `supabase-stub.sql` voor rollen, `auth.uid()` en `auth.jwt()`; plus 15 tests van de Edge Function.
- **Desktop:**
  - Domain: `Leidingveld` (de velden waarvoor de leiding een voorstel mag doen; gelijk aan de controle op de server) en `Persoon.NeemVoorstelOver` (bron *leiding*, telefoon genormaliseerd). `Webapp/Webappinstellingen` (projectadres, publieke sleutel, e-mailadres beheerder, vernieuwingssleutel, laatste synchronisatie).
  - Application (`Application/Webapp`): `Webappkopie.Maak` (alleen leden met een actieve functie in een speltak; een test controleert dat de typen geen adres of financiële eigenschap hebben), `WebappDienst` (inloggen, sessie vernieuwen met roterende sleutel, synchroniseren, voorstellen met huidige waarde en afwijking, goedkeuren eerst lokaal en dan op de server, leiding), poort `IWebappServer`.
  - Infrastructure: `Webapp/SupabaseServer` via HTTP (Auth: wachtwoord, `factors/{id}/challenge` en `verify`; PostgREST; Edge Function). Foutmeldingen zonder persoonsgegevens; geen verbinding of een gepauzeerd project wordt `WebappOnbereikbaarException`. Tabel `Webappinstellingen` (migratie *Webapp*).
  - App: module `Webapp` (achteraan in `ModuleSoort`, zodat bewaarde waarden gelijk blijven) met de onderdelen *Voorstellen*, *Leiding* en *Synchroniseren*; `WebappViewModel`. De schil synchroniseert na het openen van de database (`SynchroniseerWebappAsync`, alleen als de beheerder is ingelogd).
- **Webapp** (`webapp/`): SvelteKit 3 met Svelte 5 en Vite 8 (sinds v0.13.1; instellingen in `vite.config.ts`, `#lib` via `imports` in `package.json`, imports met extensie), `adapter-static` (één `index.html` als terugval), officiële `@supabase/supabase-js`. Logica in `src/lib` (`telefoon.ts` volgt `Telefoonnummer`, `leden.ts`, `voorstellen.ts`) met Vitest; schermen in `src/routes`; Playwright-tests met een nagebootste Supabase-server (`page.route`). Beveiligingskoppen in `static/_headers` (Cloudflare Pages). Inrichten: `docs/SUPABASE.md`.

## Sneller (v0.13.0, besluiten 234–237)

- **Meten** (besluit 234): `Meetpunt` (App) schrijft "Duur {Handeling}: {ms}" naar het logbestand (`ShellLog.Duur`, zonder persoonsgegevens) bij aanmaken en openen van de database, de eerste pagina, Start, navigeren, wisselen van administratie, boekhouding laden en overzichten maken. `tests/BtwBoeker.Infrastructure.Tests/Prestaties`: `Testdatabase` (kunstmatig, 3000 geboekte posten) en `MetingTests` met ruime tijdsbudgetten (trait `Categorie=Prestaties`).
- **Verbindingen hergebruiken** (besluit 236): `DatabaseBeheer.Verbindingstekst` zet pooling aan, zodat SQLite3MC de sleutel niet bij elke query opnieuw afleidt. `LeegPool` leegt de pool bij het sluiten van de sessie, aan het eind van *aanmaken* en bij een mislukte opening (bestand weer vrij); de back-upkopie gebruikt geen pool. Architectuurtest: verbindingen alleen via `DatabaseBeheer`/`DatabaseSessie`, en `DisposeAsync` leegt de pool.
- **Momentopname van de boekhouding:** `BoekhoudingRepository.LaadAsync` bewaart per administratie de geladen stand en geeft die terug zolang de wijzigingsteller van de sessie (`Wijzigingen`) gelijk is. `BtwBoekerDbContext.SaveChanges(Async)` verhoogt de teller, ongeacht welke opslag schrijft; een bulkbewerking (`ExecuteDelete`/`ExecuteUpdate`/`ExecuteSql`) moet dat zelf doen (architectuurtest). Een stand uit `LaadAsync` is dus **gedeeld en alleen-lezen**: wijzigen gaat altijd via `PasToeAsync`, dat een eigen stand met tracking laadt.
- **Minder laden op Start:** `Startkaarten.LaadAsync` laadt elke administratie één keer (`Startgegevens`) en de kerncijfers rekenen daarmee verder; de inrichtingsstatus telt leden met `ILedenRepository.TelAsync`.
- **Lijsten in één keer:** `Verzameling<T>.Vervang` vervangt de inhoud van een lijst met één melding (Reset) in plaats van één per regel.

### Webapp-pakketten (v0.13.1)

- SvelteKit 3.1, adapter-static 4.0, vite-plugin-svelte 7.3, Vite 8.3, Vitest 4.1 en TypeScript 6.0: telkens de nieuwste versie die minstens 2 weken oud was. TypeScript 7 en Vitest 5 zijn bewust overgeslagen (lossen niets op; Vitest 5 loopt in npm 10 vast, zie hieronder).
- `npm audit --omit=dev --audit-level=high` staat in de CI-job `webapp-en-database`: een kwetsbaarheid in wat bij de gebruiker komt laat de build falen. Ontwikkelpakketten tellen daar niet mee.
- Het lockbestand is gemaakt met npm 11: npm 10 (10.9.x) stopt bij het opnieuw berekenen van deze afhankelijkheden met `Cannot read properties of null (reading 'edgesOut')`. `npm ci` werkt met npm 10 wel. Pakketten wijzigen: `npx npm@11 install`.

## Kleur en rust (v0.14.0, besluiten 239–247)

- **Onderdeelkleuren** (`ViewModels/Onderdeelkleuren.cs`): één palet (Koraal, Amber, Indigo, Violet, Blauw, Groenblauw, Grijs) voor licht én donker; elke kleur haalt minstens 3 : 1 op de tint (16 % dekking) en op de grond van beide thema's (test). `Voor(ModuleSoort)`, `OnderdeelVan(Startkaartsoort)`, `VoorGroep(naam)` (vaste kleur per groep, voor initialen) en `Initialen`. In XAML via `OnderdeelkleurConverter` (`Onderdeelkleur`, `Onderdeeltint`) en `{conv:Paletkleur Naam=…}`.
- **Stijlen** in `FluentMica.xaml`: `Welkomstitel` (32), `Pictogramtegel`, `Keuzetegel`, `Hoofdtegel` (de gekleurde keuze), `Tekstknop`. `Paginatitel` is 24 en `Sectietitel` 16; `DialoogKnop` minstens 120 px.
- **Paginakop uit de schil:** `TabbladViewModel.Paginatitel` en `Omschrijving` (uit `ModuleDefinitie.Omschrijving`); de schil tekent de band (onderdeelkleur), de titel, de zin en de administratie. Pagina's tekenen geen eigen titel meer.
- **Architectuurtests** (XAML als XML gelezen): lettergroottes 12/16/24/32 (pictogrammen 10–48), `Margin`/`Padding`/`Thickness` op 0/4/8/12/16/24/32/48, `CornerRadius` 4/8 (pil 10, rondje 14).
- **Laatst geopend:** `ShellViewModel.HeeftLaatsteDatabase`, `LaatsteDatabaseNaam`, `LaatsteDatabaseMap`, `OpenLaatsteDatabaseCommand`.
- **Startwizard:** `StartwizardViewModel.Tarieven` (`TariefRegel`, zoals Contributie › Tarieven) en `LaadTarievenCommand`; bewaren bij *Volgende*.
- **Beginsaldi:** `StandaardinrichtingViewModel.Bevestiging` (de `BoekhoudingViewModel` geeft `IDialoogService.Bevestig` mee) en `Bevestigingsvraag`.
- **Correctie of wissel:** `LidCategorieRegel.IsCorrectie`, `KanCorrigerenOfWisselen` en `Vervangt`; `LidCategorieInvoer.Vervangt` laat de opslag de oude toewijzing verwijderen.
- **Lid sinds:** `LidGegevens.Startdatum` en `Persoon.Startdatum` (kolom *Startdatum* in `Personen`); `Persoon.LidSinds` = vroegste functiestart, anders deze datum. *Startdatum functie* is in export 1 nu verplicht.

### Webapp in dezelfde stijl (v0.14.1)

- `webapp/src/lib/leden.ts`: `PALET` (gelijk aan `Onderdeelkleuren`), `tint()` (16 % dekkend, werkt in licht en donker) en `kleurVoorGroep()` (zelfde hash als `Onderdeelkleuren.VoorGroep`; `Groepskleur_is_gelijk_aan_die_in_de_webapp` en `ontwerp.test.ts` hebben dezelfde verwachtingen).
- `Avatar.svelte` kleurt op groep (`groep`), niet meer op lid-ID. De onderbalk kleurt het actieve onderdeel.
- `ontwerp.test.ts` leest alle `.svelte`- en `.css`-bestanden: lettergroottes 12/14/16/24/32, rondingen 4/8 (pil 10, rondje 14) en afstanden op het raster (plus 96 px boven de grote kop, besluit 222).
- `webapp/src/lib/contact.ts` (besluit 249): `contactopties(lid, actie)` geeft per actie (bellen, bericht, e-mail) de mogelijkheden in de volgorde voogd 1, voogd 2, lid zelf; mobiel en vast apart, berichten alleen naar mobiele nummers. Het scherm van een lid belt of mailt direct bij één mogelijkheid en toont anders het keuzeblad.

## Publiceren (v0.15.0, besluiten 250–254)

- **Installatiebestand:** `installer/BtwBoeker.Installer.wixproj` (WiX 5, MS-RL; bewust niet WiX 6 vanwege de onderhoudsbijdrage) met `Package.wxs`. Neemt alle bestanden uit de publicatiemap op (`<Files Include="…\**">`), per computer, vaste `UpgradeCode` met `MajorUpgrade`. Niet in `BtwBoeker.slnx`: alleen op Windows te bouwen.
- **Release:** `.github/workflows/release.yml`. Jobs `installatiebestand` (Windows: bouwen, testen zonder UI, `dotnet publish -r win-x64 --self-contained`, MSI, schermafbeeldingen), `webapp` (Linux: build met `BASE_PATH=/btw-boeker/app`, zip, schermafbeeldingen) en `publiceren` (alleen bij een tag: release `btw-boeker` in de websiterepository bijwerken, schermafbeeldingen in `btw-boeker/screens/` committen, `repository_dispatch app-released`). Het geheim `WEBSITE_TOKEN` heeft alleen schrijfrechten op de websiterepository.
- **Demodatabase:** `tests/BtwBoeker.Infrastructure.Tests/Demo/Demodatabase.cs`, gedeeld met de UI-tests. Alleen verzonnen namen en `example.org`-adressen; geen IBAN's. Alle posten in het jaar van "nu", zodat Start en de rapporten gevuld zijn.
- **Webapp in een submap:** `paths.base` uit `BASE_PATH`; `#lib/pad.ts` (`pad`, `huidig`) koppelt de pure functies uit `#lib/paden.ts` aan `$app/paths`. `prerender = true` met `ssr = false`: elke route is een leeg HTML-bestand, de app start in de browser. `hooks.server.ts` draait alleen bij het bouwen en vult `__CSP_SERVER__` in app.html met de oorsprong van `VITE_SUPABASE_URL` (`#lib/csp.ts`). Tweede Playwright-configuratie `playwright.pages.config.ts` test de build onder `/btw-boeker/app` zonder server.

## Auditfile (v0.16.0, besluiten 197 en 255–259)

- `XafSchrijver` (Application/Boekhouding) is puur: invoer `XafInvoer` (de `BoekhoudingStand` van één administratie, het jaar, de naam, de betalers, datum en versie), uitvoer `XafResultaat` (bytes, aantal posten, overgeslagen concepten). Elk bestand gaat vóór het teruggeven door het ingebedde XSD `XmlAuditfileFinancieel3.2.xsd`; bij een fout volgt een `InvalidOperationException` en wordt er niets geschreven. De uitvoer is voor dezelfde invoer byte voor byte gelijk.
- Opbouw: `header`, `company` met `customersSuppliers` (relaties `R`+Guid, betalers `B`+Guid; type C/S/B/O; IBAN in `bankAccount`), `generalLedger` (B/P, rubriek als `leadDescription`), `periods` (12 maanden), `openingBalance` (opgeteld uit de openingsposten) en `transactions` (per dagboek; M/C/B/P/S; regels met `custSupID` op de debiteurregel als de post bij één factuur of één betaler hoort). Btw-codes ontbreken bewust (niet btw-plichtig).
- `XafDienst` haalt de stand, de naam van de administratie en (alleen bij de Vereniging) de betalers uit `IDatabaseSessie`; `Bestandsnaam` maakt `auditfile-<naam>-<jaar>.xaf`.
- App: `BoekhoudingViewModel.ExporteerXafCommand` (parameter `JaarRegel` of het gekozen `Jaar`), `ShellViewModel.ExporteerXafCommand` en `IDialoogService.KiesXafDoel`.

## Jaarrekeningindeling (v0.17.0, besluiten 69, 151 en 263–268)

- Domain: `Jaarrekeningindeling` (per administratie en `JaarrekeningRapport`: Balans of BatenEnLasten) met `Indelingsregel` (Volgorde, `JaarrekeningAfdeling` Activa/Passiva/Baten/Lasten, `IndelingsregelSoort` Kop/Regel/Subtotaal, Tekst, Rubrieken). `Vervang` controleert alles vooraf en laat de indeling bij een fout ongewijzigd: een rubriek staat hooguit één keer per deel, kop en subtotaal hebben geen rubrieken, een regel minstens één.
- `Jaarrekeningrekenaar` (Application) rekent niet zelf: werkelijk en vorig jaar komen uit `Grootboek.Balans` en `Begrotingsrekenaar.Werkelijk` (alleen geboekte posten; een historisch jaar uit het Voorblad), begroot uit de geldende begroting en anders het meerjarenplan waarin het jaar valt (`Begrotingsbron`). Bij de balans bepaalt het saldo per jaar het deel (debet = activa), bij baten en lasten `ResultaatKant`. Rekeningen die in geen regel vallen komen onder *Niet ingedeeld* (en tellen mee in de totalen); het resultaat van het lopende jaar staat als regel bij de passiva. Zonder eigen indeling geldt de automatische: één regel per rubriek plus *Overig*, dus hetzelfde als de bestaande rapporten.
- `JaarrekeningDienst` bewaart of zet terug (lege lijst), neemt een sjabloonindeling over op rubrieknaam en schrijft de indeling naar het sjabloonbestand (`Rekeningsjabloon.Jaarrekening`, veld `jaarrekening`, optioneel; formaatversie blijft 1).
- Opslag: tabellen `Jaarrekeningindelingen`, `Indelingsregels` en `IndelingsregelRubrieken` (migratie *Jaarrekening*). Een architectuurtest bewaakt dat grootboek, jaarafsluiting, bank en boekingen de indeling niet gebruiken.
- App: twee rapportkeuzes in *Boekhouding › Rapporten* (`Rapportsoort.JaarrekeningBalans` en `JaarrekeningBatenLasten`), selectievakje *Rekeningen tonen*, ⚠-melding bij niet-ingedeelde rekeningen en de knop *Indeling bewerken…* (`JaarrekeningIndelingViewModel`, `JaarrekeningIndelingVenster`, `IDialoogService.ToonJaarrekeningIndeling`). `Rapporten.Jaarrekening` maakt de tabel voor scherm, afdruk (Ctrl+P) en export, altijd in Expert-termen.

## Alleen zichtbaar wat nu nodig is (v0.18.0, besluiten 269–272)

- Navigatie: `NavigatieItem.IsMeer` (de regel *Meer…*, sleutel `<Module>.Meer`) en `IsDiep` (items eronder, extra inspringing). `ShellViewModel.Navigatie` houdt per module bij of *Meer…* open is (`_meerOpen`). Besluit 273: alleen het command `MeerWisselen` klapt om, aangeroepen vanuit `ShellWindow` bij een klik, Enter of Spatie; de regel selecteren zet de selectie alleen terug naar de open pagina. WPF selecteert namelijk soms twee keer (`ListBox.OnGotKeyboardFocus` en de actie zelf), en de selectie volgt de pijltjes. Staat de huidige pagina onder *Meer…*, dan is hij open. `Boekhoudingonderdelen`, `Contributieonderdelen(tellers)` en `Webapponderdelen` geven per module de dagelijkse items en de items onder *Meer…*; *Openstaand* hangt af van de teller uit `Navigatietellers`, de webapp van `WebappViewModel.WebappGewijzigd` (gekoppeld of niet).
- Verbergen in plaats van uitschakelen gaat via de converters `BoolNaarZichtbaar`, `OnwaarNaarZichtbaar`, `NietNullNaarZichtbaar`, `GelijkAanZichtbaar` en de nieuwe `GroterDanNulNaarZichtbaar`, gebonden aan eigenschappen in de ViewModels (`HeeftMeerdereBoekjaren`, `HeeftUitvoer`, `AantalZonderBon`, `Conceptmelding`, `ConceptenBoekenTekst`, `RegelsToepassenTekst`, `FactuurRegel.KanMailen`, `RunRegel.KanAnnuleren`, `KanHerincassoNu`, `IsOninbaarKeuzeOpen`, `HeeftUitgeschreven`, …). De commando's zelf houden hun `CanExecute`, zodat sneltoetsen en het Expert-menu ongewijzigd werken.
- Nieuwe stijl `Linkknop` (App.xaml) voor tekstlinks als *+ Bon toevoegen* en *+ Rubriek*.
- Tests: `ShellNavigatieTests` (navigatie en *Meer…*), `Boekhouding/ZichtbaarheidTests`, aanvullingen in `ContributieboekingViewModelTests` (oninbaar in twee stappen) en `LedenViewModelTests`; de FlaUI-tests klappen *Meer…* met een echte muisklik uit voordat ze een item onder *Meer…* kiezen, en `Meer_blijft_dicht_met_de_pijltjes_en_klapt_om_met_enter_en_spatie` controleert het toetsenbord.

## Opslaan en tellers (besluiten 276 en 277)

- `BoekhoudingViewModel.BewaarAsync` slaat op wat klopt. Een gewijzigde boekingsregel met een `Fout` blijft achter via `_halveRegels`: `LaadRegels` zet die rijen terug en koppelt hun rekening aan het object uit de nieuwe `Toewijsrekeningen`, anders blijft de keuzelijst leeg. `BewaarAsync` geeft dan `false`, zodat een pagina met een halve regel niet ongemerkt sluit, en zet `Waarschuwing` (`IPaginameldingen`: ⚠ in de statusregel). Andere fouten (rekening, post, factuur, begroting) blokkeren nog wel alles.
- `Wijzigingen` geeft bij elke verhoging het event `Gewijzigd`, naar buiten als `IDatabaseSessie.Gewijzigd` (standaard een lege implementatie, zodat testvervangers het niet hoeven te hebben). De drie opslagen met een transactie verhogen ook na de commit: wie vlak ervoor las, zou anders de oude stand met de nieuwste versie bewaren.
- `ShellViewModel.SessieGewijzigd` komt op een willekeurige thread binnen en gaat via de `SynchronizationContext` naar de UI-thread. `VernieuwNaWijzigingAsync` wacht tot het 400 ms stil is en herberekent dan de startkaarten en tellers; `_kaartenRonde` zorgt dat de nieuwste berekening wint. Laden geeft geen seintje, dus er ontstaat geen lus.
- De teller in de navigatiebalk telt alle bankrekeningen; `TeVerwerkenPerRekening` en `BankkeuzeTekstConverter` tonen het aantal per rekening in de keuzelijst.

