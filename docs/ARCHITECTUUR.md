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
