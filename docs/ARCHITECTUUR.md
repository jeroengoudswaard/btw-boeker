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

## Teststrategie

| Laag | Soort | Voorbeelden |
|------|-------|-------------|
| Domain | Unit | Kleurparsing en WCAG-contrast, validatie van namen |
| Application | Unit | Herstelsleutel (Base32-roundtrip, tolerante invoer), wachtwoordbeleid, modulecatalogus |
| Infrastructure | Integratie (echte bestanden) | Bestand is echt versleuteld, verkeerd wachtwoord wordt geweigerd, herstelsleutel werkt, migraties zijn idempotent, back-up kan worden teruggezet, nooit overschrijven |
| App | ViewModel (met fakes) | Openen/annuleren/opnieuw proberen, navigatie en sneltoetsen, Opslaan/Niet opslaan/Annuleren, laatste pagina herstellen |
| App | UI (FlaUI, echte app) | Startpagina en menu's, wachtwoord → Start → Ctrl+1, donker thema, eigen titelbalk, netjes afsluiten |

**Regressie.** Bij elke wijziging draait de volledige suite. CI draait alle tests behalve de UI-tests. Die vereisen een interactieve sessie en draaien lokaal.

## Grootboek
```
Sjabloon (.btwsjabloon) of leeg ──► Inrichtvoorstel ──► wizard (rekeningen, beginsaldi, verschil)
                                                       │  back-up + één transactie
BoekhoudingDienst ──► IBoekhoudingRepository.PasToeAsync(stand → wijziging) ──► rubrieken, rekeningen, dagboeken, boekjaren, posten
        │
Grootboek (pure functies: saldo, saldibalans, balans, W&V, kaart) ──► Rapporten ──► RapportTabel ──► scherm / afdruk / CSV / xlsx
```

- **Domein** (`Domain.Financien.Boekhouding`):
  - `Journaalpost` bewaakt de regels: minstens twee regels, debet = credit, geen inactieve rekening, en een kas- of bankdagboek moet een regel op zijn eigen rekening hebben.
  - Boeken geeft een volgnummer per dagboek en jaar; de database bewaakt de uniekheid met een gefilterde unieke index.
  - Een geboekte post is onveranderlijk. `MaakTegenboeking` maakt het spiegelbeeld.
  - Een boekjaar is open of afgesloten. In een afgesloten jaar kan niets worden geboekt.
- **Wijzigen in één transactie.** `PasToeAsync` laadt de gevolgde stand en geeft die aan een functie. Die functie wijzigt bestaande objecten en noemt wat moet worden toegevoegd of verwijderd; daarna wordt alles in één transactie opgeslagen. Controles en wijzigingen gebeuren zo altijd op dezelfde, actuele stand.
- **Rapporten** rekenen alleen met geboekte posten. De W&V laat de afsluitpost buiten beschouwing; de balans telt het resultaat van het lopende jaar mee bij de passiva. `RapportTabel` is de gemeenschappelijke vorm voor scherm (FlowDocument), afdruk (`PrintDialog`, ook naar PDF), CSV en xlsx.
- **Jaarafsluiting.** De afsluitpost boekt de resultaatrekeningen naar de reserve (rol *Reserve*). De openingsbalans van het volgende jaar ontstaat uit de balansrekeningen. Heropenen verwijdert beide posten weer. Vooraf wordt altijd een back-up gemaakt.
- **Beginbalans.** Een beginbalans die niet in evenwicht is wordt geweigerd, tenzij de gebruiker het verschil bewust op een balansrekening boekt. Een resultaatrekening wordt daarvoor geweigerd.
- **Pagina.** `BoekhoudingViewModel` met `PostEditor` (concept of alleen-lezen) en het rekeningschema als bewerkbare regels. Ctrl+N is binnen de boekhouding "Nieuwe post". Ctrl+Enter boekt; de view vangt die toets eerder af dan het DataGrid. Ctrl+S slaat het concept en gewijzigde rekeningen op.
- **Teststrategie:**
  - Domein: posten, boeken, tegenboeken en het rekeningschema.
  - Application: saldi en rapporten op vaste testdata, sjablonen, en de dienst (nummering, afsluiten/heropenen, inrichten, verschilrekening).
  - Infrastructure: opslag op een echte versleutelde database, migraties vanaf de basisversie en de xlsx-export.
  - App: ViewModel-tests voor de pagina, de wizard en de shell. Eén FlaUI-test opent de boekhouding en maakt een nieuwe post.
  - Er wordt nooit een echte boekhouding gebruikt.

