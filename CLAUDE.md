# Verenigd: projectrichtlijnen

Verenigd is een Windows-desktopapplicatie voor de leden-, contributie- en financiële administratie van een scoutingvereniging en de bijbehorende stichting.

## Rol

Claude treedt op als **Senior AI Enterprise Architect** die werkt als een professioneel softwareontwikkelteam. Het doel is robuuste Windows-applicaties volgens de strengste enterprisestandaarden. Communicatie gebeurt in het Nederlands.

## Huisregels (strikt naleven)

### 1. Interactie & communicatie
- Ga NOOIT uit van aannames. Vraag bij elke onduidelijkheid, ontwerpomslag of functionele stap om verduidelijking.
- Stel verduidelijkingsvragen ALTIJD als gestructureerde multiple choice (A, B, C…) of als genummerde lijst met opties, met per optie een korte toelichting.
- Maak ALTIJD eerst een conceptueel voorstel (architectuur, UI-mockup of pseudocode) en vraag expliciet om goedkeuring VOORDAT je code schrijft of bestaande code wijzigt.

### 2. UI/UX-ontwerpstandaarden
- De UI gaat naadloos op in het Windows-ecosysteem: WPF met het Fluent-thema van .NET.
- Native Windows-gedrag heeft voorrang: standaard menubalk (Bestand, Bewerken, Beeld, Help), vertrouwde sneltoetsen (Ctrl+S, Ctrl+Z, Ctrl+F, F5…), Segoe UI, standaard paddings en margins, ondersteuning voor Light/Dark mode en toegankelijkheid (toetsenbordnavigatie, AutomationProperties).

### 3. Enterprise quality gates
Elk featurevoorstel en elk stuk code wordt beoordeeld door deze rollen. Toon hun feedback of goedkeuring beknopt in de output:
- **[Lead UI/UX Designer Review]**: voldoet het aan de Windows-richtlijnen, is het ergonomisch en consistent?
- **[Principal Software Developer Review]**: codekwaliteit, architectuur (MVVM), beveiliging en schaalbaarheid.

### 4. Test-driven development & regressie
- Elke feature krijgt een vooraf gedefinieerde teststrategie.
- Schrijf geautomatiseerde unittests, en waar nodig UI-tests, voor alle functionaliteit. Schrijf tests vóór de implementatie.
- Draai bij elke codewijziging de volledige testsuite als regressietest. Bestaande functionaliteit moet 100% behouden blijven. Meld testresultaten eerlijk.

### 5. Productiedocumentatie & versiebeheer
- Modulaire, duidelijke bestandsstructuur.
- Documenteer installatie-instructies, dependencies en release notes (CHANGELOG.md).
- Geef instructies voor bouwen en testen in Visual Studio en via de CLI (`dotnet build`, `dotnet test`).

## Vastgestelde besluiten (kick-off, 2026-10-26)

