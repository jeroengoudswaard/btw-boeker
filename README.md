# BTW-Boeker

Windows-desktopapplicatie voor de leden-, contributie- en financiële administratie van een scoutingvereniging en de bijbehorende stichting.

| Fase | Inhoud | Status |
|------|--------|--------|
| 1 | Fundament: hoofdvenster met tabbladen, versleutelde database, herstelsleutel, back-up, thema's | ✅ Gereed (v0.1.0) |
| 2 | Leden, met import uit SOL3 (scouting.nl) | ✅ Gereed (v0.2.0) |
| 3 | Contributie en SEPA-incasso (pain.008, Rabobank) | ✅ Gereed (v0.3.0) |
| 4 | Bankimport (CAMT.053, Rabobank en ING) en dubbel boekhouden | 🔨 In uitvoering: 4a Grootboek (v0.4.0), 4b Bank (v0.4.1) en 4c-1 Contributie (v0.4.2) en 4c-2 Relaties en facturen (v0.4.3) en de standaardinrichting (v0.4.4) gereed; 4c-3 Bijlagen volgt |
| 5 | Jaarbegroting (vereniging) en meerjarenplan (stichting) | Gepland |
| 6 | E-mail via Gmail | Gepland |
| 7 | Webapp voor groepsleiders (Supabase) | Gepland |

## Vereisten

- Windows 10 (versie 2004 of nieuwer) of Windows 11; het PDF-voorbeeld van bijlagen gebruikt de PDF-functie van Windows zelf
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.4xx), of `winget install Microsoft.DotNet.SDK.10`
- Optioneel: Visual Studio 2026 met de workload **.NET desktop development**

## Bouwen en starten

### Visual Studio
1. Open `BtwBoeker.slnx`.
2. Stel **BtwBoeker.App** in als opstartproject en druk op **F5**.
3. Tests: menu **Test › Run All Tests** (Test Explorer).

### Command line
```powershell
dotnet build                                   # bouwt alles; waarschuwingen gelden als fouten
dotnet run --project src/BtwBoeker.App          # start de applicatie
dotnet test --filter "Categorie!=UI"           # unit- en integratietests (snel, ook in CI)
dotnet test --filter "Categorie=UI"            # UI-tests: start de echte app; muis en toetsenbord niet gebruiken
```

Bij de UI-tests bewaart `$env:BTWBOEKER_SCREENSHOTS = "C:\pad"` screenshots van elke stap.
Typ of klik niet tijdens de UI-tests: FlaUI gebruikt het echte toetsenbord en de echte muis. Elke UI-test heeft een time-out van 3 minuten en sluit zijn eigen app-proces altijd af.

### Database-migraties
```powershell
dotnet tool restore
dotnet ef migrations add <Naam> --project src/BtwBoeker.Infrastructure --output-dir Database/Migraties --namespace BtwBoeker.Infrastructure.Database.Migraties
```
Bestaande databases worden bij het openen automatisch bijgewerkt.

## Projectstructuur

```
src/
  BtwBoeker.Domain/          Domeinmodel en bedrijfsregels (geen dependencies)
  BtwBoeker.Application/     Use cases en interfaces (database, instellingen, beveiliging)
  BtwBoeker.Infrastructure/  EF Core + SQLite3MC (versleuteling), sleutelbeheer, JSON-instellingen
  BtwBoeker.App/             WPF (Fluent-thema), MVVM met CommunityToolkit.Mvvm, Generic Host
tests/
  BtwBoeker.*.Tests/         xUnit-tests per laag; App.Tests bevat ook de FlaUI-UI-tests
  BtwBoeker.Architectuur.Tests/  Bewaakt de lagen en de scheiding leden–financiën
docs/
  ARCHITECTUUR.md           Ontwerpkeuzes, beveiliging en teststrategie
```

## Bestanden en privacy

| Bestand | Inhoud |
|---------|--------|
| `*.btwdb` | De database, volledig versleuteld (SQLite3 Multiple Ciphers, ChaCha20-Poly1305) |
| `*.btwdb.sleutel` | De ingepakte databasesleutel. **Hoort altijd naast de `.btwdb`.** Zonder dit bestand is de database niet te openen |
| `*.btwbackup` | Een back-up: database en sleutelbestand samen in één bestand, nog steeds versleuteld |
| `%APPDATA%\BTW-Boeker\instellingen.json` | Thema, laatst geopende database, open tabbladen |
| `%LOCALAPPDATA%\BTW-Boeker\logs\` | Logbestanden (30 dagen), zonder persoonsgegevens |

SOL3-exports zijn echte persoonsgegevens: bewaar ze in `privé/` of buiten de repository. Hetzelfde geldt voor de Excel-boekhouding die u eenmalig overneemt en voor bankafschriften (CAMT.053). De tests gebruiken alleen verzonnen gegevens.

Echte gegevens horen **nooit** in Git. `.gitignore` blokkeert `*.btwdb`, `*.btwbackup`, sleutelbestanden en de mappen `voorbeelden/` en `privé/`.

## Dependencies

| Pakket | Doel |
|--------|------|
| CommunityToolkit.Mvvm | MVVM (ObservableObject, RelayCommand) |
| Microsoft.Extensions.Hosting | Dependency injection en configuratie |
| Microsoft.EntityFrameworkCore.Sqlite.Core | ORM en migraties |
| SQLite3MC.PCLRaw.bundle | SQLite met versleuteling |
| ClosedXML (MIT) | Excel lezen (overname boekhouding) en schrijven (export rapporten) |
| PDFsharp-MigraDoc 6.2.4 (MIT) | PDF van de verkoopfactuur. Bewust de laatste MIT-versie: vanaf 6.5 is de licentie AGPL-3.0 |
| QRCoder (MIT) | Betaal-QR-code (EPC-QR, "girocode") op de factuur |
| PdfPig 0.1.16 (Apache-2.0) | Tekst uit PDF-bonnen lezen (datum en bedrag) voor het postvak bonnen |
| System.Security.Cryptography.Pkcs | Vastgezet op een gepatchte versie; PDFsharp 6.2.4 trekt anders een kwetsbare versie mee |
| Google.Apis.Gmail.v1, Google.Apis.Auth (Apache-2.0) | E-mail versturen via de Gmail-API met OAuth (alleen versturen); zie `docs/GOOGLE-CLOUD.md` |
| MimeKit (MIT) | Opbouw van de e-mail (tekst en PDF-bijlage) |
| Serilog (+ Extensions.Hosting, Sinks.File) | Logging naar bestand |
| xUnit, FlaUI.UIA3 | Unit- en UI-tests |

Versies staan centraal in `Directory.Packages.props`.
