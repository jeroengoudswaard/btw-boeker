# Architectuur

## Lagen

```
Verenigd.App  ──►  Verenigd.Application  ──►  Verenigd.Domain
     │                      ▲
     └──►  Verenigd.Infrastructure ──┘
```

- **Domain**: entiteiten en waardeobjecten (`Administratie`, `Kleur`). Geen kennis van de database of de UI.
- **Application**: interfaces en applicatieregels (`IDatabaseBeheer`, `IDatabaseSessie`, `WachtwoordBeleid`, `Herstelsleutel`, `ModuleCatalogus`).
- **Infrastructure**: implementaties (EF Core, SQLite3MC, sleutelbeheer, JSON-instellingen).
- **App**: WPF-views en ViewModels (MVVM). ViewModels praten alleen met interfaces. Alle vensters lopen via `IDialoogService`, zodat de ViewModels zonder UI te testen zijn.

## Beveiliging van de database

Hiervoor wordt *envelope encryption* gebruikt:

1. Bij het aanmaken wordt een willekeurige **databasesleutel** van 256 bits gegenereerd. Daarmee versleutelt SQLite3MC het complete databasebestand.
2. Deze sleutel wordt twee keer ingepakt met AES-256-GCM en opgeslagen in `<naam>.vdb.sleutel`:
   - met een sleutel die met **PBKDF2-SHA256** (600.000 iteraties) van het **wachtwoord** is afgeleid;
   - met een sleutel die met **HKDF-SHA256** van de **herstelsleutel** (200 bits) is afgeleid.
3. Voordelen:
   - Wachtwoord wijzigen of herstellen pakt alleen de sleutel opnieuw in. De database hoeft niet opnieuw versleuteld te worden.
   - Het wachtwoord en de herstelsleutel worden nergens opgeslagen.
   - Een verkeerd wachtwoord wordt betrouwbaar herkend via de GCM-authenticatietag.
4. De databasesleutel staat alleen in het geheugen zolang de database open is, en wordt bij het sluiten gewist.

Back-ups gebruiken de SQLite online-backup-API. Die levert een consistente kopie, ook terwijl de database open is. De kopie wordt samen met het sleutelbestand in één `.vbak`-bestand (zip) gezet en blijft versleuteld.

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
| Gedeeld | `Verenigd.Domain.Gedeeld` | Alleen `Lidnummer` |

- Er is **geen** foreign key tussen beide kanten. `BetalerLeden` koppelt een lidnummer aan een betaler (besluit 36).
- `Verenigd.Architectuur.Tests` controleert dit op twee manieren: via de signaturen (reflectie) en via de broncode in de mappen `Leden` en `Financien`.
- De enige plek die beide kanten tegelijk aanspreekt is de SOL3-import (`Verenigd.Application.Import`, `LedenImportOpslag`), want export 2 levert zowel adres- als IBAN-gegevens.

Elke persoon heeft een vaste `Guid`, een tijdstip `LaatstGewijzigdUtc` en per veld een herkomst (SOL3, Verenigd of Leiding). Dat is de basis voor de synchronisatie met de webapp (besluit 38).

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

Alle testgegevens zijn synthetisch (`tests/Verenigd.Application.Tests/Import/Sol3Voorbeeld.cs`, gedeeld via een gekoppeld bestand).

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
  - Het schema `src/Verenigd.Application/Incasso/pain.008.001.08.xsd` is het inhoudelijk ongewijzigde ISO 20022-schema (door ISO gegenereerd op 2019-02-14). Het komt uit het open-source pakket *sepaxml* (MIT) en is ingebed als resource.
- **Tekens en datums.**
  - `SepaTekst` zet tekst om naar de SEPA-tekenset.
  - `Bankwerkdagen` volgt de TARGET-kalender, met de paasdatum berekend.
  - `Bedrag` rekent in hele centen en heeft een eigen notatie, die op elk systeem gelijk is.
- **Voorbeeldbestand.** `tests/Verenigd.Application.Tests/Incasso/Voorbeeld-pain008.xml` is een vast, nagekeken voorbeeld. Bijwerken na een bewuste wijziging: draai de test met `VERENIGD_VOORBEELDEN_BIJWERKEN=1` en controleer het verschil voordat u het commit.

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
- Gebruik voor een herkenbare groep (editorkaart, sectie) `tg:Groepskader` (Border), `tg:Groepspaneel` (StackPanel) of `tg:Groepsraster` (Grid) uit `Verenigd.App.Views.Toegankelijkheid`, met `AutomationProperties.Name`.
- De architectuurtest `Xaml_zet_geen_AutomationId_op_een_element_zonder_automation_peer` bewaakt dit in alle XAML-bestanden.

## Standaardinrichting en sjablonen (besluiten 114–121)

```
Sjablonen/vereniging.json, stichting.json (embedded resource) ─┐
.vsjabloon van een andere groep ──────────────────────────────┴► Rekeningsjablonen.Lees (controles)
   ► Rekeningsjablonen.Rekeningen(sjabloon, speltakken) ► wizard (aan/uit, namen, beginsaldi)
   ► Rekeningsjablonen.Voorstel ► VoorbladVoorstel { Herkomst = "standaardinrichting", MetVerkoopboek }
   ► BoekhoudingDienst.ImporteerAsync (dezelfde route als de Excel-overname: back-up, rubrieken, rekeningen, dagboeken, boekjaar, openingsbalans)
Ingerichte administratie ► Rekeningsjablonen.UitStand ► Schrijf ► .vsjabloon
```

- **Formaat.** JSON met `formaat: "verenigd-rekeningschema"` en `versie: 1`. Een nieuwere versie wordt geweigerd met de melding de app bij te werken. Daardoor kan fase 5 de begrotingsindeling toevoegen zonder oude bestanden te breken.
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
