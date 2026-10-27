# Verenigd

Windows-desktopapplicatie voor de leden-, contributie- en financiële administratie van een scoutingvereniging en de bijbehorende stichting.

| Fase | Inhoud | Status |
|------|--------|--------|
| 1 | Fundament: hoofdvenster met tabbladen, versleutelde database, herstelsleutel, back-up, thema's | ✅ Gereed (v0.1.0) |
| 2 | Leden, met import uit SOL3 (scouting.nl) | ✅ Gereed (v0.2.0) |
| 3 | Contributie en SEPA-incasso (pain.008, Rabobank) | ✅ Gereed (v0.3.0) |
| 4 | Bankimport (CAMT.053, Rabobank en ING) en dubbel boekhouden | 🔨 In uitvoering: 4a Grootboek (v0.4.0) en 4b Bank (v0.4.1) gereed, 4c Contributie & relaties volgt |
| 5 | Jaarbegroting (vereniging) en meerjarenplan (stichting) | Gepland |
| 6 | E-mail via Gmail | Gepland |
| 7 | Webapp voor groepsleiders (Supabase) | Gepland |

## Vereisten

- Windows 10 (1809+) of Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.4xx), of `winget install Microsoft.DotNet.SDK.10`
- Optioneel: Visual Studio 2026 met de workload **.NET desktop development**

## Bouwen en starten

### Visual Studio
1. Open `Verenigd.slnx`.
2. Stel **Verenigd.App** in als opstartproject en druk op **F5**.
3. Tests: menu **Test › Run All Tests** (Test Explorer).

### Command line
```powershell
dotnet build                                   # bouwt alles; waarschuwingen gelden als fouten
dotnet run --project src/Verenigd.App          # start de applicatie
dotnet test --filter "Categorie!=UI"           # unit- en integratietests (snel, ook in CI)
dotnet test --filter "Categorie=UI"            # UI-tests: start de echte app; muis en toetsenbord niet gebruiken
```

Bij de UI-tests bewaart `$env:VERENIGD_SCREENSHOTS = "C:\pad"` screenshots van elke stap.

### Database-migraties
```powershell
dotnet tool restore
dotnet ef migrations add <Naam> --project src/Verenigd.Infrastructure --output-dir Database/Migraties --namespace Verenigd.Infrastructure.Database.Migraties
```
Bestaande databases worden bij het openen automatisch bijgewerkt.

## Projectstructuur

```
src/
  Verenigd.Domain/          Domeinmodel en bedrijfsregels (geen dependencies)
  Verenigd.Application/     Use cases en interfaces (database, instellingen, beveiliging)
  Verenigd.Infrastructure/  EF Core + SQLite3MC (versleuteling), sleutelbeheer, JSON-instellingen
  Verenigd.App/             WPF (Fluent-thema), MVVM met CommunityToolkit.Mvvm, Generic Host
tests/
  Verenigd.*.Tests/         xUnit-tests per laag; App.Tests bevat ook de FlaUI-UI-tests
  Verenigd.Architectuur.Tests/  Bewaakt de lagen en de scheiding leden–financiën
docs/
  ARCHITECTUUR.md           Ontwerpkeuzes, beveiliging en teststrategie
```

## Bestanden en privacy

| Bestand | Inhoud |
|---------|--------|
| `*.vdb` | De database, volledig versleuteld (SQLite3 Multiple Ciphers, ChaCha20-Poly1305) |
| `*.vdb.sleutel` | De ingepakte databasesleutel. **Hoort altijd naast de `.vdb`.** Zonder dit bestand is de database niet te openen |
| `*.vbak` | Een back-up: database en sleutelbestand samen in één bestand, nog steeds versleuteld |
| `%APPDATA%\Verenigd\instellingen.json` | Thema, laatst geopende database, open tabbladen |
| `%LOCALAPPDATA%\Verenigd\logs\` | Logbestanden (30 dagen), zonder persoonsgegevens |

SOL3-exports zijn echte persoonsgegevens: bewaar ze in `privé/` of buiten de repository. Hetzelfde geldt voor de Excel-boekhouding die u eenmalig overneemt en voor bankafschriften (CAMT.053). De tests gebruiken alleen verzonnen gegevens.

Echte gegevens horen **nooit** in Git. `.gitignore` blokkeert `*.vdb`, `*.vbak`, sleutelbestanden en de mappen `voorbeelden/` en `privé/`.

## Dependencies

| Pakket | Doel |
|--------|------|
| CommunityToolkit.Mvvm | MVVM (ObservableObject, RelayCommand) |
| Microsoft.Extensions.Hosting | Dependency injection en configuratie |
| Microsoft.EntityFrameworkCore.Sqlite.Core | ORM en migraties |
| SQLite3MC.PCLRaw.bundle | SQLite met versleuteling |
| ClosedXML (MIT) | Excel lezen (overname boekhouding) en schrijven (export rapporten) |
| Serilog (+ Extensions.Hosting, Sinks.File) | Logging naar bestand |
| xUnit, FlaUI.UIA3 | Unit- en UI-tests |

Versies staan centraal in `Directory.Packages.props`.
