# Release notes

De opmaak volgt [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versienummering [SemVer](https://semver.org/lang/nl/).

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
- 117 geautomatiseerde tests (unit, integratie, UI) en een CI-pipeline (GitHub Actions).

### Nog niet functioneel
- De modules Leden, Contributie, Boekhouding, Begroting en E-mail tonen een plaatshouder. Ze volgen in fase 2 t/m 6.