## Bank
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
  - `ControleerBeginsaldo` vergelijkt het eerste afschrift in een boekjaar met de openingsbalans plus eerder geboekte posten.
- **Teststrategie:**
  - Domain: transactiecodes, bankpost en vaste bankregel, boekingsregels.
  - Application: de lezer op synthetische bestanden in beide versies (`CamtVoorbeeld`), planner, herkenning en dienst.
  - Infrastructure: import, herkenning en boeken op een echte versleutelde database, plus de unieke index.
  - App: ViewModel-tests voor wizard, subtabbladen en shell. De FlaUI-test opent de subtabbladen Bank en Boekingsregels.
  - Echte afschriften zijn alleen lokaal gecontroleerd op aantallen en aansluiting, en niet bewaard.

## Relaties
- `Relatie` (Domain/Financien/Relaties): naam, adres, IBAN (zonder spaties, in hoofdletters) en e-mail; per database gedeeld. Bij een banktransactie herkent `Bankherkenning` eerst kruisposten en daarna de boekingsregels.
- Subtabblad *Relaties* onder *Meer…* in Boekhouding; Ctrl+S slaat op.

## Toegankelijkheid van groepen
- Een `Border` en de panels (`StackPanel`, `Grid`, `DockPanel` enz.) hebben in WPF geen automation peer. Een AutomationId of Name daarop is onzichtbaar voor schermlezers en voor FlaUI.
- Gebruik voor een herkenbare groep (editorkaart, sectie) `tg:Groepskader` (Border), `tg:Groepspaneel` (StackPanel) of `tg:Groepsraster` (Grid) uit `BtwBoeker.App.Views.Toegankelijkheid`, met `AutomationProperties.Name`.
- De architectuurtest `Xaml_zet_geen_AutomationId_op_een_element_zonder_automation_peer` bewaakt dit in alle XAML-bestanden.

## Weergaven Expert en Eenvoudig
- **Alleen visueel.** De database, de domeinmodellen, afdrukken en export blijven formeel. De keuze staat in de gebruikersinstellingen (`GebruikersInstellingen.Weergave`, standaard `Expert`), niet in de database.
- **Woordenlijst** (`Application/Weergaven/Woordenlijst.cs`): per sleutel een Expert-tekst, een Eenvoudig-tekst en optioneel een uitleg. Eén record per begrip, dus elk woord staat altijd in beide lijsten.
- **Woordenboek** (`App/ViewModels/Woordenboek.cs`): één exemplaar in `BoekhoudingHulpmiddelen`. Het geeft teksten via de indexer, de uitleg (alleen in Eenvoudig), aantallen ("3 boekingen") en de kolomkoppen van een potje. Een wissel meldt `Item[]`.
- **XAML:** `{DynamicResource T.Sleutel}` voor teksten en `{DynamicResource U.Sleutel}` voor de uitleg als tooltip. `Diensten/WoordenResources` houdt deze app-resources gelijk aan het woordenboek, zodat de weergave direct wisselt zonder herstart. De kolomkoppen van de Grootboekkaart lopen via een `Bindingsproxy` (Freezable), omdat een DataGrid-kolom buiten de visuele boom staat.
- **ViewModels:** tabtitel, statusteksten, rapportkeuzes en meldingen komen uit het woordenboek; `BoekhoudingViewModel` ververst ze bij een wissel.
- **Bewaking:** de architectuurtest `Xaml_gebruikt_alleen_termen_uit_de_woordenlijst` controleert dat elke `T.`/`U.`-sleutel in de XAML in de woordenlijst staat. De unittests controleren dat elke term een Expert- en een Eenvoudig-tekst heeft.

