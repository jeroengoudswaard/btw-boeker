# Een versie publiceren (besluiten 250–254)

Een versie van Verenigd komt op de website via de workflow **Release** (`.github/workflows/release.yml`). Die bouwt op Windows het installatiebestand (MSI), bouwt de webapp voor GitHub Pages, maakt schermafbeeldingen met verzonnen gegevens en zet alles op https://jeroengoudswaard.github.io/verenigd/.

## Eenmalig: het token voor de website

De workflow schrijft in een andere repository (de website). Daarvoor is een token nodig met alleen die rechten.

1. Ga op GitHub naar **Settings › Developer settings › Personal access tokens › Fine-grained tokens** en klik op **Generate new token**.
2. Vul in:
   - **Token name:** `verenigd-release`
   - **Expiration:** bijvoorbeeld 1 jaar (zet een herinnering om hem te vernieuwen)
   - **Repository access:** *Only select repositories* › `jeroengoudswaard/jeroengoudswaard.github.io`
   - **Permissions › Repository permissions:** *Contents* = **Read and write** (nodig voor de release en de schermafbeeldingen). De rest blijft *No access*. *Metadata* = Read-only wordt automatisch gekozen.
3. Klik op **Generate token** en kopieer het token. U ziet het maar één keer.
4. Ga in de repository **verenigd** naar **Settings › Secrets and variables › Actions › New repository secret**:
   - **Name:** `WEBSITE_TOKEN`
   - **Secret:** het gekopieerde token

Het token komt nooit in Git en staat alleen in de geheimen van GitHub.

## Een versie uitbrengen

1. Zorg dat `main` de versie bevat. Het versienummer staat in `Directory.Build.props` en `webapp/package.json`, de wijzigingen in `CHANGELOG.md`.
2. Maak een tag en push hem:

   ```
   git checkout main
   git pull
   git tag v0.15.0
   git push origin v0.15.0
   ```

   Op GitHub kan het ook: **Releases › Draft a new release › Choose a tag** `v0.15.0` (*Create new tag on publish*).
3. Volg de workflow onder **Actions › Release**. Na ±15 minuten staat de MSI op de release `verenigd` van de website, en de website wordt opnieuw gepubliceerd.

De vaste downloadlink is altijd:
`https://github.com/jeroengoudswaard/jeroengoudswaard.github.io/releases/download/verenigd/Verenigd.msi`

Daarnaast staat er een kopie met het versienummer (`Verenigd-0.15.0.msi`).

## Wat niet automatisch gaat

- **Schermafbeeldingen van de desktop:** daarvoor heeft FlaUI een bureaublad nodig. Lukt dat niet op de GitHub-runner, dan geeft de workflow een waarschuwing en blijven de vorige afbeeldingen staan. U kunt ze dan zelf maken op Windows:

  ```
  dotnet build -c Release
  $env:VERENIGD_WEBSITE_SCHERMAFBEELDINGEN = "$PWD\schermen"
  dotnet test tests/Verenigd.App.Tests -c Release --no-build --filter "Categorie=Schermafbeeldingen"
  ```

  Zet de PNG's daarna in `verenigd/screens/` van de websiterepository.
- **Codesigning:** de bèta is niet digitaal ondertekend (besluit 253). Windows SmartScreen meldt daarom "Onbekende uitgever"; de websitepagina legt uit hoe u verder gaat.
- **De koppeling van de webapp met Supabase** (besluit 254) volgt later. Stel dan in de repository **verenigd** onder **Settings › Secrets and variables › Actions › Variables** de variabelen `SUPABASE_URL` en `SUPABASE_PUBLISHABLE_KEY` in (de publieke sleutel, nooit de `service_role`-sleutel). Voeg in Supabase onder **Authentication › URL Configuration** het adres `https://jeroengoudswaard.github.io/verenigd/app/` toe aan de toegestane adressen.

## Zelf de MSI bouwen (Windows)

```
dotnet publish src/Verenigd.App -c Release -r win-x64 --self-contained -o artifacts/publish
dotnet build installer/Verenigd.Installer.wixproj -c Release -p:Versie=0.15.0 -p:PublicatieMap=%CD%\artifacts\publish
```

De MSI staat daarna in `installer/bin/x64/Release/`.
