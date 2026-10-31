# E-mail koppelen met Gmail

Verenigd verstuurt e-mail via de Gmail-API van een bestaand gmail.com-account (besluiten 192 en 193). Elke administratie (Vereniging en Stichting) heeft een eigen account en koppeling (besluit 211): doorloop stap 4 per administratie. Eén Google Cloud-project mag voor beide; voeg dan beide Gmail-adressen toe en koppel elk blok met het eigen account. De app krijgt alleen toestemming om **te versturen**: hij kan geen e-mail lezen en kent het wachtwoord niet. Daarvoor maakt u eenmalig een eigen project in Google Cloud. Dat is gratis en duurt ongeveer een kwartier.

> Het Gmail-adres, het clientbestand en de toegangssleutel komen alleen in de versleutelde database van Verenigd. Zet het clientbestand nooit in Git en verwijder het na het inlezen van uw pc.

## 1. Project maken en de Gmail-API aanzetten

1. Ga naar <https://console.cloud.google.com/> en log in met het Gmail-account waarmee u wilt versturen.
2. Kies bovenaan **Project selecteren › Nieuw project**. Geef het een naam, bijvoorbeeld `Verenigd`, en kies **Maken**.
3. Open in het menu **API's en services › Bibliotheek**, zoek **Gmail API** en kies **Inschakelen**.

## 2. Het toestemmingsscherm (OAuth consent screen)

1. Ga naar **API's en services › OAuth-toestemmingsscherm** (in nieuwere versies: **Google Auth Platform › Branding**).
2. Kies het gebruikerstype **Extern**.
3. Vul de app-naam (`Verenigd`), uw e-mailadres als ondersteunings- en contactadres in en sla op.
4. Voeg bij **Gegevenstoegang** (Scopes) het bereik `https://www.googleapis.com/auth/gmail.send` toe.
5. Zet bij **Doelgroep** de publicatiestatus op **In productie** (*Publish app*).
   - In de testmodus vervalt de toestemming na 7 dagen en moet u steeds opnieuw koppelen.
   - Google hoeft de app niet te controleren: alleen u gebruikt hem. Bij het koppelen ziet u daarom een waarschuwing *Google heeft deze app niet geverifieerd*. Kies daar **Geavanceerd › Ga naar Verenigd (onveilig)**. Het is uw eigen project.

## 3. Het clientbestand maken

1. Ga naar **API's en services › Inloggegevens** (of **Google Auth Platform › Clients**).
2. Kies **Inloggegevens maken › OAuth-client-ID**.
3. Kies als type **Desktop-app** en als naam `Verenigd`. Kies **Maken**.
4. Kies **JSON downloaden**. U krijgt een bestand zoals `client_secret_….json`.

## 4. Koppelen in Verenigd

1. Open **Instellingen** en ga naar het onderdeel **E-mail (Vereniging)** of **E-mail (Stichting)**.
2. Vul **Uw naam (afzender)** in, bijvoorbeeld *Penningmeester Scouting …*, en **Het Gmail-adres**. Kies **Opslaan**.
3. Kies **Clientbestand inlezen…** en kies het JSON-bestand uit stap 3.
4. Kies **Koppelen met Google…**. De browser opent:
   - kies het juiste Gmail-account;
   - geef toestemming om e-mail namens u te versturen.

   De status wordt dan **✓ Gekoppeld**.
5. Verwijder het JSON-bestand van uw pc. Verenigd heeft het niet meer nodig.

## Goed om te weten

- **Daglimiet:** Gmail staat ongeveer 500 berichten per dag toe. Verenigd stopt daar en meldt hoeveel berichten niet verstuurd zijn. Verstuur die de volgende dag opnieuw.
- **Verzonden berichten** staan ook in de map *Verzonden* van Gmail. Verenigd bewaart alleen aan wie, wanneer, het onderwerp en het sjabloon.
- **Toegang intrekken** kan altijd via <https://myaccount.google.com/permissions>. Kies daarna in Verenigd **Ontkoppelen** en koppel opnieuw als u weer wilt versturen.
- **Melding "De koppeling met Google is verlopen of ingetrokken":** koppel opnieuw via Instellingen › E-mail.
