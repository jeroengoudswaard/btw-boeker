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
- Native Windows-gedrag heeft voorrang: standaard menubalk (Bestand, Bewerken, Beeld, Help; sinds besluit 179 in Eenvoudig verborgen en met Alt op te roepen), vertrouwde sneltoetsen (Ctrl+S, Ctrl+Z, Ctrl+F, F5…), Segoe UI, standaard paddings en margins, ondersteuning voor Light/Dark mode en toegankelijkheid (toetsenbordnavigatie, AutomationProperties).

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
| 83 | Bijlagen (bonnen/facturen) | Bijlagen horen bij een journaalpost (bank, kas en memoriaal); bij een banktransactie hangen ze aan de post die eruit ontstaat |
| 84 | Opslag bijlagen | In de versleutelde database (mee in de back-up), met een maximum per bestand |
| 85 | Bijlagen toevoegen | Per post (knop of slepen) én via een "postvak bonnen": eerst uploaden, de app stelt een koppeling voor op datum en bedrag |
| 86 | Bijlagen bekijken | Voorbeeld in de app (PDF en afbeeldingen), plus een optie om de bijlage te exporteren (opslaan als bestand) |
| 87 | Planning bijlagen | In stap 4c, samen met debiteuren/crediteuren en verkoopfacturen |
| 88 | Uitgaven zonder bon | Filter "zonder bon" op uitgaven boven een instelbaar bedrag |
| 89 | Opdeling 4c | 4c-1 Contributie (run boeken, afletteren, storneringen) → 4c-2 Relaties, openstaande posten en verkoopfacturen → 4c-3 Bijlagen; elk een eigen pull request |
| 90 | Rekeningen debiteuren/crediteuren | 1300 *Debiteuren contributie*, 1310 *Debiteuren overig*, 1600 *Crediteuren*; de app maakt ze aan als ze nog niet bestaan |
| 91 | Boekdatum incassorun | De uitvoerdatum van de incasso |
| 92 | Stornering | De post blijft openstaan (Contributie › Openstaand) met de reden; per post een knop "Opnieuw incasseren in de volgende run" |
| 93 | Crediteuren | Eenvoudig: betalingen gaan direct naar een kostenrekening met de bon als bijlage; crediteuren alleen voor facturen die op 31-12 openstaan (memoriaal) |
| 94 | Btw | De Vereniging is niet btw-plichtig; verkoopfacturen zonder btw |
| 95 | PDF verkoopfactuur | PDFsharp/MigraDoc (MIT-licentie) |
| 96 | Postvak bonnen | De app leest datum en bedrag uit de tekst van een PDF; lukt dat niet (bijvoorbeeld een foto), dan vult de gebruiker ze in |
| 97 | Limieten bijlagen | Maximaal 10 MB per bijlage; drempel "zonder bon" € 10 (later instelbaar) |
| 98 | Afdracht Scouting Nederland | Een gewone betaling met een boekingsregel; geen aparte functie |
| 99 | Run boeken | Automatisch als geboekte post bij *Definitief maken*; ontbreekt een opbrengstrekening per categorie, dan een melding en later de knop *Boeken* |
| 100 | Omschrijving herincasso | `Contributie Scouting <maand jaar> (herincasso) – <voornamen>`; geen nieuwe opbrengst (de vordering staat al op 1300) |
| 101 | Oninbaar | Knop *Afboeken als oninbaar*: gekozen kostenrekening aan 1300 |
| 102 | Handmatige betaling open post | Automatisch concept als IBAN van de betaler én bedrag precies passen bij één open post; anders via de afletterdialoog |
| 103 | Incasso's van vóór Verenigd | Incasso's van januari–oktober 2026 (oude systeem) via een boekingsregel direct op contributie, zoals in Excel; eventueel per speltak splitsen in het Journaal; geen debiteuren |
| 104 | Herincasso als aparte post | Een herincasso is een aparte post in de run, naast een eventuele gewone post van dezelfde betaler (uitzondering op besluit 46), zodat een nieuwe stornering eenduidig aan de oorspronkelijke vordering te koppelen is |
| 105 | Plaats relaties en facturen | Subtabbladen *Relaties* en *Facturen* in Boekhouding; relaties zijn gedeeld door beide administraties |
| 106 | Factuurnummer | Per administratie en jaar met voorvoegsel: `V2026-001` (Vereniging), `S2026-001` (Stichting) |
| 107 | Factureren voor | Voorlopig alleen de Vereniging (besluit 61); de Stichting volgt later |
| 108 | Relatie en lid | Relaties staan los van leden (geen koppeling via lidnummer) |
| 109 | Vervallen facturen | Filter en markering *Vervallen*; herinneringen per e-mail volgen in fase 6 |
| 110 | QR-code | EPC-QR ("girocode") op de PDF-factuur |
| 111 | Ctrl+N bij facturen | Blijft "Nieuwe post" (besluit 72); een nieuwe factuur alleen via de knop |
| 112 | Logo | PNG of JPG bij Instellingen › Facturen, opgeslagen in de database |
| 113 | QR-code genereren | QRCoder (MIT-licentie) |
| 114 | Standaardinrichting | Naast *Overnemen uit Excel* en *Leeg beginnen* een ingebouwd standaardschema, zodat andere scoutinggroepen zonder eigen Excel kunnen starten. Het rekeningschema nu (v0.4.4, vóór 4c-3); de begrotingsindeling volgt in fase 5 als uitbreiding van hetzelfde sjabloon |
| 115 | Inhoud standaardschema | Een nieuw, compact minimumschema dat Claude voorstelt en de gebruiker controleert (niet afgeleid van de eigen Excel-boekhouding) |
| 116 | Delen van sjablonen | Ingebouwd in de app, plus exporteren en importeren van een rekeningschema als sjabloonbestand (alleen nummers, namen, rubrieken en rollen; geen bedragen of persoonsgegevens) |
| 117 | Sjabloon Stichting | Ook een eigen standaardschema voor de Stichting (o.a. gebouw, afschrijvingen, huur) |
| 118 | Inhoud standaardschema's | Goedgekeurd zoals voorgesteld (2026-10-28): Vereniging ±32 rekeningen (0600 reserve, 1000 kas, 1100/1150 bank, 1298, 1300/1310/1400/1600/1700, 8010… contributie per speltak, 84xx–89xx overige baten, 4000 afdracht, 41xx activiteiten, 42xx huisvesting, 43xx organisatie); Stichting ±25 rekeningen (0200/0210 gebouw, 0300/0310 inventaris, 0800 leningen, 8100 huur van de Vereniging, 4200–4410 onderhoud, OZB en afschrijvingen). Dagboeken MEM, VRK (en KAS bij de Vereniging) |
| 119 | Speltakken in de wizard | Standaard Bevers, Welpen, Scouts, Explorers en Roverscouts; aanvinken wat de groep heeft, eigen namen toe te voegen |
| 120 | Beginsaldi | Een wizardstap "Beginsaldi" per balansrekening met de controle debet = credit; een verschil kan net als bij de Excel-import bewust op een tussenrekening (besluit 73) |
| 121 | PDF-bibliotheek | PDFsharp/MigraDoc vast op 6.2.4, de laatste MIT-versie (het NuGet-pakket 6.5 meldt AGPL-3.0 en heeft een licentiecontrole). Pkcs vastgezet op een gepatchte versie. Overstappen (bijv. QuestPDF) kan later door alleen `IFactuurPdfMaker` te vervangen |
| 122 | Historisch boekjaar 2025 | Aanvulling op besluit 62: een afgesloten, alleen-lezen boekjaar 2025. Per rekening de oorspronkelijke regels uit de Excel-T-rekeningen (gemarkeerd als "overgenomen uit Excel, niet dubbel geboekt"); Balans en W&V 2025 uit de totalen van het Voorblad; het Journaal 2025 blijft leeg en telt niet mee in het grootboek. Dient in fase 5 als "vorig jaar". Reden: de Excel bevat losse T-rekeningen (bank in maandtotalen, 4 van 245 datum/bedrag-combinaties te koppelen, debet ≠ credit), dus echte journaalposten zijn niet betrouwbaar te reconstrueren |
| 123 | Regels zonder datum of van vóór 2025 | Krijgen datum 1-1-2025; zonder datum gemarkeerd als "begin- of tussensaldo", een oudere datum komt in de omschrijving. Zo klopt het totaal per rekening met Excel |
| 124 | Planning historisch boekjaar | Na de proefimport, als aparte stap vóór 4c-3 (v0.4.8); eerst een uitgewerkt ontwerp ter goedkeuring |
| 125 | Ontwerp historisch boekjaar | Goedgekeurd (2026-10-28): HistorieInlezer slaat SUM-regels over; voorbeeld per rekening (aantal regels, saldo T-rekening, saldo Voorblad, ✓/⚠); Boekjaar 2025 met IsHistorisch, plus aparte tabellen voor historische regels en totalen (architectuurtest: telt nooit mee in het grootboek); wizardstap *Historie 2025* én *Bestand › Importeren › Historie uit Excel…*; band "Historisch boekjaar"; Journaal leeg; Grootboekkaart met originele regels en *Totaal volgens Voorblad*; rapporten 2025 uit het Voorblad; onbekende rekeningen: overslaan of als inactief toevoegen |
| 126 | Afwijkende saldi historie | Importeren met ⚠ en het totaal volgens het Voorblad; de gebruiker zoekt het in Excel uit |
| 127 | Weergaven Eenvoudig/Expert | Alleen visueel: de database en alle opgeslagen gegevens blijven formeel. Wisselen (toggle) via *Beeld › Weergave*, direct zonder herstart; de keuze wordt onthouden. Teksten uit twee woordenlijsten (zoals licht/donker thema), ook voor meldingen; een architectuurtest bewaakt dat elk woord in beide lijsten staat. Eenvoudig toont uitleg als tooltip bij lastige begrippen |
| 128 | Woordenlijst Eenvoudig | Eerste versie zoals voorgesteld (2026-10-28), o.a. Journaal → Alle boekingen, Bank → Banktransacties, Rekeningschema → Potjes, Grootboekkaart → Inhoud van een potje, Saldibalans → Stand van alle potjes, Balans → Bezittingen en schulden, W&V → Inkomsten en uitgaven; bijstellen tijdens gebruik |
| 129 | Debet/credit in Eenvoudig | Kolommen *Inkomst* en *Uitgave*, gezien vanuit de vereniging; de precieze regel voor balanspotjes (bank, kas, reserve, debiteuren) wordt in het ontwerp van v0.4.9 voorgelegd |
| 130 | Reikwijdte weergaven | Alleen Financiën (de boekhouding) |
| 131 | Afdrukken en export | Altijd de Expert-termen (bijv. voor de kascommissie) |
| 132 | Standaardweergave en volgorde | Een nieuwe database begint in *Expert*. Eerst het historische boekjaar (v0.4.8), daarna de weergaven (v0.4.9) |
| 133 | Kolommen in Eenvoudig | Inkomsten- en uitgavenpotjes (resultaat): *Inkomst* / *Uitgave*; geldpotjes (bank, kas, spaar): *Erbij* / *Eraf*; overige balanspotjes (reserve, nog te ontvangen/betalen): *Debet* / *Credit* met uitleg als tooltip |
| 134 | Pull request v0.4.7–v0.4.9 | Direct door met v0.4.9; daarna één pull request voor v0.4.7 t/m v0.4.9 na een Windows-run |
| 135 | PDF bekijken en lezen | Voorbeeld via Windows zelf (`Windows.Data.Pdf`, geen extra installatie); tekst lezen met PdfPig (Apache-2.0). Alles lokaal en in het geheugen: een bijlage komt nooit onversleuteld op schijf, behalve bij *Opslaan als…* |
| 136 | Bijlage bij geboekte post | Toevoegen én verwijderen mag, ook bij een geboekte post; de boeking zelf verandert niet |
| 137 | Bestandstypen bijlagen | PDF, JPG en PNG |
| 138 | "Zonder bon" | Een uitgave is geld dat van de bank of uit de kas gaat (bank- en kasdagboeken), boven de drempel van besluit 97 |
| 139 | Restpunten in 4c-3 | De twee tooltips met Expert-woorden en de IBAN-waarschuwing (besluit 22) gaan mee in de pull request van 4c-3 |
| 140 | Bijlagen bij een post | Blok *Bijlagen* onder de regels in de post-editor (Toevoegen, Bekijken, Verwijderen, slepen); een paperclip-kolom in het Journaal toont welke posten een bijlage hebben |
| 141 | Afwijkend IBAN uit SOL3 | Waarschuwing in het voorbeeldoverzicht van de import, met per lid een keuze *IBAN uit SOL3 overnemen* (standaard uit); zonder vinkje blijft de koppeling in Verenigd staan. Aanvulling op besluiten 21 en 22 |
| 142 | Negatieve bedragen op bonnen | Een minteken vóór of achter het bedrag (`-15,00`, `€ -15,00`, `15,00-`) blijft behouden; het bedrag wordt opgeslagen zoals op de bon (creditnota negatief) en mag in het postvak met minteken worden ingevuld. In de pull request van 4c-3 |
| 143 | Koppelen creditnota | Een negatieve bon wordt voorgesteld bij posten waarbij geld binnenkomt op bank of kas, met hetzelfde bedrag zonder minteken; een positieve bon werkt zoals voorheen |
| 144 | Weergave creditnota | Alleen het negatieve bedrag (geen apart label), in groen; het minteken blijft, zodat kleur niet het enige kenmerk is |
| 145 | "Zonder bon" ook ontvangsten | Wijziging van besluit 138: het filter toont uitgaven én ontvangsten van bank of kas boven de drempel zonder bijlage |
| 146 | Opdeling fase 5 | 5a Begroting Vereniging → 5b Meerjarenplan Stichting, elk een eigen pull request. 5b na 5a, als losstaand plan (10 jaar, onderhoud, investeringen, afschrijvingen, huur, indexatie) tot de boekhouding van de Stichting er is |
| 147 | Niveau begroting | Per grootboekrekening (resultaatrekeningen); rapporten tellen op per rubriek |
| 148 | Periode begroting | Alleen een jaarbedrag; het lopende jaar wordt naar rato vergeleken |
| 149 | Status begroting | Concept → *Vastgesteld* (alleen-lezen); een *Herziening* is mogelijk, de oorspronkelijke versie blijft bewaard |
| 150 | Opstellen begroting | Overnemen van de werkelijke cijfers of de begroting van vorig jaar, met een optioneel percentage; elk bedrag blijft met de hand bij te stellen |
| 151 | Jaarrekeningindeling | Nog niet in 5a: de begroting volgt de rubrieken uit het rekeningschema; een eigen indeling (besluit 69) volgt later |
| 152 | Baten of lasten | Nieuwe eigenschap *Baten/Lasten* per resultaatrekening in het rekeningschema; bij bestaande rekeningen voorgesteld uit het saldo van vorig jaar (anders 8xxx = baten), door de gebruiker aan te passen |
| 153 | Afronding overnemen | Bij overnemen met een percentage afronden op tientallen euro's |
| 154 | Eenvoudig-termen begroting | Begroting → *Begroting*, Begroot → *Verwacht*, Werkelijk → *Uitgegeven/ontvangen*, Naar rato → *Verwacht tot nu toe* |
| 155 | Afdruk begroting | Per rubriek met subtotalen en de kolommen *Werkelijk vorig jaar*, *Begroot dit jaar* en *Begroot volgend jaar* (voor de ALV) |
| 156 | App-icoon | Vinkje-icoon van de gebruiker: één `Verenigd.ico` met 16–256 px (gemaakt uit de aangeleverde PNG's; het aangeleverde .ico had alleen 16×16), alleen als app-icoon. Android-iconen ongewijzigd in `assets/android/` voor fase 7. Mee in de pull request van v0.5.0 |
| 157 | Module Begroting (Ctrl+5) | *Modules › Begroting* en Ctrl+5 openen het tabblad Boekhouding van de actieve administratie (besluit 17) op het subtabblad *Begroting*; geen apart tabblad |
| 158 | App-icoon versie 3 | Nieuw ontwerp van de gebruiker (2026-10-29; aangeleverd als JPG met ingebakken schaakbord). De tegel is uitgeknipt met afgeronde hoeken en echte transparantie; daaruit opnieuw gemaakt: `Verenigd.ico` (16–256 px) en de Android-set. Bron: `assets/bron/verenigd-icoon-1024.png`. Aanvulling (2026-10-29): zonder lege rand, de tegel vult het hele vierkant (in de taakbalk was het icoon te klein) |
| 159 | Meerjarenplan: posten | Per grootboekrekening (resultaatrekeningen) uit het rekeningschema van de Stichting, zoals de begroting; de Stichting richt daarvoor eerst een rekeningschema in (bijv. het standaardschema), zonder te boeken |
| 160 | Meerjarenplan: invullen | Een raster met per rekening een bedrag per jaar; geen cycli of automatische verdeling |
| 161 | Meerjarenplan: indexatie | Geen indexatie; alle bedragen in de prijzen van het jaar zelf |
| 162 | Meerjarenplan: overzicht | Alleen baten en lasten per jaar met het resultaat per jaar; geen geldprognose |
| 163 | Meerjarenplan: plaats | Subtabblad *Meerjarenplan* in het tabblad Boekhouding van de Stichting; Ctrl+5 opent bij de Stichting het meerjarenplan en bij de Vereniging de begroting |
| 164 | Meerjarenplan: versies | Zoals de begroting: concept → *Vastgesteld* (alleen-lezen) → herziening als nieuwe versie |
| 165 | Afdruk meerjarenplan | Liggend A4, alle 10 jaren op één pagina, met kleinere letters voor de bedragen |
| 166 | Invulhulp meerjarenplan | Per rij een knop *Naar de volgende jaren kopiëren*: het bedrag van het gekozen jaar gaat naar alle latere jaren; daarna blijft elk bedrag aan te passen |
| 167 | Startjaar meerjarenplan | Vrij te kiezen, standaard het volgende jaar; een nieuw plan begint los van het vorige |
| 168 | Herinrichting UI (wijzigt besluit 9) | Volledige herinrichting naar het model van Clipchamp: navigatiebalk links, geen tabbladen, de klassieke menubalk alleen in de Expert-weergave; per scherm één hoofdactie, lege schermen met uitleg en één knop, kaarten op het startscherm, vaste statusteksten (✓/⚠/✕, "wat er gebeurde + wat nu"). Gevolgen voor besluiten 10–12, 17 en 127 worden in het ontwerp voorgelegd |
| 169 | Startwizard: moment | Automatisch bij de eerste start (geen database); daarna altijd via *Bestand › Startwizard…* |
| 170 | Startwizard: stappen | Welkom → Database → Vereniging en Stichting → Leden → Boekhouding → Bank → Contributie → Klaar; elke stap over te slaan, *Later verder* bewaart de voortgang; de bestaande wizards en diensten worden hergebruikt |
| 171 | Volgorde | Eerst 5b afmaken, dan de startwizard, dan de herinrichting van de UI |
| 172 | Startwizard: bestaande wizards | De startwizard opent de bestaande wizards (SOL3, boekhouding, bank) als eigen venster en gaat daarna verder; geen herbouw |
| 173 | Startwizard: voortgang | *Later verder* bewaart de stap in de database (hoort bij de administratie, ook na een back-up) |
| 174 | Startwizard: venster | Een eigen venster vóór het hoofdvenster, zodat het na de herinrichting (besluit 168) blijft werken |
| 175 | Startwizard: stap al klaar | Tonen met "Al ingericht" en de knoppen om het toch opnieuw te doen; niet automatisch overslaan |
| 176 | Pull request v0.6.0–v0.7.0 | Eén pull request voor het meerjarenplan en de startwizard, na een geslaagde Windows-run |
| 177 | Herinrichting: administratie | Eén wisselaar bovenaan voor de hele app, met accentkleur en band; vervangt het tabblad per administratie (besluiten 10, 11 en 17). Herstarten opent de laatste pagina en administratie (vervangt 12) |
| 178 | Herinrichting: onderdelen Boekhouding | Inklapbare subitems in de navigatiebalk (Journaal, Bank, Postvak …), elk met één klik bereikbaar |
| 179 | Herinrichting: menubalk | In Expert zichtbaar; in Eenvoudig verborgen, Alt toont hem tijdelijk (zoals Verkenner). Sneltoetsen werken altijd. Eenvoudig/Expert geldt daarmee voor de menubalk app-breed; de eenvoudige woorden blijven alleen in Financiën (aanvulling op 130) |
| 180 | Herinrichting: aanpak | Alles in één versie (v0.8.0): schil, startpagina met kaarten, één hoofdactie per pagina en lege schermen |
| 181 | Herinrichting: Contributie | De subtabbladen van Contributie worden ook subitems in de navigatiebalk, zoals Boekhouding |
| 182 | Herinrichting: wisselen van administratie | Dezelfde pagina blijft staan voor de andere administratie; bestaat die daar niet (Contributie bij de Stichting), dan naar Start |
| 183 | Herinrichting: startkaarten | Alle 8: inrichting afmaken, banktransacties te verwerken, conceptposten, bonnen in het postvak, incassorun van deze maand, openstaande storneringen, vervallen facturen, zonder bon; alleen zichtbaar als er iets te doen is |
| 184 | Volgorde v0.8.0 | Eerst de Windows-run en de merge van v0.6.0–v0.7.0, daarna bouwen |
| 185 | Uitleg SOL3-exports: plaats | Een gedeeld onderdeel in de stap Leden van de startwizard én in stap 1 van de SOL3-importwizard: per export het doel, de kolommen en een knop *Kolomnamen kopiëren* |
| 186 | Uitleg SOL3-exports: kolommen | Alle kolommen per export, met *verplicht* (de import heeft hem nodig; leeg mag) of *optioneel* (SOL3 levert hem, Verenigd negeert hem); één bron in de importcode, bewaakt met een test |
| 187 | Uitleg SOL3-exports: klikroute | De gebruiker levert de klikroute in SOL3 (menu, knoppen, formaat); tot dan geen klikroute in de uitleg |
| 188 | Uitleg SOL3-exports: planning | Nu, als v0.7.1, mee in de pull request van v0.6.0–v0.7.0 |
| 189 | Pull request v0.6.0–v0.7.1 | Direct openen (de Windows-run van v0.7.0 is groen); v0.7.1 komt er als extra commit bij |
| 190 | Scheidingsteken SOL3-CSV | De import herkent `;` of `,` aan de kopregel (buiten aanhalingstekens, bij gelijkspel `;`). SOL3 noemt de export "door komma's gescheiden", maar levert puntkomma's (gemeld door de gebruiker) |
| 191 | E-mail: functies | Facturen en herinneringen, vooraankondiging per incassorun, bericht bij stornering/openstaande post, vrije berichten aan leden en voogden per speltak of groep, en een welkomstmail; de gebruiker kan zelf sjablonen toevoegen en aanpassen |
| 192 | E-mail: koppeling | Gmail-API met OAuth (alleen toestemming om te versturen, geen wachtwoord in de app); de gebruiker maakt eenmalig een Google Cloud-project, met hulp van Claude. De toegangssleutel staat versleuteld in de database |
| 193 | E-mail: account | Een bestaand gmail.com-account van de penningmeester (adres niet in Git; ingesteld in de app). Limiet ±500 berichten per dag |
| 194 | E-mail: groepsberichten | Elke ontvanger krijgt een eigen, persoonlijk ingevulde e-mail; geen BCC |
| 195 | E-mail: logboek | Bewaren aan wie, wanneer, onderwerp en sjabloon; niet de tekst zelf |
| 196 | Volgorde fase 6 | Eerst v0.8.0 (herinrichting), daarna fase 6 in de nieuwe navigatie; het uitgewerkte ontwerp van fase 6 (sjablonen, moment van de welkomstmail, Google Cloud-stappen) volgt dan ter goedkeuring |
| 197 | XAF (auditfile financieel) | Export van de volledige financiële administratie als XAF (XML, standaard van de Belastingdienst), zodat een groep kan overstappen naar een ander pakket; import van XAF als gespreksonderwerp. Beide nog niet ingepland; eerst een ontwerp als het zover is |
| 198 | Opbouw eigen administratie | De Vereniging bouwt 2025 opnieuw op in een lege, verbeterde administratie (posten en bankafschriften vanaf 1-1-2025). Rekeningschema uit het standaardschema of een sjabloonbestand; beginsaldi per 1-1-2025 handmatig in de wizardstap *Beginsaldi*. Daarvoor een keuze *Beginjaar* (standaard het huidige jaar) in de standaardinrichting en bij een sjabloonbestand. De Excel-overname (besluit 62) wordt voor de eigen administratie niet meer gebruikt |
| 199 | Historisch boekjaar | Blijft in de app voor andere groepen (besluiten 122–126); niet gebruikt voor de eigen administratie |
| 200 | Incasso's oude systeem 2025–2026 | Zoals besluit 103: via een boekingsregel direct op contributie, zonder debiteuren, ook voor 2025 |
| 201 | 2024 | Geen historie of vergelijking met 2024 in de applicatie |
| 202 | Planning beginjaar | De keuze *Beginjaar* gaat mee in v0.8.0 |
| 203 | E-mail: ontvangers leden | Per bericht te kiezen (standaard, alleen voogden, alleen leden zelf, voogden én leden); standaard: voogden bij leden met een actieve jeugdlidfunctie, het lid zelf bij de anderen |
| 204 | E-mail: berichten per betaler | Vooraankondiging en stornering gaan naar de voogden en leden die onder die betaler vallen, ontdubbeld op e-mailadres; geen eigen adres bij de betaler |
| 205 | E-mail: welkomstmail | Na een SOL3-import met nieuwe leden biedt de app aan een welkomstmail op te stellen voor die leden |
| 206 | E-mail: vooraankondiging | Direct na *Definitief maken* van een incassorun aanbieden, per betaler met bedrag, uitvoerdatum en mandaatkenmerk |
| 207 | E-mail: facturen | Na *Definitief maken* van een factuur aanbieden om te mailen met de PDF als bijlage; bij een vervallen factuur een knop *Herinnering sturen* |
| 208 | Opdeling fase 6 | Alles in één versie (v0.9.0): koppeling, sjablonen, opstellen, verzonden en de berichten vanuit de modules |

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
    - Volgende stap: 4c Contributie & relaties, inclusief bijlagen (besluiten 83–88).
  - **4c: in ontwerp.** Besluiten 89–98 vastgesteld (2026-10-27). Analyse CAMT: de eigen incassobatch draagt `Btch/PmtInfId` (= kenmerk van de run + "-1"); een stornering (IDDT-UPDD) draagt EndToEndId, mandaat, tegen-IBAN en redencode (`RtrInf/Rsn/Cd`, bijvoorbeeld AM04). Het Excel-rekeningschema heeft geen debiteuren- of crediteurenrekening, wel 1400/1700. Ontwerp 4c-1 voorgelegd; besluiten 99–103 vastgesteld.
  - **4c-1 Contributie: gereed**, v0.4.2 (2026-10-28). Zie CHANGELOG.md en docs/ARCHITECTUUR.md. Run boeken (automatisch), herkenning van batch, stornering en handmatige betaling, afletteren in de bank, subtabblad Openstaand met herincasso en oninbaar, opbrengstrekening per categorie.
    - Uitwerking: een herincasso is een **aparte post** in de run (naast een eventuele gewone post van dezelfde betaler), zodat een nieuwe stornering eenduidig aan de oorspronkelijke vordering te koppelen is; dit is een uitzondering op "één post per betaler per run" (besluit 46), bevestigd als besluit 104.
    - Tests: 596 in totaal. In de Linux-cloudsessie groen: Domain 200, Application 207, Infrastructure 51, Architectuur 7; ViewModel-tests 123/125 (dezelfde 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite en de 6 FlaUI-UI-tests.
    - Volgende stap: ontwerp 4c-2 Relaties, openstaande posten en verkoopfacturen.
  - **4c-2 Relaties en verkoopfacturen: gereed**, v0.4.3 (2026-10-28). Besluiten 105–113. Zie CHANGELOG.md en docs/ARCHITECTUUR.md. Subtabbladen Facturen en Relaties in Boekhouding, dagboek VRK (1310 aan opbrengst), creditfactuur, PDF met EPC-QR, Instellingen › Facturen met logo, herkenning en afletteren van factuurbetalingen in de bank.
    - Uitwerking: PDFsharp-MigraDoc staat vast op 6.2.4, de laatste MIT-versie (6.5 is AGPL-3.0 met licentiecontrole), zodat besluit 95 blijft gelden; System.Security.Cryptography.Pkcs is vastgezet op een gepatchte versie. De QR-code wordt als 8-bits PNG weggeschreven (PDFsharp leest de 1-bits PNG van QRCoder niet); gecontroleerd met een QR-lezer.
    - Tests: 623 in totaal. In de Linux-cloudsessie groen: Domain 207, Application 215, Infrastructure 56, Architectuur 7; ViewModel-tests 130/132 (dezelfde 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite en de 6 FlaUI-UI-tests, plus een PDF-factuur met Segoe UI bekijken en de QR-code met een bankapp scannen.
  - **Standaardinrichting en sjablonen: gereed**, v0.4.4 (2026-10-28). Besluiten 114–121. Standaardschema's Vereniging en Stichting (JSON, ingebouwd), wizard Speltakken → Rekeningen → Beginsaldi → Klaar (zelfde route als de Excel-overname), export en import als `.vsjabloon`, vier keuzes op het startscherm van een lege boekhouding.
    - Tests: 638 in totaal. In de Linux-cloudsessie groen: Domain 207, Application 225, Infrastructure 56, Architectuur 7; ViewModel-tests 135/137 (dezelfde 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite en de 6 FlaUI-UI-tests.
    - **Windows-test (2026-10-27, Windows 11, commit `a054e08`):** build zonder waarschuwingen; 637/638 geslaagd, inclusief alle FlaUI-UI-tests. De 2 Windows-padtests slagen op Windows. Eén echte bug gevonden: de startkaart van een lege boekhouding verscheen niet (`IsGeladen` gaf geen melding voor `IsLeeg`, aanwezig sinds v0.4.0).
  - **v0.4.5 (2026-10-28): bugfixes.** `IsLeeg` en de kolom *Soort* bij Relaties (`Rol`) melden zich nu, elk met een regressietest. Een controle van alle berekende eigenschappen vond geen andere gebonden gevallen.
    - Tests: 640 in totaal. In de Linux-cloudsessie groen: Domain 207, Application 225, Infrastructure 56, Architectuur 7; ViewModel-tests 137/139 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 640/640).
    - **Windows-test (2026-10-27, commit `ae25a91`):** build zonder waarschuwingen; 639/640 geslaagd. De fix voor `IsLeeg` werkt. Eén test faalde: `PostEditor` werd niet gevonden, omdat een `Border` geen automation peer heeft (testprobleem, dat ook voor schermlezers gold).
  - **v0.4.6 (2026-10-28): toegankelijkheid van groepen.** 13 containers met een AutomationId op een Border of panel zijn nu `Groepskader`, `Groepspaneel` of `Groepsraster` (peer van het type Group, met Nederlandse naam). Een architectuurtest bewaakt dit en een Windows-test controleert de peers.
    - **Windows-test (2026-10-27, commit `9d83d85`):** build zonder waarschuwingen; 641/642 geslaagd. Alle FlaUI-UI-tests slagen, ook `PostEditor` en `FactuurEditor`. De nieuwe peertest faalde, omdat `IsControlElement` in WPF `IsVisible` volgt en het element niet getoond werd (testprobleem). Opgelost: de test toont het element in een venster buiten beeld, en een extra test controleert dat een ingeklapte groep geen control-element is. `IsControlElementCore` blijft bewust ongewijzigd.
    - Tests: 643 in totaal. In de Linux-cloudsessie groen: Domain 207, Application 225, Infrastructure 56, Architectuur 8; ViewModel-tests 137/139 (de 2 Windows-padtests). **Windows-test (2026-10-27, commit `b5c5e3e`): 643/643 geslaagd**, inclusief alle FlaUI-UI-tests.
    - Pull request: één PR voor v0.4.3 t/m v0.4.6 (4c-2, standaardinrichting en bugfixes), geopend na de geslaagde Windows-run.
    - Daarna: de gebruiker doet zelf de proefimport van de echte Excel-boekhouding en bankafschriften op Windows (de bestanden blijven buiten Git). Nog niet uitgevoerd: de proefimport van de echte Excel-boekhouding en van de echte bankafschriften (die doet de gebruiker zelf).
  - **v0.4.7: bevindingen uit de proefimport.** De gebruiker test met de echte bestanden en verzamelt bevindingen; die worden samen opgelost in één PR (keuze 3A).
    - Antwoord bij de proefimport: het boekjaar is 2026, omdat de eindbalans 2025 de openingsbalans 2026 wordt (besluit 62). 2025 wordt zichtbaar als historisch, alleen-lezen boekjaar (besluiten 122–124, v0.4.8).
    - Opgelost: rapportkolommen (tekst `3*`, bedragen `1*`; de viewer las de vaste breedte als verhouding) en de underscore op 6 accentknoppen en 6 selectievakjes (DataTemplate `Toegangstekst` met AccessText; de Fluent-sjablonen van `AccentButtonStyle` en CheckBox herkennen geen toegangstoets). Beide met een test.
    - Tests: 645 in totaal. In de Linux-cloudsessie groen: Domain 207, Application 225, Infrastructure 56, Architectuur 9; ViewModel-tests 137/139 (de 2 Windows-padtests). Nog op Windows te draaien: de nieuwe layouttest en een visuele controle van saldibalans, afdruk en de blauwe knoppen.
    - Ook opgelost in v0.4.7: de underscore op 6 selectievakjes (CheckBox-sjabloon van Fluent; dezelfde `Toegangstekst`).
  - **v0.4.8 Historisch boekjaar 2025: gereed** (2026-10-28). Besluiten 122–126. HistorieInlezer (T-rekeningen op alle werkbladen, SUM-regels overgeslagen), HistorieDienst, eigen tabellen (migratie Historie), wizard *Historie* (na de Excel-overname of via *Bestand › Importeren › Historie uit Excel…*), band "Historisch boekjaar", kaart met *Totaal volgens Voorblad* en ⚠, rapporten 2025 uit het Voorblad.
    - Uitwerking: de "wizardstap Historie" is een eigen wizard die de Excel-overname direct na afloop opent (selectievakje *Daarna ook de historie…*, standaard aan); zo is er één wizard voor beide routes.
    - Proef met de echte Excel 2025 (in de cloudsessie, niet bewaard): 61 rekeningen, 275 regels, 37 kloppen met het Voorblad, 4 wijken af (600, 1000, 1700, 8330), 19 zonder regels, 4 niet op het Voorblad.
    - Tests: 661 in totaal. In de Linux-cloudsessie groen: Domain 211, Application 231, Infrastructure 57, Architectuur 10; ViewModel-tests 141/143 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 661/661), de layouttest en een visuele controle.
  - **v0.4.9 Weergaven Expert en Eenvoudig: gereed** (2026-10-28). Besluiten 127–134. Woordenlijst (Application) met per term Expert, Eenvoudig en uitleg; Woordenboek (App) met indexer, aantallen en kolomkoppen per potje; app-resources `T.`/`U.` via `WoordenResources`, dus wisselen zonder herstart; *Beeld › Weergave*; keuze in de gebruikersinstellingen (enum `Weergavemodus`, standaard Expert); kolomkoppen Grootboekkaart via `Bindingsproxy`.
    - Tests: 679 in totaal. In de Linux-cloudsessie groen: Domain 211, Application 243, Infrastructure 57, Architectuur 11; ViewModel-tests 145/147 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 679/679, inclusief de nieuwe test `WoordenResourcesTests`) en een visuele controle van beide weergaven.
    - **Windows-test (2026-10-28, commit `3f0a8ff`):** 678/679 geslaagd. De UI-test `Start_zonder_database…` sloot de app niet af: de in v0.4.8 toegevoegde stap *Bestand › Importeren* klapte de menu's open via UI Automation en probeerde ze met Escape te sluiten (vermoedelijk zonder toetsenbordfocus). Opgelost in commit `00dfb30` met `Collapse()` en een diagnose bij een mislukte afsluiting (testprobleem, de app is ongewijzigd).
    - **Windows-test (2026-10-28, commit `e869c5c`): 679/679 geslaagd**, inclusief alle FlaUI-UI-tests. Correctie (Windows-run 2026-10-29): `Start_zonder_database…` faalde op de pc van de gebruiker ook op v0.4.9 (`cdbe6d2`, 2 van 2 runs), omdat het venster na `Collapse()` in de menumodus bleef; de 679/679 was dus niet reproduceerbaar. Opgelost in v0.4.10 (`b6c73f7`).
    - Pull request: één PR voor v0.4.7 t/m v0.4.9 (besluit 134), geopend na de geslaagde Windows-run.
    - Gemerged als PR #7 (2026-10-28, `cdbe6d2`).
  - **4c-3 Bijlagen: gereed**, v0.4.10 (2026-10-29). Besluiten 135–141. Bijlagen bij een post (ook geboekt), paperclip-kolom, filter *Zonder bon*, subtabblad *Postvak bonnen* met herkenning van datum en bedrag (PdfPig) en koppelvoorstel, voorbeeldvenster via `Windows.Data.Pdf`, tabellen `Bijlagen` en `BijlageInhoud` (migratie Bijlagen). Restpunten: tooltips via de woordenlijst en de IBAN-keuze in de SOL3-import (besluit 141).
    - Uitwerkingen: in het postvak kiest u de post in een keuzelijst (voorstellen met ✓ bovenaan, daarna de andere posten van dat jaar) in plaats van een aparte dialoog *Andere post kiezen…*; een verwijderd concept zet zijn bijlagen terug in het postvak; het doelplatform van de app is nu `net10.0-windows10.0.19041.0`.
    - Tests: 724 in totaal. In de Linux-cloudsessie groen: Domain 218, Application 264, Infrastructure 62, Architectuur 12; ViewModel-tests 154/156 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 724/724, inclusief de nieuwe `BijlageVoorbeeldTests`) en een visuele controle van het bijlagenblok, het postvak en het voorbeeldvenster met een echte bon.
    - **Windows-test (2026-10-29, Windows 11, commit `d8ed4e0`):** build zonder waarschuwingen; 720/724. Domain, Application, Infrastructure en Architectuur volledig groen. Gefaald: (1) de nieuwe venstertest van `BijlageVenster` (venster zonder `Application` mist de resource `Pictogrammen`; testprobleem), (2) `Start_zonder_database…` (ook op v0.4.9; venster blijft in de menumodus en sluit niet), (3) twee sneltoetstests instabiel als gevolg van (2). Een derde run bleef hangen op een achtergebleven app-venster.
    - Opgelost (testcode, de app is ongewijzigd): venstertest vervangen door een end-to-end test in de echte app plus een test van alleen het laden van een afbeelding; UI-tests beëindigen de menumodus vóór sneltoetsen en sluiten (met controle), `SetForeground()`, harde time-out van 3 minuten per UI-test, en het app-proces wordt altijd afgesloten. Tests: 725.
    - **Windows-test (2026-10-29, commit `b6c73f7`): 725/725 geslaagd in 2 opeenvolgende runs, inclusief alle FlaUI-UI-tests.** Na beide runs geen achtergebleven testproces.
    - Nog na te gaan door de gebruiker: kan het venster in de menumodus met Alt+F4 worden gesloten? Zo niet, dan is het een app-bug en volgt een regressietest.
    - **Creditnota's (besluiten 142–145), na de Windows-run:** minteken vóór of achter het bonbedrag blijft behouden en mag worden ingevuld (`BonHerkenning.TryParseBedrag`), koppelvoorstel bij een ontvangst op bank of kas, negatief bedrag in groen, *Zonder bon* ook voor ontvangsten (`ZonderBon.Geldbeweging`). Tests: 740. In de Linux-cloudsessie groen: Domain 218, Application 278, Infrastructure 62, Architectuur 12; ViewModel-tests 155/157 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 740/740) en de groene weergave in het postvak.
    - Uitvoermap van de app: `src/Verenigd.App/bin/<Config>/net10.0-windows10.0.19041.0/Verenigd.exe`.
    - **Windows-test (2026-10-29, commit `ae37699`): 740/740 geslaagd** (gemeld door de gebruiker).
    - Gemerged als PR #8 (2026-10-29, `d159768`). Fase 4c is daarmee gereed.