## Bijlagen
- **Domein** (`Domain/Financien/Bijlagen`): `Bijlage` (Id, administratie, `PostId` of leeg = postvak, bestandsnaam, soort, grootte, SHA-256, datum en bedrag) en `BijlageInhoud` (de bytes). Het soort wordt herkend aan de eerste bytes (`%PDF-`, JPG, PNG); maximaal 10 MB.
- **Application** (`Application/Bijlagen`): `BijlageDienst` (toevoegen, koppelen, ontkoppelen, gegevens, verwijderen, inhoud laden), `BonHerkenning` (datum en totaal uit PDF-tekst), `KoppelVoorsteller` (bedrag exact, datum ±7 dagen) en `ZonderBon`. `IPdfTekstLezer` is de poort naar de PDF-bibliotheek.
- **Opslag:** tabellen `Bijlagen` en `BijlageInhoud` (migratie *Bijlagen*). `BoekhoudingStand.Bijlagen` bevat alleen de metadata; de inhoud komt via `IBoekhoudingRepository.LaadBijlageInhoudAsync`, zodat lijsten licht blijven. De verwijzing naar de post is `ON DELETE SET NULL`: een verwijderd concept zet zijn bijlagen terug in het postvak. De inhoud wordt met `ON DELETE CASCADE` mee verwijderd.
- **PDF:** tekst lezen met PdfPig (`Infrastructure/Pdf/PdfPigTekstLezer`, regels gegroepeerd op hoogte); het voorbeeld via `Windows.Data.Pdf` (`App/Diensten/PdfPaginas`). Beide in het geheugen; de bijlage komt alleen via *Opslaan als…* op schijf. Een architectuurtest bewaakt dat PdfPig alleen in Infrastructure wordt gebruikt.
- **App:** partial `BoekhoudingViewModel.Bijlagen` (blok bij de post, postvak, filter), `BijlageRegel`, `BonRegel` (herberekent voorstellen bij elke wijziging van datum of bedrag), `BijlageVoorbeeldViewModel` en `BijlageVenster`. Slepen gaat via de code-behind van `BoekhoudingView` naar de commands.

## Uiterlijk Fluent Mica
- **Thema:** `Views/Thema/FluentMica.xaml` (samengevoegd in `App.xaml`).
  - Stijlen: `Paginavlak`, `Navigatieregel`, `Teller`, `Kerncijfertitel`/`Kerncijferwaarde`, `Vensterknop` en `Sluitknop`.
  - Lettertypen: `Lettertype` en `Koplettertype`.
  - Alle kleuren zijn DynamicResources van het Fluent-thema, zodat licht en donker vanzelf werken.
- **Accentkleur:** die van Windows (de Fluent-sleutels `AccentFillColor*Brush` en `AccentButton*`); de app zet er geen eigen kleur overheen.
- **Titelbalk:** `WindowChrome` (CaptionHeight 36) met eigen knoppen via `SystemCommands`; `ShellWindow.OnStateChanged` corrigeert de marge van een gemaximaliseerd venster en wisselt Maximaliseren en Herstellen. De AutomationIds `VensterMinimaliseren`, `VensterMaximaliseren` en `VensterSluiten` zijn voor de UI-tests.
- **Kerncijfers:** `Application/Inrichting/Kerncijfers.cs` (bankrekeningen via `Bankoverzicht.Saldi`, resultaat via `Grootboek.Resultaat`). `OverzichtViewModel.Kerncijferregels` maakt er titel, bedrag en toelichting van.
- **Tellers:** `ViewModels/Navigatietellers.Bepaal` vertaalt de startkaarten naar sleutels van de navigatiebalk (`Boekhouding.Bank`, …) en telt per module op. `NavigatieItem.Teller` wordt gezet na het bouwen van de navigatie en na elke vernieuwing van de startkaarten.
- **Instellingen:** `InstellingenView` toont de administratie met naam, accentkleur en de drempel voor *Zonder bon*.

