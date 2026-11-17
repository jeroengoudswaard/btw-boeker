# BTW-Boeker: UX-review

De UX-review en de volgorde van ronde 2, verplaatst uit CLAUDE.md (2026-11-17). De besluiten die eruit volgden staan in docs/BESLUITEN.md (32–38).

## UX-review (ronde 1, 2026-11-15)

**Opdracht van de gebruiker.** Kijk als een topontwerper naar de hele app en noteer wat goed is en wat te ingewikkeld is voor een gemiddelde gebruiker. De doelen:
- geen bedieningselementen die op dat moment geen functie hebben;
- zoveel mogelijk harmonie;
- de eenvoudigste versie die dezelfde functionaliteit geeft.

Neem zoveel mogelijk over uit het moederproduct (het product waarvan BTW-Boeker is afgeleid). Daar zit al veel goed werk in.

In ronde 2 wordt de app vereenvoudigd en geharmoniseerd. Volgens de huisregels gebeurt dat stap voor stap, met per stap een ontwerpvoorstel ter goedkeuring.

**Bron en beperking.** Bekeken zijn:
- alle XAML-schermen;
- de schermafbeeldingen van v0.20.0 uit de demodatabase: Start, Journaal en Rapporten, licht, 1024×768.

Niet live bekeken: Bank, Postvak, de onderdelen onder *Meer…*, de startwizard, de dialogen, de weergave Eenvoudig en donker. Ronde 2 begint daarom met schermafbeeldingen van elk scherm. Punten met *(nagaan)* zijn uit de code afgeleid en nog niet op het scherm gezien.

### Wat we overnemen uit het moederproduct
Het moederproduct had dezelfde review (ronde 1, 2026-11-15) en heeft stap A al gebouwd en uitgebracht: een *rustiger schil*. BTW-Boeker is van een eerdere versie afgesplitst en heeft die verbeteringen nog niet.

Over te nemen:
- **Vensterrand.** Windows tekent de schaduw en de rand van het hoofdvenster: `GlassFrameThickness` is niet 0, en `VensterrandDwm` geeft afgeronde hoeken en een accentrand op Windows 11. Bewaakt met een architectuurtest.
  - BTW-Boeker heeft nog `GlassFrameThickness="0"`, dus het venster heeft geen schaduw en is slecht te zien boven andere vensters.
- **Meldingsbalk in plaats van de statusbalk.** Meldingen staan rechts in de paginakop met ✓/⚠/✕ op een gekleurde achtergrond. ✓ verdwijnt na 6 s; ⚠ en ✕ blijven tot ze gesloten worden of tot een andere pagina.
- **Paginatitel.** De titel is het onderdeel waar de gebruiker is (*Journaal*), met de module klein erboven als kruimel, plus één zin uitleg per onderdeel uit de woordenlijst.
- **Venstertitel.** 🔒 *bestand – BTW-Boeker*, zonder de naam van de administratie.
- **Opslaan alleen bij wijzigingen.** De knop staat er alleen als er iets op te slaan is, en is dan de hoofdactie. Ctrl+S werkt altijd.
- **Menubalk zonder *Modules* en *Extra*.** *Instellingen* (Ctrl+,) gaat naar *Bestand*.
- **Eén teller per regel**, een **focusrand** alleen bij toetsenbordgebruik, en een **onderdeelpaneel** zonder lege lijn.
- **De leidende principes** van die review (hieronder, aangevuld voor BTW-Boeker).
- **Lessen uit dat werk:**
  - schermafbeeldingen maken met een niet-gemaximaliseerd venster en een DPI-bewust testproces;
  - een melding niet dubbel tonen: onderaan de pagina én in de meldingsbalk.

We nemen de UI-onderdelen en het ontwerp over, nooit namen, teksten of woorden uit het oude domein; `ProductnaamTests` blijft dat bewaken. **Raakt besluit 31** ("geen code uit het product waarvan BTW-Boeker is afgeleid"). Daarvoor is eerst een besluit van de gebruiker nodig (open vraag 1).

