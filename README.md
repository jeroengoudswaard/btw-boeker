# BTW-Boeker

Windows-desktopapplicatie voor een eenvoudige financiële administratie met btw: dubbel boekhouden, bankafschriften (CAMT.053), relaties, bonnen als bijlage en rapporten, met als doel de btw-aangifte per maand voor te bereiden. Het eerste gebruik is de verhuur van een vakantiewoning.

| Stap | Inhoud | Status |
|------|--------|--------|
| 1 | Eigen product: naam, database (`.btwdb`), installatiebestand en icoon | ✅ Gereed |
| 2a | Eén administratie; onderdelen die niet bij verhuur horen verwijderd | ✅ Gereed |
| 2b | Rekeningschema, beginsaldi en boekingen inlezen uit een XAF-bestand (e-Boekhouden) | 🔨 In ontwerp |
| 3 | Btw: tarief per boekingsregel, prestatiedatum, invoer van nota's, maandrapport en export | Gepland |
| 4 | Documentatie, website en publicatie | Gepland |

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
  BtwBoeker.Architectuur.Tests/  Bewaakt de lagen, de productnaam en de reikwijdte
docs/
  ARCHITECTUUR.md           Ontwerpkeuzes, beveiliging en teststrategie
```

## Bestanden en privacy

| Bestand | Inhoud |
|---------|--------|
| `*.btwdb` | De database, volledig versleuteld (SQLite3 Multiple Ciphers, ChaCha20-Poly1305) |
| `*.btwdb.sleutel` | De ingepakte databasesleutel. **Hoort altijd naast de `.btwdb`.** Zonder dit bestand is de database niet te openen |
| `*.btwbackup` | Een back-up: database en sleutelbestand samen in één bestand, nog steeds versleuteld |
| `%APPDATA%\BTW-Boeker\instellingen.json` | Thema, weergave, laatst geopende database en pagina |
| `%LOCALAPPDATA%\BTW-Boeker\logs\` | Logbestanden (30 dagen), zonder persoonsgegevens |

Bankafschriften (CAMT.053), XAF-exports, nota's en bonnen zijn echte gegevens: bewaar ze in `prive/` of buiten de repository. De tests gebruiken alleen verzonnen gegevens.

Echte gegevens horen **nooit** in Git. `.gitignore` blokkeert `*.btwdb`, `*.btwbackup`, `*.xaf`, sleutelbestanden en de mappen `voorbeelden/` en `prive/`.

## Dependencies

| Pakket | Doel |
|--------|------|
| CommunityToolkit.Mvvm | MVVM (ObservableObject, RelayCommand) |
| Microsoft.Extensions.Hosting | Dependency injection en configuratie |
| Microsoft.EntityFrameworkCore.Sqlite.Core | ORM en migraties |
| SQLite3MC.PCLRaw.bundle | SQLite met versleuteling |
| ClosedXML (MIT) | Rapporten exporteren naar Excel (.xlsx) |
| PdfPig 0.1.16 (Apache-2.0) | Tekst uit PDF-bonnen lezen (datum en bedrag) voor het postvak bonnen |
| Serilog (+ Extensions.Hosting, Sinks.File) | Logging naar bestand |
| xUnit, FlaUI.UIA3 | Unit- en UI-tests |

Versies staan centraal in `Directory.Packages.props`.