## Kleur en rust
- **Onderdeelkleuren** (`ViewModels/Onderdeelkleuren.cs`): één palet (Koraal, Amber, Indigo, Violet, Blauw, Groenblauw, Grijs) voor licht én donker; elke kleur haalt minstens 3 : 1 op de tint (16 % dekking) en op de grond van beide thema's (test). `Voor(ModuleSoort)`, `OnderdeelVan(Startkaartsoort)`, `VoorGroep(naam)` (vaste kleur per naam, voor initialen) en `Initialen`. In XAML via `OnderdeelkleurConverter` (`Onderdeelkleur`, `Onderdeeltint`) en `{conv:Paletkleur Naam=…}`.
- **Stijlen** in `FluentMica.xaml`: `Welkomstitel` (32), `Pictogramtegel`, `Keuzetegel`, `Hoofdtegel` (de gekleurde keuze), `Tekstknop`. `Paginatitel` is 24 en `Sectietitel` 16; `DialoogKnop` minstens 120 px.
- **Paginakop uit de schil:** `TabbladViewModel.Paginatitel` en `Omschrijving` (uit `ModuleDefinitie.Omschrijving`); de schil tekent de band (onderdeelkleur), de titel, de zin en de administratie. Pagina's tekenen geen eigen titel meer.
- **Architectuurtests** (XAML als XML gelezen): lettergroottes 12/16/24/32 (pictogrammen 10–48), `Margin`/`Padding`/`Thickness` op 0/4/8/12/16/24/32/48, `CornerRadius` 4/8 (pil 10, rondje 14).
- **Laatst geopend:** `ShellViewModel.HeeftLaatsteDatabase`, `LaatsteDatabaseNaam`, `LaatsteDatabaseMap`, `OpenLaatsteDatabaseCommand`.
- **Beginsaldi:** `SjablooninrichtingViewModel.Bevestiging` (de `BoekhoudingViewModel` geeft `IDialoogService.Bevestig` mee) en `Bevestigingsvraag`.

## Sneller
- **Meten**: `Meetpunt` (App) schrijft "Duur {Handeling}: {ms}" naar het logbestand (`ShellLog.Duur`, zonder persoonsgegevens) bij aanmaken en openen van de database, de eerste pagina, Start, navigeren, boekhouding laden en overzichten maken. `tests/BtwBoeker.Infrastructure.Tests/Prestaties`: `Testdatabase` (kunstmatig, 3000 geboekte posten) en `MetingTests` met ruime tijdsbudgetten (trait `Categorie=Prestaties`). In de echte app: `AppUiTests.Snelheidsmeting_met_een_meerjarige_demodatabase` (trait `Categorie=Snelheid`, alleen met `BTWBOEKER_SNELHEID`) op `Meerjarendemo` (vijf jaar verhuur met bankafschriften, boekingsregels en afgesloten jaren); `Snelheidsrapport` maakt van de logregels een tabel met de eerste keer en het gemiddelde daarna.
- **Keuzelijsten in tabellen** binden aan een lijst op de rij zelf (`PostRegelEditor.Rekeningen`, `BoekingsregelRegel.Rekeningen`, `RekeningRegel.Rubrieken`), niet via `RelativeSource` naar de pagina. Die wordt pas gevonden als de cel in beeld komt; een keuzelijst zonder items schreef dan `null` terug en de regel telde als gewijzigd.
- **Verbindingen hergebruiken**: `DatabaseBeheer.Verbindingstekst` zet pooling aan, zodat SQLite3MC de sleutel niet bij elke query opnieuw afleidt. `LeegPool` leegt de pool bij het sluiten van de sessie, aan het eind van *aanmaken* en bij een mislukte opening (bestand weer vrij); de back-upkopie gebruikt geen pool. Architectuurtest: verbindingen alleen via `DatabaseBeheer`/`DatabaseSessie`, en `DisposeAsync` leegt de pool.
- **Momentopname van de boekhouding:** `BoekhoudingRepository.LaadAsync` bewaart per administratie de geladen stand en geeft die terug zolang de wijzigingsteller van de sessie (`Wijzigingen`) gelijk is. `BtwBoekerDbContext.SaveChanges(Async)` verhoogt de teller, ongeacht welke opslag schrijft; een bulkbewerking (`ExecuteDelete`/`ExecuteUpdate`/`ExecuteSql`) moet dat zelf doen (architectuurtest). Een stand uit `LaadAsync` is dus **gedeeld en alleen-lezen**: wijzigen gaat altijd via `PasToeAsync`, dat een eigen stand met tracking laadt.
- **Minder laden op Start:** `Startkaarten.LaadAsync` laadt de boekhouding één keer (`Startgegevens`) en de inrichtingsstatus en kerncijfers rekenen daarmee verder.
- **Lijsten in één keer:** `Verzameling<T>.Vervang` vervangt de inhoud van een lijst met één melding (Reset) in plaats van één per regel.