- **Fase 5 (Begroting & meerjarenplan):** besluiten 146–156 vastgesteld (2026-10-29).
  - **5a Begroting Vereniging: gereed**, v0.5.0 (2026-10-29). Subtabblad *Begroting* in Boekhouding (jaar, versie, overnemen + % afgerond op tientallen, vaststellen, herzien, naar rato per dag, Ctrl+S, Ctrl+P = ALV-afdruk), rapport *Begroting en werkelijk*, kolom *Baten/lasten* in het rekeningschema (`ResultaatKant`, migratie *Begroting* met voorstel uit het saldo via `MigratieSql.KantVoorstellen`). App-icoon (besluit 156).
    - Uitwerkingen: het overnemen en vaststellen staan in de werkbalk van het subtabblad (geen aparte dialogen); naar rato rekent per dag; een ongunstig verschil is rood.
    - **Windows-test (2026-10-29, commit `7a7c015`): 773/773 geslaagd** (gemeld door de gebruiker).
    - Daarna: Ctrl+5 / *Modules › Begroting* opent Boekhouding op het subtabblad Begroting (besluit 157), en het icoon is opnieuw gemaakt (besluit 158). Tests: 774; in de Linux-cloudsessie groen (ViewModel 163/165, de 2 Windows-padtests).
    - Tests: 773 in totaal. In de Linux-cloudsessie groen: Domain 226, Application 291, Infrastructure 65, Architectuur 14; ViewModel-tests 162/164 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 773/773) en een visuele controle van het subtabblad Begroting, de ALV-afdruk en het icoon.
    - Gemerged als PR #9 (2026-10-29, `e973ee1`), na groene CI (keuze van de gebruiker: mergen zonder extra Windows-run voor icoon en Ctrl+5).
  - **5b Meerjarenplan Stichting: gereed**, v0.6.0 (2026-10-30). Besluiten 159–167. Subtabblad *Meerjarenplan* (alleen Stichting): raster 10 jaar per resultaatrekening, knop → per rij (vanaf het jaar bij *Kopiëren vanaf*), resultaat per jaar, vaststellen en herzien, liggende afdruk (`RapportTabel.IsLiggend`), Ctrl+5 per administratie. Tabellen `Meerjarenplannen`, `Meerjarenposten`, `Meerjarenbedragen` (migratie *Meerjarenplan*).
    - Uitwerking: de knop per rij kopieert vanaf het jaar dat in de werkbalk bij *Kopiëren vanaf* staat (één keuze voor alle rijen).
    - Tests: 791 in totaal. In de Linux-cloudsessie groen: Domain 232, Application 296, Infrastructure 66, Architectuur 14; ViewModel-tests 168/170 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 791/791) en een visuele controle van het subtabblad Meerjarenplan en de liggende afdruk.