### Leidende principes voor ronde 2
1. **Eén ding één keer.** Elk gegeven en elke actie staat op één plek.
2. **Alleen wat nu iets doet.** Geen uitgeschakelde knoppen, lege detailvlakken, lege werkbalken of tellers met 0.
3. **De lijst is het scherm.** De lijst gebruikt de volle breedte. Een detail verschijnt pas bij selectie of bij *Nieuw…*, en dan schuift de lijst in.
4. **Eén patroon per soort handeling.** "Nieuw …" staat linksboven; bevestigen (opslaan, boeken) staat rechtsonder in het vlak waar het bij hoort. Er is één manier van opslaan.
5. **De status bepaalt de acties.** Een concept toont *Boeken* en *Verwijderen*, een geboekte post *Tegenboeken*; nooit allebei. (In de posteditor gaat dit al goed.)
6. **Gewone taal in de interface.** Geen bestandsformaten (CAMT.053, XAF 3.2, .btwdb), sneltoetsen of interne begrippen in lopende tekst. Die horen in tooltips, menu's of de wizard.
7. **Alles leidt naar de maandaangifte.** De app bestaat voor de btw-aangifte per maand (besluiten 9 en 13). Start en navigatie maken zichtbaar wat er deze maand nog moet gebeuren voordat de aangifte klopt. Dat principe is nieuw voor BTW-Boeker.

### Wat goed is (behouden)
- **Het welkomstscherm.** Drie tegels, waarvan één gekleurd, met *Laatst geopend* eronder. Rustig en duidelijk.
- **Start.** De *Te doen*-kaarten verschijnen alleen als er iets te doen is, elk met één vervolgknop. Bij niets te doen staat er "✓ Alles is bij".
- **Het gedeelde onderdeel `LeegScherm`.** Een uitleg en één knop; het juiste patroon voor elke lege lijst.
- **Wat al alleen verschijnt als het nodig is:**
  - de boekjaarkeuze bij meer dan één jaar;
  - de bankrekeningkeuze bij meer dan één rekening;
  - het filter *Zonder bon* bij posten zonder bon;
  - *N concepten boeken* bij concepten;
  - het invoerveld *+ Rubriek* pas na een klik.

  Dit is precies principe 2.
- **Navigatie links.** Gekleurde pictogrammen, tellers, en de inrichting weggestopt onder *Meer…*.
- **Toegankelijkheid.** Labels met toegangstoetsen, AutomationIds en LiveSetting. Die mogen bij het vereenvoudigen niet verloren gaan.
- **De rapporten.** Een rustige tabel met afdrukken en exporteren bovenaan; de saldibalans is overzichtelijk.

### Bevindingen: schil (geldt voor elke pagina)
- **Naam en bestand staan er twee keer:** in de titelbalk (*Vakantiehuis De Voorbeeld – vakantiehuis.btwdb*) en in de statusbalk. Er is maar één administratie (besluit 26). → Venstertitel zoals in het moederproduct, statusbalk weg.
- **De statusbalk** toont vooral vaste gegevens: bestand, naam, boekjaar en "Gereed". → Meldingsbalk (zie hierboven). Het boekjaar komt in de paginakop.
- **De paginatitel is de module ("Boekhouding"), niet de plek ("Journaal").** → Titel en uitleg per onderdeel.
- **Opslaan staat altijd rechtsonder**, ook uitgeschakeld op Rapporten, waar niets op te slaan is. Daarnaast staat de tekst "Niet-opgeslagen wijzigingen". → Alleen tonen bij wijzigingen; de tekst vervalt.
- **De menubalk.** *Modules* herhaalt de navigatie; *Extra* bevat alleen *Instellingen*. → Beide weg. *Thema* gaat naar Instellingen; *Beeld* houdt *Weergave* en *Vorige/Volgende pagina*.
- **Twee tellers voor hetzelfde:** Boekhouding (1) en Journaal (1). → Bij een uitgeklapt onderdeel alleen de teller bij het subitem.
- **De focusrand.** Op Start staat een zwarte rechthoek om de hele navigatielijst, en het gekozen item heeft een gestippelde rand. → Een focusrand alleen bij toetsenbordgebruik, om de regel zelf.
- **Een lege werkbalk.** Boven het Journaal staat een lijn waar de verborgen boekjaarkeuze en de uitvoerknoppen horen. → Geen lijn zonder inhoud.
- **De navigatie heeft één niveau te veel.** Er is één administratie en vrijwel één module (Boekhouding). *Boekhouding* is dus alleen een map om Journaal, Bank, Bonnen en Rapporten.
  - → Plat maken: **Start · Journaal · Bank · Bonnen · Rapporten · Meer… · Instellingen**, elk met een eigen kleur.
  - Na stap 3 komt **Btw-aangifte** erbij, direct onder Start.
  - Raakt besluit 28 (navigatie Start · Boekhouding · Instellingen); open vraag 2.