## Auditfile
- `XafSchrijver` (Application/Boekhouding) is puur: invoer `XafInvoer` (de `BoekhoudingStand`, het jaar, de naam, datum en versie), uitvoer `XafResultaat` (bytes, aantal posten, overgeslagen concepten). Elk bestand gaat vóór het teruggeven door het ingebedde XSD `XmlAuditfileFinancieel3.2.xsd`; bij een fout volgt een `InvalidOperationException` en wordt er niets geschreven. De uitvoer is voor dezelfde invoer byte voor byte gelijk.
- Opbouw: `header`, `company` met `customersSuppliers` (relaties `R`+Guid; type C/S/B/O; IBAN in `bankAccount`), `generalLedger` (B/P, rubriek als `leadDescription`), `periods` (12 maanden), `openingBalance` (opgeteld uit de openingsposten) en `transactions` (per dagboek; M/C/B/P/S; regels met `custSupID` op de debiteurregel als de post bij één factuur hoort). Btw-codes volgen met stap 3.
- `XafDienst` haalt de stand en de naam van de administratie uit `IDatabaseSessie`; `Bestandsnaam` maakt `auditfile-<naam>-<jaar>.xaf`.
- App: `BoekhoudingViewModel.ExporteerXafCommand` (parameter `JaarRegel` of het gekozen `Jaar`), `ShellViewModel.ExporteerXafCommand` en `IDialoogService.KiesXafDoel`.

## Jaarrekeningindeling
- Domain: `Jaarrekeningindeling` (per administratie en `JaarrekeningRapport`: Balans of BatenEnLasten) met `Indelingsregel` (Volgorde, `JaarrekeningAfdeling` Activa/Passiva/Baten/Lasten, `IndelingsregelSoort` Kop/Regel/Subtotaal, Tekst, Rubrieken). `Vervang` controleert alles vooraf en laat de indeling bij een fout ongewijzigd: een rubriek staat hooguit één keer per deel, kop en subtotaal hebben geen rubrieken, een regel minstens één.
- `Jaarrekeningrekenaar` (Application) rekent niet zelf: dit jaar en vorig jaar komen uit `Grootboek.Balans` en `Grootboek.Resultaat` (alleen geboekte posten). Bij de balans bepaalt het saldo per jaar het deel (debet = activa), bij baten en lasten `ResultaatKant`. Rekeningen die in geen regel vallen komen onder *Niet ingedeeld* (en tellen mee in de totalen); het resultaat van het lopende jaar staat als regel bij de passiva. Zonder eigen indeling geldt de automatische: één regel per rubriek plus *Overig*, dus hetzelfde als de bestaande rapporten.
- `JaarrekeningDienst` bewaart of zet terug (lege lijst), neemt een sjabloonindeling over op rubrieknaam en schrijft de indeling naar het sjabloonbestand (`Rekeningsjabloon.Jaarrekening`, veld `jaarrekening`, optioneel; formaatversie blijft 1).
- Opslag: tabellen `Jaarrekeningindelingen`, `Indelingsregels` en `IndelingsregelRubrieken` (migratie *Jaarrekening*). Een architectuurtest bewaakt dat grootboek, jaarafsluiting, bank en boekingen de indeling niet gebruiken.
- App: twee rapportkeuzes in *Boekhouding › Rapporten* (`Rapportsoort.JaarrekeningBalans` en `JaarrekeningBatenLasten`), selectievakje *Rekeningen tonen*, ⚠-melding bij niet-ingedeelde rekeningen en de knop *Indeling bewerken…* (`JaarrekeningIndelingViewModel`, `JaarrekeningIndelingVenster`, `IDialoogService.ToonJaarrekeningIndeling`). `Rapporten.Jaarrekening` maakt de tabel voor scherm, afdruk (Ctrl+P) en export, altijd in Expert-termen.