- **Startwizard en herinrichting UI:** besluiten 168–175 vastgesteld (2026-10-30).
  - **Startwizard: gereed**, v0.7.0 (2026-10-30). Eigen venster *Verenigd instellen* met acht stappen, automatisch bij de eerste start en via *Bestand › Startwizard…*; voortgang in tabel `Startwizard` (migratie *Startwizard*); ✓ per stap uit `Inrichtingsstatus`.
    - Uitwerkingen: "Stichting aan of uit" uit het ontwerp is niet gebouwd (beide administraties bestaan altijd, besluit 4); de tarieven vult u in op Contributie › Tarieven (de wizard onthoudt de stap en opent dat subtabblad); ✕ werkt als *Later verder*; de bankstap kan pas als de boekhouding van de Vereniging is ingericht. Gevonden en opgelost: de kleurrondjes bij Instellingen › Administraties werkten niet.
    - Tests: 805 in totaal. In de Linux-cloudsessie groen: Domain 232, Application 300, Infrastructure 67, Architectuur 14; ViewModel-tests 176/178 (de 2 Windows-padtests).
    - **Windows-test (2026-10-30, Windows 11, commit `b14bb81`): 805/805 geslaagd**, inclusief alle FlaUI-UI-tests (Domain 232, Application 300, Architectuur 14, Infrastructure 67, App 192); build zonder waarschuwingen, geen achtergebleven testproces. Nog door de gebruiker te doen: visuele controle van de wizard, de kleurrondjes in Instellingen, het subtabblad Meerjarenplan en de liggende afdruk.
  - Pull request: één PR voor v0.6.0 t/m v0.7.1 (besluiten 176 en 189). Gemerged als PR #11 (2026-10-30, `a5a2968`), na groene CI.
  - **Herinrichting UI: gereed**, v0.8.0 (2026-10-30). Besluiten 168, 177–183 en 198. Navigatiebalk met uitklapbare onderdelen, wisselaar voor de administratie, laatste pagina herstellen, statusregel ✓/⚠/✕, menubalk in Eenvoudig via Alt, startpagina met kaarten, één hoofdactie per pagina, lege schermen, keuze *Beginjaar* bij het inrichten.
    - Uitwerkingen: de pagina's heten in de code nog `TabbladViewModel` (geen hernoeming, om het risico klein te houden); de begroting staat als eigen item in de navigatiebalk en niet ook onder Boekhouding; *Opslaan* is alleen blauw waar opslaan de hoofdactie is; *Boeken*, *Toewijzen*, *Koppelen* en *Definitief maken* zijn gewone knoppen geworden. De tabbladopdrachten (sluiten met Ctrl+W) zijn vervallen.
    - Tests: 843 in totaal. In de Linux-cloudsessie groen: Domain 232, Application 315, Infrastructure 67, Architectuur 26; ViewModel-tests 184/186 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 843/843, met de omgebouwde FlaUI-UI-tests en de nieuwe test `Eenvoudig_verbergt_de_menubalk…`) en een visuele controle van de navigatiebalk, de wisselaar, de startkaarten, de lege schermen en de menubalk in Eenvoudig (Alt).
    - **Windows-test (2026-10-30, Windows 11, commit `8a82c11`): niet groen.** Build zonder waarschuwingen. Gemeten 841 tests (Domain 232, Application 315, Architectuur 26, Infrastructure 67, App 201; niet de verwachte 843). Domain, Application, Architectuur en Infrastructure in alle runs groen. App-tests: run 1 200/201, run 2 195/201, run 3 (alleen App) 195/201.
      - Run 1: `Eenvoudig_verbergt_de_menubalk_en_alt_toont_hem_tijdelijk` faalde met `ElementNotAvailableException` / `COMException 0x80040201` (UIA-gebeurtenis); in run 2 en 3 faalde deze test op de oorzaak hieronder.
      - Run 2 en 3: dezelfde 6 tests faalden (`Laatste_database_openen…`, `Geimporteerde_leden…`, `Contributie_toont…`, `Boekhouding_leeg_beginnen…`, `Bijlage_bekijken…`, `Eenvoudig_verbergt…`), allemaal bij het wachtwoordvenster: de titel bleef "Verenigd". Een schermafbeelding toont het venster *Database openen* met de cursor in een **leeg** wachtwoordveld; het getypte wachtwoord en Enter kwamen niet aan, omdat het app-venster niet de echte voorgrond had (Windows foreground lock; VS Code stond op de voorgrond). Testprobleem, geen app-fout: de wachtwoordstap gebruikt `Focus()` + `Keyboard.Type` zonder `SetForeground()` (dat zit wel in `Sneltoets`).
      - Gevraagd: vóór het typen van het wachtwoord `venster.SetForeground()` (en `dialoog.SetForeground()`) aanroepen en controleren dat het wachtwoordveld de toetsenbordfocus heeft; liefst één hulpmethode voor alle tests die de database openen. Daarna opnieuw de volledige suite op Windows, 2 runs.
      - Risico: omdat de toetsen niet in de app terechtkwamen, kan het testwachtwoord in een ander venster getypt zijn; overweeg de UI-tests te laten stoppen als het app-venster niet op de voorgrond komt.
      - Opgelost (2026-10-30): `VoerWachtwoordIn` (één hulpmethode voor alle 6 tests) haalt venster en dialoog naar voren, zet de focus en typt pas als de systeembrede focus op het wachtwoordveld van dit app-proces ligt; anders stopt de test zonder te typen. `Sneltoets` controleert hetzelfde. Daarnaast een app-fout gevonden die de `ElementNotAvailableException` verklaart: bij uitklappen bouwde de navigatiebalk alle regels opnieuw op, waardoor de gekozen regel (en de focus) verdween; nu worden de regels bijgewerkt (`WerkNavigatieBij`), met een regressietest. Het juiste aantal tests is 842 (de schatting van 843 was te hoog; 841 + 1 nieuwe test).
      - Gebruiker (2026-10-30): de UI is goed; later nog aantrekkelijker maken. **Nog op Windows te draaien:** de volledige suite, 2 runs (verwacht 842/842).
  - **Uitleg SOL3-exports: gereed**, v0.7.1 (2026-10-30). Besluiten 185–188. `Sol3Exportuitleg` (Application, verplicht uit `ExportHerkenning`) en het gedeelde onderdeel `Sol3Uitleg` in de stap Leden van de startwizard en (uitklapbaar) in stap 1 van de SOL3-importwizard.
    - Tests: 812 in totaal. In de Linux-cloudsessie groen: Domain 232, Application 307, Infrastructure 67, Architectuur 14; ViewModel-tests 176/178 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 812/812, de UI-test van de importwizard controleert nu ook de uitleg) en een visuele controle.
    - **Windows-test (2026-10-30, Windows 11, commit `bc8cc09`): 813/813 geslaagd**, inclusief alle FlaUI-UI-tests (Domain 232, Application 308, Architectuur 14, Infrastructure 67, App 192); build zonder waarschuwingen, geen achtergebleven testproces. Eén Application-test meer dan de verwachte 812 (vermoedelijk toegevoegd in `bc8cc09` na de telling). Nog door de gebruiker te doen: visuele controle van de uitleg in de startwizard en de importwizard.
    - Klikroute (besluit 187) aangeleverd en verwerkt, met per export de rapportnaam "invoer voor Verenigd, output #1–4" en exporteren met "Alleen details" en "CSV, door komma's gescheiden".
    - Na de Windows-run: het scheidingsteken wordt herkend (besluit 190), met 3 nieuwe tests. Tests: 816 in totaal; in de Linux-cloudsessie groen (Application 311). **Nog op Windows te draaien:** de volledige suite (verwacht 816/816).