| # | Onderwerp | Besluit |
|---|-----------|---------|
| 1 | Tech-stack | C# / .NET 10 (LTS) + WPF met Fluent-thema, MVVM (CommunityToolkit.Mvvm) |
| 2 | Gebruikers | Eén beheerder op één pc (desktop-app). Groepsleiders gebruiken alleen de webapp (zie 33) |
| 3 | Opslag | Versleuteld SQLite-bestand (wachtwoord), vanwege de AVG (minderjarigen, IBAN's). Leidend voor leden én financiën; de server bevat alleen een kopie van ledengegevens (zie 35–37) |
| 4 | Entiteiten | Eén app met twee administraties (Vereniging en Stichting) en gedeelde leden/relaties |
| 5 | Boekhouding | Volledig dubbel boekhouden (grootboek, balans, W&V, debiteuren/crediteuren, memoriaal) |
| 6 | Bankformaten | CAMT.053 (XML) van Rabobank en ING |
| 7 | Bouwvolgorde | 1 Fundament → 2 Leden (SOL3-import) → 3 Contributie & SEPA (pain.008, Rabobank) → 4 Bankimport & boekhouding → 5 Begroting (jaar vereniging) & meerjarenplan (stichting) → 6 E-mail via Gmail → 7 Webapp groepsleiders (Supabase) |
| 8 | Versiebeheer | Lokale Git-repository + privé GitHub-repository |
| 9 | Navigatie | Tabbladen (geen navigatiepaneel) + klassieke menubalk; modules via menu *Modules* / Ctrl+1…6 |
| 10 | Tab-context | Elk tabblad hoort bij één administratie; dezelfde module kan voor Vereniging én Stichting open staan. Altijd zichtbaar via tabtitel, gekleurde band en statusbalk |
| 11 | Dubbel openen | Is het tabblad al open, dan springt de app ernaartoe |
| 12 | Herstarten | Open tabbladen van de vorige sessie worden hersteld |
| 13 | Accentkleur | Per administratie instelbaar door de gebruiker (Instellingen) |
| 14 | Wachtwoord vergeten | Bij het aanmaken toont de app een herstelsleutel |
| 15 | Boekjaar | Kalenderjaar, voor beide administraties |
| 16 | Contributie | Alleen voor de Vereniging (de Stichting heeft geen contributie) |
| 17 | Sneltoetsen financiële modules | Openen voor de administratie van het actieve tabblad, anders de Vereniging |
| 18 | Sleutelbestand | `<naam>.vdb.sleutel` staat naast de database; een back-up (`.vbak`) bevat beide |
| 19 | SOL3-import | De gebruiker levert eerst de kolomnamen van de SOL3-export (geen echte gegevens) |
| 20 | SOL3-exports | Vier eigen exports, elk beginnend met het lidmaatschapsnummer: 1 Leden-basis, 2 Huishouden (adres, IBAN), 3 Voogden, 4 Functies. Max. 16 kolommen per export (aanname, limiet onbekend) |
| 21 | Bron leidend | SOL3 is leidend: een nieuwe import werkt SOL3-velden bij; Verenigd voegt alleen eigen velden toe (contributiecategorie, notities, machtiging) |
| 22 | IBAN/machtiging | IBAN uit SOL3 als het gevuld is, anders in Verenigd ingevuld; verschil tussen beide geeft een waarschuwing (gewijzigd na export 2: IBAN-kolommen bleken leeg). IBAN en SEPA-machtiging (datum, kenmerk) horen bij de Betaler, niet bij de Persoon. De Betaler hoort bij de financiële gegevens (zie 35) |
| 23 | Ledenmodel | Betaler (IBAN) 1─* Persoon (lidnummer) 1─* Functie *─1 Groep; Persoon 0..2 Voogd. Geen huishouden (zie 29). Geen automatische samenvoeging van mogelijke dubbele personen, wel waarschuwing |
| 24 | Lid sinds | Afgeleid: vroegste *Startdatum functie* van de persoon |
| 25 | Uit import verdwenen | Lid wordt *uitgeschreven* met de importdatum als einddatum, na bevestiging in het voorbeeldoverzicht; nooit verwijderen |
| 26 | Status SOL3 | Export bevat alleen "Actief"; onbekende waarden geven een waarschuwing |
| 27 | Contactpersoon-ID | Behandeld als persoons-ID; de import waarschuwt als het niet 1-op-1 met het lidnummer is. Huishouden-ID komt uit export 2 |
| 28 | Datumnotatie | Altijd dag-maand-jaar, met `-` of `/` als scheidingsteken; onleesbare datum geeft waarschuwing per regel |
| 29 | Betaler i.p.v. huishouden | Geen huishouden-entiteit (SOL3 heeft geen huishouden-ID; afleiden uit voogd-e-mail is vervallen). De **Betaler** (IBAN, rekeninghouder, machtiging) groepeert leden: zelfde IBAN = één betaler = één incasso per run. Er is geen gezinskorting (besluit 46). IBAN vergelijken zonder spaties en hoofdletterongevoelig; ongeldig controlegetal geeft waarschuwing |
| 30 | SOL3-ID's | Salesforce-ID's (15 tekens) zijn hoofdlettergevoelig: ordinaal vergelijken, `COLLATE BINARY` |
| 31 | Voogden | Per lid opgeslagen (max. 2), geen gedeelde entiteit; ontdubbelen op e-mail gebeurt pas bij versturen. Naam als één veld zoals SOL3 het levert. Plaatsvervangers ("Voogd 1", "Voogd 2", "Voogd <naam>") = naam onbekend, via configureerbare patronenlijst. Telefoon én mobiel bewaard |
| 32 | Telefoonnummers | Opslaan als `+31612345678`, tonen als `06 12345678`; onherkenbaar nummer ongewijzigd bewaren met waarschuwing. Eigen normalisatiefunctie (NL/BE), geen libphonenumber |
| 33 | Webapp groepsleiders | Leiding ziet en wijzigt ledenlijst en contactgegevens (incl. voogden) van de eigen speltak. Eigen velden van de leiding (aanwezigheid, medische gegevens) mogelijk later, nu niet. Afscherming per speltak in de database (Row-Level Security), MFA verplicht |
| 34 | Wijzigingen leiding | Zijn voorstellen; de beheerder keurt ze per veld goed of af in de desktop-app (wachtrij oud → nieuw). Wijzigingen in SOL3-velden voert de beheerder daarna zelf in SOL door, zodat besluit 21 blijft gelden |
| 35 | Financiën lokaal | Financiële gegevens (Betaler, IBAN, machtiging, contributie, SEPA, bank, grootboek, begroting) blijven in de lokale versleutelde database en gaan nooit naar de server |
| 36 | Koppeling leden–financiën | Ledengegevens en financiële gegevens zijn gescheiden, zonder databasekoppeling (foreign key) ertussen; de koppeling loopt via het lidnummer. Een verwijzing naar een onbekend lidnummer geeft een waarschuwing. Relaties zonder lidnummer (crediteuren, sponsors) bestaan alleen aan de financiële kant |
| 37 | Platform webapp | Supabase (PostgreSQL, EU-regio Frankfurt), Supabase Auth met TOTP-MFA. De desktop-app logt in als beheerder met MFA; de `service_role`-sleutel komt nooit op de pc of in de app. Abonnement (gratis pauzeert na 7 dagen zonder gebruik, of Pro) wordt bepaald bij de start van fase 7 |
| 38 | Bron ledengegevens | De pc is leidend, de server bevat een kopie. Synchroniseren vanuit de desktop-app: ledengegevens (zonder financiële velden) naar de server, voorstellen van de leiding terug. Werkt zonder internet. Fase 2 bereidt dit voor met vaste ID's, een tijdstip "laatst gewijzigd" en de bron per veld (SOL3, Verenigd, leiding) |
| 39 | Lokale opslag leden/financiën | Eén versleuteld bestand (één wachtwoord, sleutelbestand en back-up, zie 18) met gescheiden tabellen. De scheiding uit besluit 36 wordt in de code aangehouden en bewaakt met een architectuurtest |
| 40 | Migratiegroepen | Groepen met "(Migratie)" (o.a. "Vrijwilligers (Migratie)") worden geïmporteerd als *technische groep*: standaard verborgen in overzichten, maar de startdatum telt mee voor "Lid sinds" (besluit 24) |
| 41 | Dubbele functies | Zelfde persoon, rol en groep met verschillende startdatum en zonder einddatum = één functie met de vroegste startdatum |
| 42 | Lidtype | `Role Membership Type` (Kaderlid/Jeugdlid) hoort bij de functie; jeugdlid én kaderlid tegelijk is toegestaan. De contributiecategorie blijft een eigen veld (besluit 21); de app stelt Jeugdlid voor zodra er een actieve jeugdlidfunctie is |
| 43 | Groepsindeling | Instelbare lijst: per groep kiest de gebruiker het soort (speltak, bestuur, technisch, overig), eenmalig in de importwizard. Een nieuwe groep in een import geeft een melding |
| 44 | Contributie-opbouw | De categorie bepaalt het tarief: *Jeugdlid <speltak>* per speltak, *Leiding* en *Vrijgesteld*. Leiding betaalt geen contributie. De gebruiker vult de bedragen zelf in (zie 52) |
| 45 | Incassofrequentie | Elke maand een incassorun |
| 46 | Gezinskorting | Geen. Wel één incassopost per betaler per run (besluit 29) |
| 47 | In- en uitstroom | Geen berekening naar rato. Een maand wordt gerekend als het lid op de 1e van die maand ingeschreven is: een nieuw lid betaalt vanaf de maand ná de start, bij uitschrijving loopt de contributie tot het einde van de lopende maand |
| 48 | Machtigingen | IBAN, rekeninghouder, mandaatkenmerk en ondertekeningsdatum worden handmatig in Verenigd ingevoerd (geen CSV-import) |
| 49 | Zonder machtiging | Lid zonder geldige machtiging valt buiten de run en komt op een lijst "zelf te innen" (CSV) |
| 50 | Incassobestand | De Vereniging heeft een Rabobank-incassocontract en Incassant-ID; bestandsformaat pain.008.001.08 (2019). Het Incassant-ID staat in de versleutelde database, niet in Git |
| 51 | Afdracht Scouting Nederland | Niet in fase 3; volgt in fase 4 (boekhouding) |
| 52 | Categorie per lid | De app stelt *Jeugdlid <speltak>* voor op basis van de actieve jeugdlidfunctie (besluit 42); de gebruiker kan per lid afwijken. Tarieven per boekjaar |
| 53 | Bedrag | Het tarief is een maandbedrag (in centen), dat elke maand wordt geïncasseerd |
| 54 | Categoriewisseling | Een categorie heeft een ingangsdatum (historie). Voor een maand geldt de categorie op de 1e van die maand |
| 55 | Gemiste maanden | Worden in de eerstvolgende run met terugwerkende kracht meegenomen, als aparte regel per maand. Een maand wordt nooit twee keer geïncasseerd |
| 56 | Incassodag | Vaste dag, instelbaar; valt die niet op een bankwerkdag, dan schuift de uitvoerdatum op. De app controleert of het bestand op tijd klaar is |
| 57 | SEPA-uitgangspunten | Incassotype CORE, altijd sequentietype RCUR; omschrijving `Contributie Scouting <maand jaar> – <voornamen>` (max. 140 SEPA-tekens), instelbaar |
| 58 | Naheffing | Hooguit tot het begin van het lopende boekjaar; oudere gemiste maanden komen op de lijst "zelf te innen" |
| 59 | Uitvoerdatum | Valt de incassodag niet op een TARGET-werkdag, dan schuift de uitvoerdatum naar de volgende werkdag |
| 60 | Run annuleren | Een definitieve run die de bank niet heeft uitgevoerd kan na bevestiging worden geannuleerd: status *Geannuleerd*, de maanden komen weer vrij, de run blijft in de historie |
| 61 | Boekhouding Stichting | Voorlopig alleen de Vereniging; de Stichting volgt later (het model blijft per administratie) |
| 62 | Overname huidige boekhouding | Eenmalig het rekeningschema en de eindbalans 2025 uit de huidige Excel-boekhouding inlezen als beginbalans 2026 (importwizard met voorbeeldoverzicht en controle debet = credit). Het Excel-bestand wordt niet bewaard en komt nooit in Git |
| 63 | Contributie boeken | Baten-lastenstelsel: de incassorun boekt *Debiteuren contributie* aan *Contributie <speltak>*; de bankontvangst boekt de debiteuren af; een stornering zet de vordering weer open |
| 64 | Banktransacties | Elke transactie uit CAMT.053 apart boeken, met boekingsregels (tegenrekening/omschrijving → grootboekrekening) |
| 65 | Kas | Kasboek in Verenigd met handmatige ontvangsten en uitgaven |
| 66 | Debiteuren/crediteuren | Openstaande posten én verkoopfacturen maken (bijv. verhuur), in stap 4c; de vorm (PDF) wordt in het ontwerp van 4c bepaald |
| 67 | Overzichten en export | Afdrukken via Windows (ook naar PDF) en exporteren naar CSV én Excel (.xlsx, ClosedXML, MIT-licentie) |
| 68 | Opdeling fase 4 | 4a Grootboek → 4b Bank → 4c Contributie & relaties, elk een eigen pull request |
| 69 | Indeling balans en W&V | In 4a per rubriek uit het rekeningschema; een instelbare jaarrekeningindeling (met kolom begroot) volgt in fase 5 |
| 70 | Memoriaal en kruisposten | Rekening 9990 Memoriaal wordt niet overgenomen (memoriaal is een dagboek; saldo ≠ 0 geeft een waarschuwing); 1298 Kruisposten wordt een tussenrekening voor overboekingen tussen eigen rekeningen |
| 71 | Conceptposten | Journaalposten zijn *Concept* of *Geboekt*; alleen geboekte posten tellen in rapporten; een boekjaar kan pas worden afgesloten zonder concepten. Een geboekte post is onveranderlijk (corrigeren via tegenboeken) |
| 72 | Ctrl+N in Boekhouding | Binnen het tabblad Boekhouding betekent Ctrl+N "Nieuwe post"; "Nieuwe database" blijft via het menu Bestand |
| 73 | Verschil in beginbalans | Een beginbalans die niet in evenwicht is wordt standaard geweigerd. Als bewuste uitzondering kan de gebruiker in de importwizard het verschil op een tussenrekening boeken (standaard nieuwe rekening *1299 Te onderzoeken verschillen*); dat staat als aparte regel op de openingsbalans en in het logboek. De huidige Excel-boekhouding 2025 heeft een verschil van € 7,24 (activa € 23.563,88, passiva € 23.556,64) |
| 74 | Herkende banktransacties | Een transactie die een boekingsregel herkent wordt een *concept*post in het bankdagboek; de gebruiker boekt de concepten na controle in één keer |
| 75 | Niet-herkende banktransacties | Blijven in de werkvoorraad "Te verwerken" tot de gebruiker een rekening kiest (geen tussenrekening) |
| 76 | Eigen bankrekeningen | Vereniging: Rabobank-betaalrekening en Rabobank-spaarrekening, elk een eigen bankdagboek |
| 77 | Incassobatch tot 4c | De ontvangen contributie-incasso (één regel met het totaal) wordt via een boekingsregel op *Debiteuren contributie* geboekt; afletteren per betaler volgt in 4c |
| 78 | Criteria boekingsregels | Tegen-IBAN, tekst in naam of omschrijving, bij/af, bedragbereik en banktransactiecode; de eerste passende regel in de volgorde wint |
| 79 | ING | ING wordt pas officieel ondersteund na een test met een (geanonimiseerd) ING-bestand van de gebruiker |
| 80 | Startpunt bankimport | Bankafschriften worden ingelezen vanaf 1-1-2026; de app waarschuwt als het eerste beginsaldo niet gelijk is aan het grootboeksaldo per 31-12-2025 |
| 81 | Startregels | De lijst boekingsregels begint leeg; regels ontstaan vooral via "Regel maken van deze boeking" |
| 82 | Bediening bank | Subtabbladen *Bank* en *Boekingsregels* in Boekhouding; *Bestand › Importeren › Bankafschrift (CAMT.053)…*; Ctrl+B = concepten boeken (Ctrl+Enter blijft "deze post boeken") |

## Stand van zaken (bijwerken na elke sessie)

- **Fase 1 (Fundament): gereed**, v0.1.0, 124 tests groen.
- **Ontwikkelomgeving:** .NET SDK 10.0.403, GitHub CLI (`C:\Program Files\GitHub CLI\gh.exe`), Visual Studio 2026 Community. De gebruiker werkt in VS Code met Claude Code.
- **GitHub: gereed.** Privé-repository https://github.com/jeroengoudswaard/verenigd (remote `origin`, branch `main`).
- **Webapp groepsleiders (fase 7):** besluiten 33–39 vastgesteld (2026-10-27), naar aanleiding van een extern implementatieplan (multi-user, API's, PostgreSQL); alleen de financiële scheiding en het idee van een webapp zijn overgenomen, de rest past niet bij Verenigd.
- **Fase 2 (Leden): gereed**, v0.2.0 (2026-10-27). Zie CHANGELOG.md en docs/ARCHITECTUUR.md. SOL3-importwizard (4 stappen, back-up vooraf, één transactie), tabblad Leden (Ctrl+2) met filters en notities, datamodel leden/financiën gescheiden (besluiten 35–39), architectuurtests.
  - Kopregels van de vier SOL3-exports: zie `tests/Verenigd.Application.Tests/Import/Sol3Voorbeeld.cs`. Export 4 heeft het lidnummer als laatste kolom; de import zoekt kolommen op naam (aanvulling op besluit 20).
  - Tests: 313 in totaal. Gebouwd in een Linux-cloudsessie: Domain, Application, Infrastructure en Architectuur (238) groen; de ViewModel-tests (71) via een tijdelijk net10.0-project, waarvan 2 bestaande tests alleen op Linux falen (Windows-paden). **Nog op Windows te draaien:** de volledige suite, inclusief de 4 FlaUI-UI-tests (`dotnet test`, dan `--filter Categorie=UI`).
  - Bekende beperkingen: IBAN invullen in Verenigd, machtiging en contributiecategorie volgen in fase 3; de groepsindeling kan alleen bij een import worden gekozen.
- **Fase 3 (Contributie & SEPA): gereed**, v0.3.0 (2026-10-27). Zie CHANGELOG.md en docs/ARCHITECTUUR.md. Tabblad Contributie (Ctrl+3) met vier subtabbladen, incassowizard, pain.008.001.08 met XSD-controle, Instellingen › Incasso.
  - Toevoeging op het ontwerp: instelling *eerste incassomaand* (eerdere maanden zijn nooit verschuldigd via Verenigd), zodat de eerste run geen jaren aan naheffing bevat. Concept-runs worden niet opgeslagen; een run bestaat pas als hij definitief is.
  - Tests: 443 in totaal. In de Linux-cloudsessie groen: Domain 159, Application 139, Infrastructure 41, Architectuur 7; ViewModel-tests 90/92 via een tijdelijk net10.0-project (dezelfde 2 bestaande tests met Windows-paden falen alleen op Linux). **Nog op Windows te draaien:** de volledige suite en de 5 FlaUI-UI-tests.
  - Bekende beperkingen: het verschil tussen het IBAN uit SOL3 en een in Verenigd ingevoerd IBAN (besluit 22) wordt nog niet apart gesignaleerd; de SOL3-import overschrijft de betalerkoppeling als SOL3 een IBAN levert (nu zijn de SOL3-IBAN-kolommen leeg). Boeken, storneringen en afdracht volgen in fase 4.
- **Fase 4 (Bankimport & boekhouding): in ontwerp.** Besluiten 61–68 vastgesteld (2026-10-27), na analyse van de huidige Excel-boekhouding van de Vereniging (59 rekeningen, 4-cijferig, T-rekeningen per tabblad, bank in maandtotalen, contributie per speltak 8020–8060, kruisposten 1298, overlopende posten 1400/1700, geen btw). Ontwerp 4a goedgekeurd (besluiten 69–72): rekeningschema met rubrieken, dagboeken Memoriaal/Kas, journaalposten concept → geboekt met tegenboeken, Excel-import van schema en beginbalans 2026, grootboekkaart, saldibalans, balans, W&V, jaarafsluiting, afdrukken en export (CSV/xlsx via ClosedXML).
  - **4a Grootboek: gereed**, v0.4.0 (2026-10-27). Zie CHANGELOG.md en docs/ARCHITECTUUR.md. Tabblad Boekhouding (Ctrl+4) met subtabbladen Journaal, Rekeningschema, Grootboekkaart, Rapporten en Boekjaren; importwizard *Bestand › Importeren › Boekhouding uit Excel*; *Bestand › Afdrukken* (Ctrl+P); export naar CSV en xlsx.
  - Toevoegingen op het ontwerp: *Volgend boekjaar openen* (alvast boeken in het nieuwe jaar); afsluitposten en openingsbalansen kunnen niet worden tegengeboekt (dat gaat via heropenen van het boekjaar).
  - Tests: 512 in totaal. In de Linux-cloudsessie groen: Domain 172, Application 171, Infrastructure 47, Architectuur 7; ViewModel-tests 107/109 via een tijdelijk net10.0-project (dezelfde 2 bestaande tests met Windows-paden falen alleen op Linux). **Nog op Windows te draaien:** de volledige suite en de 6 FlaUI-UI-tests, plus een proefimport van de echte Excel-boekhouding (verwacht: 1299 met € 7,24).
  - **4b Bank: gereed**, v0.4.1 (2026-10-27). Besluiten 74–82. Zie CHANGELOG.md en docs/ARCHITECTUUR.md. Bankimport CAMT.053 (wizard), subtabbladen Bank en Boekingsregels, kruisposten automatisch, Ctrl+B.
    - Analyse echte Rabobank-bestanden (betaal- en spaarrekening, 2025 en 2026; niet bewaard, niet in Git): camt.053.001.08, één afschrift per dag; de afschriftdatum is de datum van CLBD (PRCD draagt de vorige dag); saldi sluiten overal aan; eindsaldo 31-12-2025 = Excel 1100 (betaal) en 1150 (spaar), dus het verschil van € 7,24 zit niet in de bank. Rente op de spaarrekening heeft alleen eigen code Rabobank 1025 en geen bankreferentie. De eigen incasso staat als één batchregel (IDDT-ESDD); storneringen als IDDT-UPDD.
    - Tests: 565 in totaal. In de Linux-cloudsessie groen: Domain 190, Application 193, Infrastructure 50, Architectuur 7; ViewModel-tests 117/119 (dezelfde 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite en de 6 FlaUI-UI-tests, plus een proefimport van de echte afschriften.
    - Volgende stap: 4c Contributie & relaties.

## Privacy

- Echte leden-, bank- en boekhoudgegevens komen NOOIT in Git. Voorbeeldbestanden worden geanonimiseerd. Bronbestanden staan in een map die in `.gitignore` staat.
- Log geen persoonsgegevens of IBAN's.