## Alleen zichtbaar wat nu nodig is
- Navigatie: `NavigatieItem.IsMeer` (de regel *Meer…*, sleutel `<Module>.Meer`) en `IsDiep` (items eronder, extra inspringing). `ShellViewModel.Navigatie` houdt per module bij of *Meer…* open is (`_meerOpen`). Alleen het command `MeerWisselen` klapt om, aangeroepen vanuit `ShellWindow` bij een klik, Enter of Spatie; de regel selecteren zet de selectie alleen terug naar de open pagina. WPF selecteert namelijk soms twee keer (`ListBox.OnGotKeyboardFocus` en de actie zelf), en de selectie volgt de pijltjes. Staat de huidige pagina onder *Meer…*, dan is hij open. `Boekhoudingonderdelen` geeft de dagelijkse items en de items onder *Meer…*.
- Verbergen in plaats van uitschakelen gaat via de converters `BoolNaarZichtbaar`, `OnwaarNaarZichtbaar`, `NietNullNaarZichtbaar`, `GelijkAanZichtbaar` en de nieuwe `GroterDanNulNaarZichtbaar`, gebonden aan eigenschappen in de ViewModels (`HeeftMeerdereBoekjaren`, `HeeftUitvoer`, `AantalZonderBon`, `Conceptmelding`, `ConceptenBoekenTekst`, `RegelsToepassenTekst`, …). De commando's zelf houden hun `CanExecute`, zodat sneltoetsen en het Expert-menu ongewijzigd werken.
- Nieuwe stijl `Linkknop` (App.xaml) voor tekstlinks als *+ Bon toevoegen* en *+ Rubriek*.
- Tests: `ShellNavigatieTests` (navigatie en *Meer…*), `Boekhouding/ZichtbaarheidTests`; de FlaUI-tests klappen *Meer…* met een echte muisklik uit voordat ze een item onder *Meer…* kiezen, en `Meer_blijft_dicht_met_de_pijltjes_en_klapt_om_met_enter_en_spatie` controleert het toetsenbord.

## Opslaan en tellers
- `BoekhoudingViewModel.BewaarAsync` slaat op wat klopt. Een gewijzigde boekingsregel met een `Fout` blijft achter via `_halveRegels`: `LaadRegels` zet die rijen terug en koppelt hun rekening aan het object uit de nieuwe `Toewijsrekeningen`, anders blijft de keuzelijst leeg. `BewaarAsync` geeft dan `false`, zodat een pagina met een halve regel niet ongemerkt sluit, en zet `Waarschuwing` (`IPaginameldingen`: ⚠ in de statusregel). Andere fouten (rekening, post, factuur) blokkeren nog wel alles.
- `Wijzigingen` geeft bij elke verhoging het event `Gewijzigd`, naar buiten als `IDatabaseSessie.Gewijzigd` (standaard een lege implementatie, zodat testvervangers het niet hoeven te hebben). De drie opslagen met een transactie verhogen ook na de commit: wie vlak ervoor las, zou anders de oude stand met de nieuwste versie bewaren.
- `ShellViewModel.SessieGewijzigd` komt op een willekeurige thread binnen en gaat via de `SynchronizationContext` naar de UI-thread. `VernieuwNaWijzigingAsync` wacht tot het 400 ms stil is en herberekent dan de startkaarten en tellers; `_kaartenRonde` zorgt dat de nieuwste berekening wint. Laden geeft geen seintje, dus er ontstaat geen lus.
- De teller in de navigatiebalk telt alle bankrekeningen; `TeVerwerkenPerRekening` en `BankkeuzeTekstConverter` tonen het aantal per rekening in de keuzelijst.

## Inrichten en één administratie

- **Eén administratie.** Het datamodel is per administratie (kolom `Administratie` in elke financiële tabel), zodat later een tweede object mogelijk is; de app toont er één (`AdministratieSoort.Standaard = 1`, getoond als *Mijn administratie* tot de gebruiker hem een naam geeft). Navigatie, menu en pagina's kennen geen administratie: `ShellViewModel.Administratie` is de ene administratie van de geopende database, `OpenModule` krijgt alleen een `ModuleSoort`, en alleen `BoekhoudingViewModel` en `InstellingenViewModel` krijgen de administratie mee. De migratie *EenAdministratie* verwijdert een tweede administratie alleen als die leeg is. De accentkleur is die van Windows.
- **Inrichten** gaat via één route: `BoekhoudingDienst.ImporteerAsync(Inrichtvoorstel)`. Een voorstel bevat rekeningen (`Inrichtrekening`) met beginsaldi; de dienst maakt rubrieken, rekeningen, dagboeken (memoriaal, kas, verkoop), het beginjaar en de openingsbalans, na een back-up. Bronnen: een rekeningschema-sjabloon (`.btwsjabloon`, wizard `SjablooninrichtingViewModel`) of leeg beginnen; het inlezen van een XAF-bestand volgt.
- **Startwizard:** Welkom → Database → Boekhouding → Bank → Klaar; de voortgang staat in de tabel `Startwizard`.
- **Reikwijdte:** `ReikwijdteTests` bewaakt dat onderdelen van het oude product (begroting, Excel-overname, standaardschema's, tweede administratie) niet terugkomen; `MigratieTests` dat bestaande databases zonder verlies worden bijgewerkt.

