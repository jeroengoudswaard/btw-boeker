# Release notes

De opmaak volgt [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/) en de versienummering [SemVer](https://semver.org/lang/nl/).

## [Onuitgebracht]

## [0.18.0] - 2026-11-05: Alleen zichtbaar wat nu nodig is (besluiten 269–272)

### Gewijzigd
- **Navigatie** (besluit 269): per onderdeel alleen het dagelijkse werk; de inrichting staat onder een inklapbare regel *Meer…* (standaard ingeklapt).
  - Boekhouding: Journaal, Bank, Postvak, Facturen en Rapporten; onder *Meer…* Boekingsregels, Relaties, Rekeningschema, Grootboekkaart en Boekjaren.
  - Contributie: Leden en Runs, plus *Openstaand* alleen als er iets openstaat (of de pagina open is); onder *Meer…* Betalers en Tarieven.
  - Webapp: zonder koppeling alleen *Koppelen*; gekoppeld Voorstellen, met Leiding en Synchroniseren onder *Meer…*.
  - Opent u een pagina onder *Meer…* (bijv. via een startkaart of de laatste sessie), dan klapt *Meer…* vanzelf uit.
- **Knoppen verbergen in plaats van uitschakelen** (besluit 270); een knop die blijft staan toont het aantal:
  - Bank: *Concepten boeken (3)* is de hoofdactie zodra er concepten zijn, *Regels toepassen (2)* alleen bij te verwerken transacties; *Afschrift inlezen…* is een gewone knop.
  - Journaal: filterknop *N zonder bon* alleen als er zulke posten zijn; bij een post zonder bijlagen een link *+ Bon toevoegen*.
  - Werkbalk: jaarkeuze alleen bij meer dan één boekjaar; *Afdrukken* en *Exporteren ▾* (Excel of CSV) alleen bij Rapporten, Grootboekkaart, Begroting en Meerjarenplan.
  - Rapporten: de ⚠-melding over conceptposten verschijnt alleen als er concepten zijn, met het aantal.
  - Boekingsregels: de acties alleen bij een gekozen regel. Rekeningschema: rubriek toevoegen via een link *+ Rubriek*.
  - Contributie › Openstaand: de knoppen alleen bij een gekozen post; *Afboeken als oninbaar…* vraagt de kostenrekening pas in de bevestiging (niet meer in de werkbalk). Runs: *Annuleren* alleen bij een definitieve, nog niet ontvangen run. Tarieven: *Kopiëren van vorig jaar* alleen als dat kan. Contributie › Leden: filter *Alleen waarschuwingen* alleen als er waarschuwingen zijn.
  - Leden: *Ook uitgeschreven* alleen als er uitgeschreven leden zijn; de knop *Vernieuwen* is weg (F5 werkt nog).
  - Webapp › Voorstellen: *Goedkeuren* alleen als het voorstel goed te keuren is.
- **Facturen** (besluit 271): de acties per status van de gekozen factuur: concept *Verwijderen*, definitief *Mailen…*, *PDF…* en *Creditfactuur…*, vervallen ook *Herinnering sturen…*.
- Sneltoetsen, het Expert-menu en de lege schermen werken zoals voorheen.

### Afwijkingen van het ontwerpcanvas
- Het filter bij Facturen blijft altijd zichtbaar: het staat standaard op *Open*, zodat betaalde facturen anders niet te vinden zijn.
- De bankrekeningkeuze blijft een keuzelijst (verschijnt alleen bij meer dan één bankrekening) in plaats van een knoppengroep.
- De factuuracties staan onder de lijst bij de gekozen factuur, niet in een aparte detailkaart.
- Aantallen staan tussen haakjes achter de knoptekst (*Concepten boeken (3)*), zodat de toegangstoets en de woordenlijst gelijk blijven.
- Dialoogknoppen die pas werken na geldige invoer (bijv. *Herstellen*, *Aanmaken*) blijven uitgeschakeld, zoals gebruikelijk in Windows.

## [0.17.0] - 2026-11-05: Jaarrekeningindeling (besluiten 263–268)

### Toegevoegd
- **Jaarrekening volgens een eigen indeling** (besluiten 69, 151 en 263–268): in *Boekhouding › Rapporten* de keuzes *Jaarrekening: balans* en *Jaarrekening: baten en lasten*.
  - Kolommen: werkelijk dit jaar, begroot (alleen bij baten en lasten) en werkelijk vorig jaar. Bij de Stichting komt begroot uit het meerjarenplan.
  - *Rekeningen tonen* (standaard aan) zet de rekeningen onder elke regel.
  - Een ⚠-melding noemt rekeningen die in geen regel vallen; ze staan onder *Niet ingedeeld* en tellen mee in de totalen.
  - Afdrukken (Ctrl+P) en exporteren naar CSV en Excel zoals de andere rapporten.
- **Venster *Indeling bewerken…***: per deel (activa en passiva, of baten en lasten) kopjes, regels met één of meer rubrieken en subtotalen; omhoog en omlaag (Alt+↑/↓), verwijderen (Delete) en *Eén regel per rubriek* om opnieuw te beginnen. Een rubriek die al bij een andere regel van hetzelfde deel staat, is grijs.
- **Sjabloonbestand**: een geëxporteerd rekeningschema neemt de eigen indeling mee; bij inrichten met zo'n sjabloon wordt hij overgenomen.

### Gewijzigd
- De bestaande rapporten *Balans* en *Staat van baten en lasten* blijven ongewijzigd naast de jaarrekening (besluit 268). Zonder eigen indeling geeft de jaarrekening dezelfde indeling: één regel per rubriek.

## [0.16.1] - 2026-11-05: Kleine verbeteringen (besluiten 261 en 262)

### Gewijzigd
- **Webapp:** de balk van de geïnstalleerde app heeft het donkerblauw van het app-icoon (`theme_color` `#0B1F4B`, was groen) (besluit 261).
- **GitHub Actions** bijgewerkt naar versies op Node 24 (besluit 262): checkout v7, setup-dotnet v5, setup-node v7, upload-artifact v7 en download-artifact v8. De waarschuwing over Node 20 bij elke run is daarmee weg. setup-node gebruikt geen automatische cache.

### Opgelost
- **Twee ViewModel-tests** (`ShellViewModelTests`) gebruikten een Windows-pad (`C:\Data\…`) en faalden daardoor in de Linux-cloudsessie; het pad is nu platformonafhankelijk, zodat de hele suite overal groen kan zijn.

## [0.16.0] - 2026-11-04: Auditfile (XAF) en webapp-icoon (besluiten 255–260)

### Toegevoegd
- **Auditfile Financieel (XAF 3.2)** (besluiten 197 en 255–259): *Bestand › Exporteren › Auditfile (XAF)…* en een knop per boekjaar bij *Boekhouding › Boekjaren*. Eén bestand per administratie en boekjaar met:
  - rekeningschema, dagboeken en perioden;
  - de openingsbalans apart, en alle geboekte posten per dagboek;
  - relaties en betalers van de contributie als debiteuren en crediteuren, met IBAN.
  Conceptposten gaan niet mee (⚠-melding met het aantal); een historisch boekjaar kan niet. Elk bestand wordt gecontroleerd tegen het officiële schema van XAF 3.2.

### Gewijzigd
- **Webapp:** het inlogscherm toont het echte app-icoon in plaats van een vereenvoudigd vinkje (besluit 260).
- **Schermafbeeldingen voor de website:** de test maximaliseert het venster in plaats van het op 1280×800 te zetten. Op de GitHub-runner (scherm 1024×768) viel het venster anders rechts buiten beeld en onder de taakbalk.

## [0.15.0] - 2026-11-04: Publiceren (besluiten 250–254)

### Toegevoegd
- **Installatiebestand (MSI)** (besluiten 250 en 251): `installer/` (WiX 5) met de self-contained publicatie voor win-x64, dus zonder losse .NET-installatie. Installeert per computer in *Program Files\Verenigd*, met een snelkoppeling in het menu Start; een nieuwe versie vervangt de oude, de database en instellingen blijven staan.
- **Release-workflow** `.github/workflows/release.yml`: bij een versietag (`v0.15.0`) bouwen en testen op Windows, de MSI maken en samen met de webapp publiceren naar de release `verenigd` op de website; de website wordt daarna opnieuw gepubliceerd. Bij een pull request dat het installatiebestand wijzigt alleen bouwen. Zie docs/PUBLICEREN.md.
- **Schermafbeeldingen voor de website** (besluit 252): een demodatabase met verzonnen gegevens (`Demodatabase`, getest) en de UI-test `Schermafbeeldingen_voor_de_website_uit_de_demodatabase`, in licht en donker; alleen met `VERENIGD_WEBSITE_SCHERMAFBEELDINGEN`. De webapp-schermen komen uit `schermen.spec.ts` (`SCHERMEN=<map>`).

### Gewijzigd
- **Webapp op GitHub Pages** (besluit 254):
  - bouwt met `BASE_PATH` (bijv. `/verenigd/app`) voor een submap; alle links gaan via `pad()`;
  - elke pagina wordt vooraf als bestand gebouwd, zodat een directe link of verversen ook zonder terugvalpagina werkt; een lid staat daarom in de zoekparameter (`/lid?id=…`, was `/lid/<id>`);
  - een Content-Security-Policy in de pagina (`<meta>`), met alleen de ingestelde Supabase-server; frame-ancestors en Permissions-Policy blijven via `static/_headers` (Cloudflare);
  - zonder ingestelde server toont het inlogscherm dat de server nog niet is ingericht (inloggen uitgeschakeld);
  - de vervolgstap na de MFA-code accepteert alleen een pad binnen de app (geen `//andere-site`).

## [0.14.1] - 2026-11-04: Webapp in dezelfde stijl (besluiten 243 en 248)

### Gewijzigd
- **Webapp:** dezelfde kleuren, letterschaal en maten als de desktop-app: grond, kaarten met een rand, vijf lettergroottes (32, 24, 16, 14, 12), afstanden op het raster en rondingen van 8 px.
- **Initialen** in de kleur van de groep, precies dezelfde als in de desktop-app (zelfde berekening, met een test aan beide kanten).
- **Menubalk onderin:** het actieve onderdeel in zijn eigen kleur (Leden koraal, Voorstellen groenblauw, Account grijs).
- Test `ontwerp.test.ts` bewaakt lettergroottes, rondingen en afstanden in de webapp.
- **Bellen, Bericht en E-mail** (besluit 249): het keuzeblad toont alle mogelijkheden: voogd 1, voogd 2 en het lid zelf, met mobiel en vast als aparte regels. Is er maar één mogelijkheid, dan gaat het direct. Een bericht gaat alleen naar mobiele nummers.

## [0.14.0] - 2026-11-03: Kleur en rust (besluiten 239–247)

### Gewijzigd
- **Ontwerpsysteem** (besluiten 239–240):
  - kleur met betekenis: de administratie houdt de accentkleur, elk onderdeel krijgt een eigen kleur (Leden koraal, Contributie amber, Boekhouding indigo, Begroting violet, E-mail blauw, Webapp groenblauw), in het pictogram en als band boven de pagina;
  - vijf lettergroottes (32, 24, 16, 14, 12), afstanden op een raster van 4 tot 48 px, rondingen 4 en 8 px; drie architectuurtests bewaken dit.
- **Elke pagina dezelfde kop** (besluit 242): titel, één zin uitleg en de administratie, uit de schil.
- **Welkomstscherm** (besluit 241): drie even grote tegels, *Nieuwe database* als enige gekleurd, en *Laatst geopend* met de laatst gebruikte database.
- **Start:** kerncijfers en kaarten even breed; het getal op een kaart in de kleur van het onderdeel.
- **Leden:** gekleurde initialen per groep. De knoppen onderaan een venster zijn even breed.
- **Startwizard** (besluit 245): de tarieven vult u in de stap Contributie zelf in; de wizard stuurt niet meer naar Contributie › Tarieven.
- **Beginsaldi** (besluit 244): een kader dat dit definitief is (daarna alleen memoriaalboekingen), uitleg over debet en credit, en een bevestiging met het beginjaar en de totalen.
- **Categorie van een lid** (besluit 246): bij het wijzigen de keuze *Correctie* (vanaf Lid sinds, vervangt de oude) of *Wissel* (vanaf de 1e van de volgende maand); *Correctie* is de standaard zolang er met de oude categorie niets is geïncasseerd.

### Opgelost
- **Lid sinds bleef leeg** als de functies in export 4 geen startdatum hadden: de *Startdatum functie* uit export 1 wordt nu ingelezen als terugval (besluit 247, migratie *StartdatumLid*). Bestaande leden krijgen die bij de volgende SOL3-import.

## [0.13.1] - 2026-11-03: Webapp-pakketten bijgewerkt

### Gewijzigd
- **Webapp:** SvelteKit 3.1, adapter-static 4.0, vite-plugin-svelte 7.3, Vite 8.3, Vitest 4.1 en TypeScript 6.0 (was SvelteKit 2.70, Vite 6.4, Vitest 3.2, TypeScript 5.9). De schermen en het gedrag zijn ongewijzigd.
  - De instellingen staan nu in `vite.config.ts` (`svelte.config.js` is vervallen); `$lib` heet `#lib` en imports noemen de extensie.
- **CI:** `npm audit --omit=dev --audit-level=high` in de job `webapp-en-database`.

### Beveiliging
- `npm audit` van de webapp: de kritieke melding (`tinypool`) en de gemiddelde meldingen (`@vitest/mocker`, `cookie`) zijn weg. Er blijven 4 meldingen met de laagste ernst over, alle 4 door `deepmerge` in de Vite-plugin van Svelte; daarvoor bestaat nog geen oplossing. Alles zat in ontwikkel- en testpakketten: wat bij de gebruikers komt had en heeft 0 meldingen (`npm audit --omit=dev`).

## [0.13.0] - 2026-11-03: Sneller (besluiten 234–237)

### Gewijzigd
- **Verbindingen hergebruiken** (besluit 236): de versleutelde database leidt de sleutel niet meer bij elke query opnieuw af. Bij sluiten wordt de pool geleegd, zodat het bestand direct vrij is.
- **Boekhouding één keer laden:** de boekhouding wordt per administratie bewaard en pas opnieuw geladen na een wijziging. Start laadt elke administratie één keer (was tot 5 keer).
- **Lijsten in één keer vullen:** 26 lijsten melden een nieuwe inhoud in één keer aan het scherm in plaats van per regel.

### Toegevoegd
- **Meetpunten** (besluit 234): de duur van aanmaken, openen, navigeren, wisselen, Start, boekhouding laden en overzichten staat in het logbestand (regels "Duur …", zonder persoonsgegevens).
- Prestatietests op een kunstmatige database met 3000 posten, en architectuurtests voor de pool en de wijzigingsteller.

### Metingen (Linux-cloudsessie, kunstmatige database met 3000 posten, gemiddelde van 2 runs)

| Handeling | v0.12.0 | v0.13.0 |
|---|---|---|
| Verbinding openen (lege database) | 48 ms | 4 ms |
| Database openen met migraties (lege database) | 2,7 s | 0,5 s |
| Boekhouding laden, eerste keer | 1,8 s | 0,7 s |
| Boekhouding laden, zonder wijziging sindsdien | 1,8 s | 0 ms |
| Startkaarten en kerncijfers | 5,3 s | 0,2 s |
| Eén post boeken¹ | 0,35 s | 0,25 s |
| Saldibalans, balans, W&V | ±30 ms | ±30 ms |

¹ Gemeten met volledig geoptimaliseerde JIT (`DOTNET_TieredCompilation=0`). Met de standaardinstelling lijkt boeken in de test trager (0,30 → 0,53 s), omdat v0.12.0 vooraf veel vaker laadde en de code daardoor al opgewarmd was.

## [0.12.0] - 2026-11-03: Webapp voor de leiding (fase 7)

### Toegevoegd
- **Webapp voor de leiding** (besluiten 33, 222–231), als statische site op Cloudflare Pages, te installeren op de telefoon:
  - inloggen met e-mail, wachtwoord en de code uit een authenticator-app (MFA verplicht);
  - na de uitnodiging zelf een wachtwoord kiezen en de app koppelen;
  - de ledenlijst van de eigen speltak, met de leiding apart en zoeken;
  - per lid de voogden en contactgegevens, met *Bellen*, *Bericht* en *E-mail* (bij twee voogden eerst een keuze);
  - *Wijzigen* maakt een voorstel per veld; *Voorstellen* toont de status (wacht, goedgekeurd, afgewezen);
  - licht en donker.
- **Supabase** (besluiten 35–38): database met beveiliging per speltak (Row-Level Security), alleen een kopie van ledengegevens zonder adres en zonder financiële gegevens; Edge Function voor uitnodigen en intrekken. Handleiding: `docs/SUPABASE.md`.
- **Pagina Webapp** in de desktop-app (besluiten 34, 230, 232):
  - *Voorstellen*: per veld goedkeuren of afwijzen, met een waarschuwing als Verenigd intussen iets anders heeft; goedkeuren wijzigt het lid met bron *leiding*;
  - *Leiding*: uitnodigen per speltak, speltakken wijzigen en toegang intrekken;
  - *Synchroniseren*: project invullen, inloggen als beheerder (wachtwoord en code), nu synchroniseren; ook automatisch bij het openen van de database.
- CI-job voor de beveiligingstests (PostgreSQL) en de webapp.
- 1048 geautomatiseerde tests (was 911): .NET 972 (Domain 258, Application 349, Infrastructure 86, Architectuur 29, App 250 inclusief de nieuwe UI-test van de pagina Webapp), beveiliging en Edge Function 47, webapp 29 (25 Vitest en 4 Playwright).

## [0.11.0] - 2026-10-31: Nieuw uiterlijk (Fluent Mica)

### Gewijzigd
- **Uiterlijk Fluent Mica** (besluit 216), dicht bij Windows 11:
  - lichte navigatiebalk; de gekozen regel heeft een vlak en een accentstreepje;
  - een paginavlak met een afgeronde hoek, kaarten met een rand, ruimere marges (40 px) en Segoe UI Variable.
  - Licht en donker volgen het Fluent-thema (Beeld › Thema).
- **Accentkleur per administratie:** knoppen, streepje en tellers volgen de kleur van de gekozen administratie in plaats van de Windows-accentkleur.
- **Eigen titelbalk** (besluit 221) met pictogram, titel en de vensterknoppen Minimaliseren, Maximaliseren/Herstellen en Sluiten. Slepen, dubbelklikken, Snap (Win+pijl, slepen naar de rand) en Alt+spatie blijven werken.
- **Instellingen in tabs** (besluit 219): Administraties, Incasso, Facturen, E-mail en Groepen. *Opslaan* onderaan geldt voor alle tabs.
- **De band met de administratie** is een rustige regel boven de pagina, met een gekleurde stip.

### Toegevoegd
- **Kerncijfers op Start** (besluit 217), voor de gekozen administratie:
  - per bankrekening het saldo (volgens het laatste afschrift, anders volgens het grootboek);
  - de openstaande contributie met het aantal storneringen (alleen de Vereniging);
  - het resultaat van het jaar tot vandaag, naast de begroting naar rato.
- **Tellers in de navigatiebalk** (besluit 220): bijvoorbeeld *Bank 3* en *Postvak 2*; een ingeklapte module telt de onderdelen op.
- Architectuurtest: een test met een time-out is altijd asynchroon (xUnit voert een `void`-test met `Timeout` niet uit).
- 911 geautomatiseerde tests (was 905).

## [0.10.1] - 2026-10-31: Drempel en groepsindeling

### Toegevoegd
- **Drempel "zonder bon" per administratie** (besluit 214) bij Instellingen › Administraties, standaard € 10. Het filter *Zonder bon* in Boekhouding en de startkaart gebruiken de drempel van de eigen administratie.
- **Instellingen › Groepen** (besluit 215): per groep het soort (speltak, bestuur, technisch, overig), ook na de import te wijzigen.

## [0.10.0] - 2026-10-31: Stichting volwaardig

### Toegevoegd
- **Boekhouding, bank en facturen voor de Stichting** (besluiten 209 en 210, vervangen 61 en 107).
  - **Instellingen › Facturen:** per administratie eigen gegevens (naam, adres, KvK, IBAN, termijn, voettekst, logo); de factuur-PDF en de QR-code gebruiken die van de eigen administratie.
  - **Startwizard › Bank:** kies Vereniging of Stichting. Het klaaroverzicht toont ook de boekhouding en bank van de Stichting (optioneel).
- **Gmail per administratie** (besluit 211).
  - **Instellingen › E-mail:** per administratie een eigen afzender, clientbestand en koppeling.
  - **E-mail › Opstellen:** kies bij *Van* de afzender, standaard de Vereniging. Een factuur van de Stichting gaat via de Stichting.
  - De daglimiet geldt per afzender; *Verzonden* toont de kolom *Van*.

### Gewijzigd
- Migraties *EmailPerAdministratie* (logboek) en *ZonderBonDrempel* (administraties). De bestaande koppeling met Gmail blijft die van de Vereniging.
- 905 geautomatiseerde tests (was 890).

## [0.9.0] - 2026-10-30: E-mail via Gmail

### Toegevoegd
- **Pagina E-mail** (fase 6, besluiten 191–196 en 203–208) met de onderdelen *Opstellen*, *Verzonden* en *Sjablonen* in de navigatiebalk (Ctrl+6).
  - **Opstellen:** kies groepen (speltakken, bestuur, overig) en de ontvangers per bericht: standaard, alleen voogden, alleen leden zelf, of voogden én leden. Standaard krijgen de voogden de e-mail bij leden met een actieve jeugdlidfunctie, en anders het lid zelf. Adressen worden ontdubbeld; wie onder één adres valt, staat samen in `{voornaam}` ("Anna en Bo"). Leden zonder geldig adres worden gemeld.
  - Kies een sjabloon of schrijf een eigen tekst. Het voorbeeld toont de e-mail zoals de gekozen ontvanger hem krijgt; een onbekend invulveld geeft een waarschuwing. Met *Proef naar mezelf* gaat het voorbeeld naar uw eigen adres.
  - *Versturen* vraagt eerst om bevestiging en stuurt elke ontvanger een eigen, persoonlijk ingevulde e-mail (geen BCC). Wat niet verstuurd kon worden, staat met de reden onder het bericht. Bij de daglimiet van 500 berichten stopt Verenigd en meldt hoeveel berichten niet verstuurd zijn.
  - **Verzonden:** per bericht aan wie, wanneer, het onderwerp, het sjabloon en de status; de tekst zelf wordt niet bewaard.
  - **Sjablonen:** zes standaardsjablonen (Mededeling speltak, Welkomstmail, Vooraankondiging incasso, Bericht bij stornering, Factuur, Herinnering factuur). Ze zijn aan te passen maar niet te verwijderen. U kunt eigen sjablonen toevoegen; per soort staat ernaast welke invulvelden er zijn, met uitleg.
- **Berichten vanuit de modules:**
  - **Welkomstmail** na een SOL3-import met nieuwe leden, alleen voor die leden (besluit 205).
  - **Vooraankondiging** direct na *Definitief maken* van een incassorun, per betaler met bedrag, uitvoerdatum en mandaatkenmerk (besluit 206).
  - **Bericht mailen** bij een stornering in Contributie › Openstaand, aan de voogden en leden onder die betaler (besluit 204).
  - **Factuur mailen** na *Definitief maken*, met de PDF als bijlage, plus de knoppen *Mailen…* en *Herinnering sturen…* (bij een vervallen factuur) in Boekhouding › Facturen (besluit 207).
- **Instellingen › E-mail:** uw naam en Gmail-adres als afzender, *Clientbestand inlezen…*, *Koppelen met Google…* en *Ontkoppelen…*. Verenigd vraagt alleen toestemming om te versturen, met de Gmail-API en OAuth; er staat geen wachtwoord in de app. Adres en sleutels staan versleuteld in de database, niet in Git.
- Handleiding `docs/GOOGLE-CLOUD.md` voor het eenmalige Google Cloud-project: Gmail-API, toestemmingsscherm *In productie* en een OAuth-client voor een desktop-app.
- Nieuwe pakketten: Google.Apis.Gmail.v1 en Google.Apis.Auth (Apache-2.0), MimeKit (MIT).
- 889 geautomatiseerde tests (was 842).

### Opgelost
- UI-tests typen het wachtwoord alleen als de app de toetsenbordfocus heeft, anders stoppen ze. De navigatiebalk wordt bij uitklappen bijgewerkt in plaats van opnieuw opgebouwd, zodat de gekozen regel en de focus blijven (na de Windows-run van v0.8.0).

## [0.8.0] - 2026-10-30: Herinrichting van de app

### Gewijzigd
- **Navigatiebalk in plaats van tabbladen** (besluiten 168, 178 en 181): links Start, Leden, Contributie, Boekhouding, Begroting (bij de Stichting: Meerjarenplan), E-mail en Instellingen. De onderdelen van Boekhouding en Contributie klappen uit onder hun module en zijn met één klik bereikbaar; de tabkoppen op de pagina's zijn weg. Ctrl+Tab en Ctrl+Shift+Tab gaan naar de volgende of vorige regel.
- **Eén wisselaar voor de administratie** (besluiten 177 en 182), links bovenaan, met de accentkleur. Hij geldt voor de hele app; bij wisselen blijft dezelfde pagina staan, en bestaat die bij de andere administratie niet (Contributie bij de Stichting), dan gaat u naar Start. Ctrl+1…6 openen bij de gekozen administratie.
- **Herstarten** opent de laatste pagina en administratie (vervangt het herstellen van tabbladen, besluit 12).
- **Menubalk** (besluit 179): in Expert zichtbaar; in Eenvoudig verborgen en met Alt tijdelijk op te roepen. Alle sneltoetsen blijven werken.
- **Eén hoofdactie per pagina** (besluit 168): de blauwe knop is de belangrijkste actie (bijvoorbeeld *Nieuwe post*, *Bankafschrift inlezen*, *Bonnen toevoegen*, *Nieuwe factuur*). *Opslaan* is alleen blauw waar opslaan de hoofdactie is (begroting, meerjarenplan, categorieën, tarieven).
- **Statusregel** met ✓ (gelukt), ⚠ (waarschuwing) of ✕ (fout) en de melding van de pagina.

### Toegevoegd
- **Startpagina met kaarten** (besluit 183): inrichting afmaken, banktransacties te verwerken, conceptposten, bonnen in het postvak, incassorun van deze maand, openstaande storneringen, vervallen facturen en boekingen zonder bon. Een kaart verschijnt alleen als er iets te doen is; de knop gaat naar de juiste pagina.
- **Lege schermen**: een lege lijst (journaal, bank, postvak, boekingsregels, facturen, relaties, leden, betalers, incassoruns, openstaand) toont uitleg en één knop.
- **Beginjaar kiezen** bij het inrichten met het standaardschema of een sjabloonbestand (besluit 198): ook een eerder jaar, met beginsaldi per 1 januari van dat jaar.
- Architectuurtests: hooguit één hoofdactie per pagina; elke hoofdlijst heeft een leeg scherm.
- 842 geautomatiseerde tests (was 816).

## [0.7.1] - 2026-10-30: Uitleg over de SOL3-exports

### Toegevoegd
- **Uitleg over de vier SOL3-exports** (besluiten 185–187), in de stap *Leden* van de startwizard en in stap 1 van de SOL3-importwizard (uitklapbaar onder *Welke exports en kolommen?*):
  - per export het doel en alle kolommen in de volgorde van SOL3, met *(optioneel)* bij kolommen die Verenigd niet gebruikt;
  - een knop *Kolomnamen kopiëren* per export (één naam per regel);
  - de klikroute in SOL3 (Meer › Rapporten › Nieuw rapport … Opslaan als "invoer voor Verenigd, output #1–4" en Exporteren met "Alleen details", CSV), zoals aangeleverd door de gebruiker;
  - het formaat: Windows-1252 of UTF-8, datums als dag-maand-jaar, kolomvolgorde vrij.
- **SOL3-import leest komma's én puntkomma's** (besluit 190): het scheidingsteken wordt herkend aan de kopregel. SOL3 noemt de export "door komma's gescheiden", maar levert puntkomma's; een komma binnen aanhalingstekens blijft gewoon in het veld.
- Welke kolommen verplicht zijn, komt uit dezelfde bron als de import; een test bewaakt dat de uitleg en de import gelijk blijven.
- 816 geautomatiseerde tests (was 805).

## [0.7.0] - 2026-10-30: Startwizard

### Toegevoegd
- **Startwizard *Verenigd instellen*** (besluiten 169–175): Welkom → Database → Vereniging → Leden → Boekhouding → Bank → Contributie → Klaar.
  - Opent automatisch bij de allereerste start (één keer) en daarna altijd via *Bestand › Startwizard…*.
  - Elke stap is over te slaan. *Later verder* (ook ✕) onthoudt de stap in de database; bij het openen van die database biedt Verenigd aan om daar verder te gaan.
  - Gebruikt de bestaande wizards: nieuwe database met herstelsleutel, SOL3-import, Excel-overname, standaardschema, sjabloonbestand, leeg beginnen en bankimport.
  - Naam en kleur van de administraties en de incassogegevens vult u in de wizard zelf in; *Volgende* slaat ze op. *Tarieven invullen…* opent Contributie › Tarieven.
  - Een stappenbalk met ✓ (klaar), ● (huidige stap) en ○ (nog te doen). Het ✓ volgt uit de gegevens zelf, dus ook bij een bestaande database ziet u wat al is ingericht ("Al ingericht").
  - De stap *Klaar* toont per onderdeel ✓ of ⚠, met waar u het later kunt doen.
- 805 geautomatiseerde tests (was 791).

### Gewijzigd
- Naam en kleur van een administratie en de incassogegevens staan in gedeelde onderdelen, gebruikt door Instellingen en de startwizard.
- Dialogen openen boven het actieve venster (bijvoorbeeld de startwizard) in plaats van altijd boven het hoofdvenster.

### Opgelost
- De kleurrondjes bij *Extra › Instellingen › Administraties* deden niets: de knop zocht de opdracht bij het verkeerde onderdeel.

## [0.6.0] - 2026-10-30: Meerjarenplan Stichting (fase 5b)

### Toegevoegd
- **Subtabblad *Meerjarenplan* in Boekhouding van de Stichting** (besluiten 159–167):
  - Een raster met per resultaatrekening van de Stichting een bedrag per jaar, over 10 jaar, met een toelichting per rij. Bedragen in de prijzen van het jaar zelf (geen indexatie).
  - Het startjaar is vrij te kiezen, standaard het volgende jaar; een nieuw plan begint leeg.
  - Per rij een knop → *Naar de volgende jaren kopiëren*: het bedrag van het jaar bij *Kopiëren vanaf* gaat naar alle latere jaren; daarna blijft elk bedrag aan te passen.
  - Onderaan het resultaat (baten min lasten) per jaar.
  - Status *Concept* → *Vastgesteld* (alleen-lezen); *Herzien* maakt een nieuwe conceptversie, de vastgestelde versie blijft bewaard.
  - Ctrl+S slaat op; Ctrl+P drukt het plan liggend af op A4, met alle 10 jaren op één pagina.
- **Ctrl+5 / *Modules › Begroting*** opent bij de Stichting het meerjarenplan en bij de Vereniging de begroting (besluit 163).
- Architectuurtest: ook het meerjarenplan telt nooit mee in grootboek, jaarafsluiting of bank.
- 791 geautomatiseerde tests (was 774).

### Gewijzigd
- App-icoon zonder lege rand: de tegel vult nu het hele vierkant, zodat het icoon in de taakbalk even groot is als andere apps (besluit 158).

## [0.5.0] - 2026-10-29: Begroting Vereniging (fase 5a)

### Toegevoegd
- **Subtabblad *Begroting* in Boekhouding** (besluiten 146–155):
  - Per jaar een bedrag per resultaatrekening (baten en lasten), met een toelichting per regel.
  - Een kolom *Vorig jaar*, *Werkelijk* (tot en met vandaag), *Naar rato* (het jaarbedrag naar het deel van het jaar dat voorbij is, per dag) en *Verschil* (rood bij een ongunstig verschil). Een totaalregel toont baten, lasten en resultaat.
  - *Overnemen* van de werkelijke cijfers of de begroting van een ander jaar, met een percentage erbij en afgerond op tientallen euro's. Daarna kun je elk bedrag zelf aanpassen.
  - Status *Concept* → *Vastgesteld* (alleen-lezen, met datum en omschrijving). *Herzien* maakt een nieuwe conceptversie; de vastgestelde versie blijft bewaard. Een keuzelijst toont alle versies.
  - Ctrl+S slaat de bedragen op; Ctrl+P drukt de begroting voor de ALV af (per rubriek met subtotalen en *Werkelijk* twee jaar terug, *Begroot* vorig jaar en *Begroot* dit jaar). Export naar xlsx werkt ook.
- **Rapport *Begroting en werkelijk*** bij Rapporten.
- **Kolom *Baten/lasten*** in het Rekeningschema (besluit 152). Bestaande rekeningen krijgen een voorstel uit het saldo (zonder afsluitposten, inclusief de historie); zonder saldo geldt vanaf 8000 baten. Ook de Excel-overname en sjabloonbestanden kennen de kant.
- Weergave Eenvoudig: Begroot → *Verwacht*, Werkelijk → *Uitgegeven/ontvangen*, Naar rato → *Verwacht tot nu toe*, Baten/lasten → *Inkomst/uitgave*.
- **App-icoon** (besluiten 156 en 158): het vinkje-icoon (versie 3) in alle Windows-formaten (16–256 px) voor programmabestand, taakbalk en titelbalken, met echte transparantie rond de afgeronde tegel. De Android-iconen staan voor later in `assets/android/`, de bron in `assets/bron/`.
- ***Modules › Begroting* (Ctrl+5)** opent het tabblad Boekhouding op het subtabblad Begroting (besluit 157); de placeholder uit fase 1 is vervallen.
- Architectuurtests: de begroting telt nooit mee in grootboek, jaarafsluiting of bank; het app-icoon bevat alle formaten.
- 774 geautomatiseerde tests (was 740).

## [0.4.10] - 2026-10-29: Bijlagen (stap 4c-3)

### Toegevoegd
- **Bijlagen bij een post** (besluiten 83–86, 136, 137 en 140): een blok *Bijlagen* onder de regels in de post-editor met *Bijlage toevoegen…*, slepen, *Bekijken*, *Opslaan als…* en *Verwijderen*. Werkt ook bij een geboekte post; de boeking zelf verandert niet. Verwijderen vraagt altijd om bevestiging.
- **Paperclip-kolom** in het Journaal voor posten met een bijlage.
- **Filter *Zonder bon*** in het Journaal (besluiten 88, 97 en 138): uitgaven van bank of kas boven € 10 zonder bijlage.
- **Subtabblad *Postvak bonnen*** (besluiten 85 en 96): bonnen toevoegen of erop slepen; van een PDF leest Verenigd datum en totaalbedrag. Een post met precies hetzelfde bedrag en een datum binnen 7 dagen wordt voorgesteld (✓); met *Koppelen* hangt de bon aan die post. Bij een foto vult u datum en bedrag zelf in. Hetzelfde bestand twee keer toevoegen geeft een melding.
- **Voorbeeldvenster** (besluiten 86 en 135): PDF's via de PDF-functie van Windows zelf, afbeeldingen direct; zoomen (Ctrl+plus/min, Ctrl+0) en *Opslaan als…* (Ctrl+S).
- Toegestaan: PDF, JPG en PNG, herkend aan de inhoud en niet aan de extensie; maximaal 10 MB per bijlage.
- Bijlagen staan versleuteld in de database en gaan mee in de back-up. Ze komen nooit onversleuteld op schijf, behalve met *Opslaan als…*. Een verwijderd concept zet zijn bijlagen terug in het postvak.
- **SOL3-import, afwijkend IBAN** (besluit 141, aanvulling op 21 en 22): levert SOL3 een ander IBAN dan de betaler in Verenigd, dan volgt een waarschuwing en een tabblad *IBAN-verschillen* met per lid *IBAN uit SOL3 overnemen* (standaard uit). Zonder vinkje blijft de koppeling in Verenigd staan. De melding toont alleen de laatste vier cijfers.
- Weergave Eenvoudig: Bijlagen heet *Bonnen*; de tooltips bij *Toewijzen* en *Tegenboeken* volgen nu ook de woordenlijst.
- 724 geautomatiseerde tests (was 679).

### Creditnota's in het postvak (besluiten 142–145)
- Een negatief bedrag op een bon blijft negatief: een minteken vóór of achter het bedrag (`-15,00`, `€ -15,00`, `15,00-`) wordt herkend, en in het postvak mag je zelf een minteken invullen.
- Een creditnota wordt voorgesteld bij een post waarbij hetzelfde bedrag op bank of kas binnenkomt.
- Het negatieve bedrag staat in groen (het minteken blijft).
- Het filter *Zonder bon* toont nu ook ontvangsten van bank of kas boven € 10 zonder bijlage.

### Tests (na de Windows-run van 2026-10-29)
- Het voorbeeldvenster wordt nu end-to-end in de echte app getest (`Bijlage_bekijken_toont_de_pdf_in_het_voorbeeldvenster`). De losse venstertest is vervangen door een test van alleen het laden van een afbeelding, omdat een venster zonder `Application` de app-resources mist.
- UI-tests beëindigen vóór toetsenbordinvoer en vóór het sluiten de menumodus (en controleren dat), zetten het venster op de voorgrond, hebben een harde time-out van 3 minuten en sluiten hun app-proces altijd af, ook als het sluiten mislukt.
- 740 geautomatiseerde tests (725 vóór de creditnota-wijziging).

### Gewijzigd
- Doelplatform van de app: `net10.0-windows10.0.19041.0` (Windows 10 versie 2004 of nieuwer), voor `Windows.Data.Pdf`. De uitvoermap is daardoor `src/Verenigd.App/bin/<Config>/net10.0-windows10.0.19041.0/Verenigd.exe`.
- Nieuwe afhankelijkheid: PdfPig 0.1.16 (Apache-2.0), alleen in Infrastructure.
- De SOL3-import neemt een afwijkend IBAN niet meer automatisch over (zie hierboven).

## [0.4.9] - 2026-10-28: Weergaven Expert en Eenvoudig

### Toegevoegd
- **Weergave Eenvoudig** voor het tabblad Financiën (besluiten 127–133), naast de bestaande weergave *Expert*:
  - Wisselen via *Beeld › Weergave › Expert / Eenvoudig*, direct en zonder herstart. De keuze wordt onthouden; een nieuwe installatie begint in *Expert*.
  - Gewone woorden, bijvoorbeeld Journaal → *Alle boekingen*, Rekeningschema → *Potjes*, Grootboekkaart → *Inhoud van een potje*, Saldibalans → *Stand van alle potjes*, Balans → *Bezittingen en schulden*, Staat van baten en lasten → *Inkomsten en uitgaven*, Concept → *Nog niet definitief*, Boeken → *Definitief maken*.
  - Lastige begrippen krijgen in Eenvoudig een uitleg als tooltip.
  - Kolomkoppen op de Grootboekkaart: inkomsten- en uitgavenpotjes *Uitgave* / *Inkomst*, geldpotjes (bank, kas) *Erbij* / *Eraf*, overige balanspotjes *Debet* / *Credit* met uitleg.
  - Ook de meldingen, de tabtitel (*Financiën*) en het menu *Modules* volgen de weergave.
- Alleen visueel: de database blijft formeel en afdrukken en export gebruiken altijd de Expert-termen.
- Een architectuurtest bewaakt dat de XAML alleen termen uit de woordenlijst gebruikt.
- 679 geautomatiseerde tests (was 661).

## [0.4.8] - 2026-10-28: Historisch boekjaar 2025

### Toegevoegd
- **Historisch boekjaar uit de Excel-boekhouding** (besluiten 122–126), als afgesloten, alleen-lezen boekjaar:
  - Verenigd leest de T-rekeningen op alle werkbladen: per rekening datum, omschrijving, debet en credit. SUM-totaalregels worden overgeslagen.
  - Regels zonder datum zijn begin- of tussensaldi. Die en regels met een datum buiten het jaar krijgen 1 januari; een oudere datum komt in de omschrijving.
  - **Voorbeeld per rekening** met het aantal regels, het saldo van de regels, het saldo volgens het Voorblad en een controle (✓, ⚠ met het verschil, *Geen regels* of *Niet op het Voorblad*). Een afwijking wordt overgenomen met een ⚠.
  - Rekeningen die niet in het rekeningschema staan: overslaan, of toevoegen als inactieve rekening.
  - **Twee manieren om te starten:** het selectievakje *Daarna ook de historie van dit jaar overnemen* in de Excel-overname (standaard aan), of *Bestand › Importeren › Historie uit Excel…* voor een al ingerichte administratie. Vooraf wordt een back-up gemaakt.
- **In het tabblad Boekhouding** bij het historische jaar:
  - Een band "Historisch boekjaar, overgenomen uit Excel, niet dubbel geboekt".
  - Het Journaal is leeg.
  - De Grootboekkaart toont de oorspronkelijke regels en *Totaal volgens Voorblad*, met een ⚠ als dat afwijkt.
  - Saldibalans, Balans en Staat van baten en lasten komen uit de totalen van het Voorblad.
  - Bij Boekjaren staat het jaar als *Historisch*; het kan niet worden heropend en er kan niet in worden geboekt.
- Een architectuurtest bewaakt dat historische regels en totalen in de applicatielaag alleen door het model, de grootboekberekeningen en de historie-import worden gebruikt. Bank, contributie, facturen en de jaarafsluiting blijven er dus buiten.
- 661 geautomatiseerde tests (was 645).

### Proef met de echte Excel-boekhouding 2025 (niet bewaard)
- 61 rekeningen en 275 regels.
- 37 rekeningen kloppen met het Voorblad; 4 wijken af (600, 1000, 1700 en 8330).
- 19 rekeningen zonder regels en 4 die niet op het Voorblad staan.

## [0.4.7] - 2026-10-28: Bevindingen uit de proefimport

### Opgelost
- **Saldibalans en andere rapporten:** de kolom *Rekening* was één letter breed, met één letter per regel. De viewer las de vaste breedte van de bedragkolommen als verhouding. Alle kolommen hebben nu een verhoudingsbreedte (tekst 3 delen, elk bedrag 1 deel); dat geldt op het scherm én bij Afdrukken.
- **Underscore zichtbaar op blauwe knoppen en selectievakjes** (bijvoorbeeld "_Toewijzen"): de accentknop en het selectievakje van het Fluent-thema herkennen geen toegangstoets. Ze tonen hun tekst nu via de sjabloon `Toegangstekst`, zodat Alt+letter weer werkt.
  - Knoppen: *Standaardschema gebruiken*, *Boeken*, *Bankafschrift inlezen*, *Toewijzen*, *Definitief maken* en *Nieuwe incassorun*.
  - Selectievakjes: *Het verschil bewust boeken* (twee wizards), *Alleen met waarschuwing*, *Alleen openstaande posten*, *Toon uitgeschreven* en de controle in de incassowizard.

### Toegevoegd
- Een architectuurtest die faalt bij een accentknop of selectievakje met toegangstoets zonder `Toegangstekst`.
- Een Windows-test die de layout van de saldibalans in een getoond venster meet.
- 645 geautomatiseerde tests (was 643).

## [0.4.6] - 2026-10-28: Toegankelijkheid van groepen

### Opgelost
- Dertien groepen in de schermen waren onzichtbaar voor schermlezers en UI Automation, waaronder de editors *Journaalpost*, *Factuur*, *Relatie* en *Betaler*, de bankdetails, *Afletteren*, de startkaart van de boekhouding en de secties Incasso en Facturen in Instellingen. Een `Border` of panel heeft in WPF geen automation peer, dus hun AutomationId en naam kwamen niet door. Gevonden door de FlaUI-test op Windows (`PostEditor` niet gevonden).
  - Deze containers zijn nu een `Groepskader`, `Groepspaneel` of `Groepsraster` (`Views/Toegankelijkheid`): dezelfde opmaak, maar voor schermlezers een groep met een Nederlandse naam, zoals "Journaalpost" of "Factuur".

### Toegevoegd
- Een architectuurtest die faalt zodra een AutomationId op een element zonder automation peer staat.
- Een test die de peers van de groepscontainers controleert (alleen op Windows). Het element staat daarbij in een getoond venster buiten beeld, omdat WPF `IsControlElement` laat afhangen van `IsVisible`. Een tweede test bevestigt dat een ingeklapte groep geen control-element is voor schermlezers.
- 643 geautomatiseerde tests (was 640).

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