- **Puntjes.** "…" en "..." staan door elkaar (*Afdrukken...* naast *Indeling bewerken…*). → Overal "…".
- **Sneltoetsen in lopende tekst.** Voorbeelden:
  - "(Ctrl+B)" op een startkaart;
  - "Maak een post (Ctrl+N)";
  - "Opslaan met Ctrl+S." onder Boekingsregels, Relaties en Rekeningschema.

  → Alleen in tooltips en menu's.
- **Toelichtingen onderaan pagina's** (Postvak, Boekingsregels, Relaties, Rekeningschema, Boekjaren). → In `LeegScherm` bij een lege lijst, anders een ⓘ-tooltip bij de kop.
- **Weergave Expert/Eenvoudig.** Twee woordenlijsten verdubbelen elke tekst en elke test. Met één gebruiker en één administratie is er misschien één taal genoeg. → Open vraag 3.

### Bevindingen per scherm
- **Welkom**
  - De ondertitel belooft "facturen", maar er is geen facturenonderdeel *(nagaan; besluit 7 noemt verkoopfacturen)*. → Weglaten, of de belofte waarmaken.
  - "Kies een bestaand .btwdb-bestand" en "Herstel een .btwbackup-back-up" noemen extensies. → "Open een bestaande administratie" en "Zet een back-up terug".
- **Startwizard**
  - Teksten uit het oude model: "database, **administraties**, boekhouding en bank". Ook de tooltip van *Startwizard…* in het menu.
  - Vijf knoppen onderaan: *Later verder*, *Overslaan*, *Vorige*, *Volgende* en *Voltooien*; op de laatste stap ook nog *Volgende* *(nagaan)*. → *Vorige* en één hoofdknop die *Volgende* of *Voltooien* heet; *Overslaan* als link bij een stap die overgeslagen mag worden. *Later verder* kan vervallen, omdat sluiten hetzelfde doet.
  - "Een bankafschrift in CAMT.053-formaat" → "Een bankafschrift van de Rabobank (download als CAMT.053)", met de uitleg in een ⓘ.
  - De stap *Boekhouding* biedt *Sjabloon…* en *Leeg beginnen*. Volgens besluit 28 wordt dat *Rekeningschema (XAF)*, en dat hoort bij stap 2b.
  - Op *Klaar* staat "via Instellingen of Boekhouding". Dat moet kloppen na de nieuwe navigatie.
- **Start**
  - De kerncijfertitel is afgekapt ("Resultaat 2026 t/…"). Er zijn vier kolommen voor één kaart. → Korte titel; het raster volgt het aantal kaarten.
  - *Inrichting afmaken* toont twee getallen: het tegeltje "3" en de tekst "2 van 5 stappen klaar". → Eén getal: "Nog 3 stappen".
  - Er is nog niets over btw. → Na stap 3 een hoofdkaart **"Btw <maand>: € … te betalen, aangeven vóór <datum>"**, met wat er nog openstaat: ongeboekte bankregels, bonnen en concepten van die maand (principe 7).
- **Journaal**
  - De lijst is 2/5 van de breedte, met een horizontale scrollbalk en drie zichtbare kolommen. Rechts staat een leeg vlak met "Selecteer een post…".
  - Het zoekveld heeft geen label of placeholder: een leeg vak onder *Nieuwe post*.
  - → De lijst over de volle breedte, een zoekveld met de placeholder "Zoeken", en de posteditor als paneel bij selectie of bij *Nieuwe post*.
  - De kolom *Nummer* (MEM-2026-0031) is voor een gebruiker niet belangrijk. → Datum, Omschrijving, Bedrag en Status. Het nummer staat in het detail.
- **Posteditor**
  - "Debet … · credit …" en de kolomkoppen Debet/Credit staan vast in de XAML, buiten de woordenlijst *(nagaan in Eenvoudig)*.
  - *Bijlage toevoegen* staat er twee keer: als link zonder bijlage en als knop met bijlage. → Eén knop, plus slepen.
  - Voor de btw (stap 3) komt er per regel een tarief bij (besluit 10). De editor wordt daardoor breder. → Bedrag en tarief invoeren; grondslag en btw berekent de app en toont ze klein onder de regel, niet in eigen kolommen.
- **Bank**
  - Twee werkbalkregels:
    - de rekening en het saldo;
    - *boeken*, *regels toepassen*, *inlezen* en het filter *Tonen*.
  - → Eén regel: het filter, *Inlezen* en het saldo. *N concepten boeken* blijft als hoofdactie zodra er concepten zijn. *Regels toepassen* gebeurt vanzelf na het wijzigen van een regel.
  - In het detail staan *Soort* (met code), *Toewijzen* en *Regel maken van deze boeking…*, ook bij een al verwerkte transactie *(nagaan)*. → Bij te verwerken: rekening, omschrijving en *Toewijzen*. *Regel maken* als link eronder. Bij verwerkt: alleen de verwijzing naar de post.