- **Fase 6 (E-mail via Gmail): gereed**, v0.9.0 (2026-10-30). Besluiten 191–196 en 203–208; uitgewerkt ontwerp goedgekeurd (navigatie E-mail › Opstellen/Verzonden/Sjablonen, Instellingen › E-mail, Gmail-API met alleen versturen, MimeKit, standaardsjablonen, daglimiet, logboek zonder tekst). Zie CHANGELOG.md, docs/ARCHITECTUUR.md en de handleiding docs/GOOGLE-CLOUD.md.
  - Uitwerkingen: andere pagina's bieden een bericht aan via `TabbladViewModel.EmailAanbieden` (`EmailVoorstel`); de welkomstmail, vooraankondiging en factuurmail worden na een vraag aangeboden, de stornering en de herinnering via een knop. Extra knop *Mailen…* bij een definitieve factuur (ook later nog te mailen). Bij Instellingen › E-mail gaat de afzender mee met Opslaan; inlezen, koppelen en ontkoppelen worden direct bewaard. Koppelen wacht hooguit 5 minuten op de browser.
  - Tests: 889 in totaal. In de Linux-cloudsessie groen: Domain 241, Application 329, Infrastructure 69, Architectuur 27; ViewModel-tests 205/207 (de 2 Windows-padtests). **Nog op Windows te draaien:** de volledige suite (verwacht 889/889, met de nieuwe UI-test `Email_toont_onderdelen_koppelmelding_en_standaardsjablonen`), 2 runs, en een echte koppeling met Gmail (handleiding volgen, proef naar mezelf, één bericht aan een testgroep).
  - Pull request: v0.8.0 en v0.9.0 staan op dezelfde branch; één PR na een geslaagde Windows-run (de v0.8.0-run, verwacht 842/842, is daarmee vervangen door de run van v0.9.0).

- **Taken voor later:** XAF-export en -import (besluit 197), nog niet ingepland.

## Privacy

- Echte leden-, bank- en boekhoudgegevens komen NOOIT in Git. Voorbeeldbestanden worden geanonimiseerd. Bronbestanden staan in een map die in `.gitignore` staat.
- Log geen persoonsgegevens of IBAN's.