- **Postvak bonnen**
  - Vier knoppen per bon: *Koppelen*, *Tonen…*, *Datum en bedrag bewaren* en *Uit postvak verwijderen…*. → *Koppelen* als hoofdactie. Datum en bedrag worden vanzelf bewaard. Tonen via dubbelklik of een voorbeeld. Verwijderen in het contextmenu.
  - De vaste uitleg onderaan ("Sleep bonnen op deze lijst…") → in `LeegScherm` en als placeholder van de lijst.
  - Naam: in de navigatie *Postvak bonnen* → *Bonnen*.
- **Rapporten**
  - De ⚠-melding over concepten is afgekapt ("Boek ze in het Journaal om :"). → Meldingsbalk, of een eigen regel met een knop *Naar het journaal*.
  - De rapportkeuze is een keuzelijst met veel soorten. Na stap 3 komt de **maandaangifte** bovenaan, met de rubrieken zoals in Mijn Belastingdienst (besluit 14).
  - *Rekeningen tonen* en *Indeling bewerken…* verschijnen alleen bij de jaarrekening. Dat is goed.
- **Onder *Meer…*: Boekingsregels, Relaties, Rekeningschema, Grootboekkaart, Boekjaren**
  - *Boekingsregels*: een tabel met 11 bewerkbare kolommen, met keuzelijsten en vinkjes in elke rij. Dit is het drukste scherm van de app.
    - → Een lijst met de naam en "wanneer → rekening", en een detailpaneel voor de voorwaarden.
    - Het gewone pad blijft *Regel maken* vanuit Bank.
    - De toelichting noemt "Kruisposten" en "Ctrl+S".
  - *Relaties*: de vinkjes *Debiteur* en *Crediteur* zijn jargon. Bij verhuur zijn er maar een paar relaties (verhuurorganisatie, energie, water).
    - → *Klant* / *Leverancier*.
    - Is er geen facturenonderdeel, dan is het de vraag of Relaties een eigen onderdeel nodig heeft *(nagaan waarvoor het nu dient)*.
  - *Rekeningschema*: een tabel met 9 bewerkbare kolommen (Soort, Kant, Rol: jargon). Na de XAF-import (besluit 25) wordt het schema zelden gewijzigd.
    - → Alleen-lezen lijst (nummer, naam, saldo) met bewerken in een detailpaneel.
    - *Rol* en *Kant* alleen bij de rekeningen waar ze iets doen.
  - *Grootboekkaart* → geen eigen item, maar doorklikken vanuit Rapporten en Rekeningschema.
  - *Boekjaren*:
    - drie knoppen per rij;
    - *Volgend boekjaar openen* staat er altijd;
    - *Auditfile (XAF)…* staat er dubbel met *Bestand › Exporteren*.

    → Acties alleen bij het gekozen jaar, volgens de status; exporteren op één plek.
  - **Tooltip uit het oude domein:** *Bestand › Exporteren › Rekeningschema als sjabloon* zegt "om te delen met andere groepen". Herschrijven.
- **Instellingen**
  - Eén kaart (naam en drempel) met een eigen *Opslaan* en "Niet-opgeslagen wijzigingen". → Direct bewaren bij wijzigen, zoals in Windows-Instellingen. *Thema* en *Weergave* horen hier ook.
  - De *drempel "zonder bon"* hoort bij het filter in het Journaal, niet bij de naam van de administratie *(keuze in ronde 2)*.

### Stap 0: nieuw gevonden in de opnames (2026-11-15)
- **Start**
  - "Resultaat 2026 t/m vandaag € 0,00" staat naast 62 concepten; de gebruiker ziet zijn werk niet terug. → Ook de stand met concepten tonen.
  - "356 boekingen zonder bon" bij een drempel van € 10 is ruis en geen taak.
- **Journaal**
  - Bij een geboekte post staan de prullenbakjes per regel er nog, plus een lege kolom Omschrijving.
  - De kop zegt "Geboekt op 15-11-2026": dat is de boekdatum, niet de datum van de post.
  - Kolommen afgekapt ("Be", "Geboel").
- **Bank**
  - Er staat "Saldo bank … · grootboek (met concepten) … — verschil € 77.981,32": boekhoudtaal en een alarmerend getal, zonder uitleg of actie.
  - *Toewijzen* staat uitgeschakeld tot er een rekening is gekozen.
  - De uitleg onderaan het detail is afgekapt en heeft een eigen scrollbalk.
- **Donker:** de gekozen rij in een tabel is lichtcyaan met donkere tekst, en valt uit de toon.
- **Lege lijsten (Bonnen, Relaties)** tonen vijf à zes elementen tegelijk:
  - de knop twee keer (werkbalk en `LeegScherm`);
  - een teller "0 …";
  - een lege tabel met koppen;
  - "Kies een … in de lijst";
  - de uitleg onderaan, bij Relaties dezelfde tekst als in `LeegScherm`.
- **De boekjaarkeuze** staat boven elk onderdeel, ook waar ze niets doet: Boekingsregels, Relaties, Rekeningschema, Boekjaren.
- **Boekingsregels**
  - Koppen afgekapt ("Naan", "Omsc", "Trans", "Let o").
  - Meestal lege kolommen: Tegen-IBAN, bedragen, transactiecode, Let op.
- **Rekeningschema**
  - De kolommen Rubriek en Let op zijn leeg.
  - Saldo staat hoger in de cel dan de rest van de rij.
  - Bank (1100) toont € 0,00 terwijl de bank € 92.665,07 laat zien, zonder uitleg.
- **Grootboekkaart**
  - Opent zonder gekozen rekening: een lege tabel en "Beginsaldo · eindsaldo" zonder bedragen.
  - Boekjaar én Van/Tot en met: twee manieren om de periode te kiezen.
- **Boekjaren**
  - *Afsluiten…* staat ook bij 2026, met 62 concepten, terwijl 2025 nog open is: een knop die niet kan.
- **Instellingen**
  - De kaart staat 24 px links van de paginatitel.
- **Startwizard**
  - Er staan vijf knoppen onderaan, waarvan op stap 1 en stap 5 twee of drie uitgeschakeld.
  - Op stap 5 staat Welkom als niet gedaan (○).
- **Welkom**
  - Zonder database staan er een navigatiekolom met alleen Start, en een statusbalk "Geen database geopend · Gereed".
- **Eenvoudig**
  - "Postvak bonnen" is niet vertaald.
  - Debet en Credit staan vast in de posteditor; dat is nu bevestigd.

### Voorgestelde volgorde voor ronde 2 (elk een eigen voorstel ter goedkeuring)
0. **Schermafbeeldingen van elk scherm**, licht en donker, uit de demodatabase, met een niet-gemaximaliseerd venster. Daarbij een ontwerpcanvas met per scherm de bevindingen en een schets, zoals in het moederproduct.
1. **Schil, overgenomen uit het moederproduct:**
   - vensterrand;
   - meldingsbalk in plaats van de statusbalk;
   - paginatitel met kruimel;
   - venstertitel;
   - Opslaan alleen bij wijzigingen;
   - menubalk zonder *Modules* en *Extra*;
   - één teller, focusrand, geen lege lijn.

   Weinig risico en overal zichtbaar.
2. **Platte navigatie**: Start · Journaal · Bank · Bonnen · Rapporten · Meer… · Instellingen. Na open vraag 2.
3. **Lijst en detail**: één patroon voor Journaal, Bank, Bonnen, Boekingsregels, Relaties en Rekeningschema.
4. **Acties volgens status**: Bank, Bonnen en Boekjaren.
5. **Teksten**:
   - jargon, extensies en sneltoetsen uit de lopende tekst;
   - "…";
   - resten van het oude model (de startwizard, "groepen");
   - de belofte "facturen".
6. **Instellingen**: direct bewaren, met thema, weergave en drempel op de juiste plek.

Stap 3 van de planning (btw-integratie) bouwt daarna op dit patroon: de maandaangifte als hoofdkaart op Start en als eerste rapport.

### Open vragen aan de gebruiker (beantwoord 2026-11-15: 1A 2A 3A 4A, zie besluiten 32–35)
1. **Besluit 31 verduidelijken.** Mogen generieke UI-onderdelen (code) uit het moederproduct worden overgenomen, zonder namen en woorden uit het oude domein? Of alleen het ontwerp, zodat de code opnieuw wordt geschreven?
2. **Platte navigatie** in plaats van Start · Boekhouding · Instellingen (besluit 28)?
3. **Weergave Expert/Eenvoudig** houden, of één taal? En zo ja, welke?
4. **Volgorde.** Eerst ronde 2 (stappen 0–6), of eerst stap 2b (XAF-import) en daarna ronde 2?
