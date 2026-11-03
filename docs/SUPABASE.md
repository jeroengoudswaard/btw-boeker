# Webapp voor de leiding: Supabase en Cloudflare inrichten

Deze handleiding hoort bij fase 7 (v0.12.0, besluiten 33–38 en 222–233). U doet dit eenmalig. Reken op ongeveer een uur.

Wat er na afloop staat:
- **Supabase** (database en inloggen), in de EU-regio Frankfurt, met het gratis abonnement (besluit 228);
- **Cloudflare Pages** met de webapp op een eigen adres (besluit 227);
- **Verenigd** op uw pc, gekoppeld als beheerder met wachtwoord en authenticator-app.

> **Nooit** de `service_role`-sleutel in Verenigd, in de webapp of in Git zetten (besluit 37). Verenigd en de webapp gebruiken alleen de **publieke** sleutel; de beveiliging zit in de database.

## 1. Supabase-project aanmaken

1. Maak een account op [supabase.com](https://supabase.com) (bij voorkeur met het e-mailadres van de beheerder).
2. Kies **New project**:
   - naam: bijvoorbeeld `verenigd`;
   - regio: **Central EU (Frankfurt)**;
   - een sterk databasewachtwoord (bewaar het in uw wachtwoordbeheerder; Verenigd heeft het niet nodig).
3. Sluit bij **Organization › Legal documents** de verwerkersovereenkomst (DPA) af. Die is nodig voor de AVG.

## 2. Inloggen en MFA instellen

1. **Authentication › Sign In / Providers › Email**: aan. Zet **Allow new users to sign up** uit (alleen de beheerder nodigt uit, besluit 230).
2. **Authentication › Multi-Factor**: **TOTP** aan (verificatie met een authenticator-app).
3. **Authentication › URL Configuration**:
   - **Site URL**: het adres van de webapp (stap 5), bijvoorbeeld `https://verenigd-leiding.pages.dev`;
   - **Redirect URLs**: hetzelfde adres met `/**` erachter.
4. Optioneel: **Authentication › Emails**: de teksten van de uitnodiging en van "wachtwoord vergeten" in het Nederlands zetten.

## 3. Database en Edge Function installeren

Gebruik de Supabase-CLI op uw pc (in de map van de repository):

```powershell
npx supabase login
npx supabase link --project-ref <projectref>     # de projectref staat in de projectinstellingen
npx supabase db push                              # zet supabase/migrations op de server
npx supabase functions deploy leiding             # de Edge Function voor uitnodigen en intrekken
npx supabase secrets set WEBAPP_URL=https://verenigd-leiding.pages.dev
```

De migratie maakt de tabellen voor de ledenkopie (zonder adres en zonder financiële gegevens), de beveiligingsregels per speltak en de functies voor de beheerder.

## 4. Uzelf beheerder maken

1. Maak in **Authentication › Users** een gebruiker aan met het e-mailadres van de beheerder (**Add user › Create new user**, met een sterk wachtwoord).
2. Open **SQL Editor** en voer uit (vul het adres in):

   ```sql
   insert into public.beheerders (user_id)
   select id from auth.users where email = 'beheerder@voorbeeld.nl';
   ```

3. Koppel de authenticator-app: open de webapp (stap 5), log in met dit adres en volg de stappen. Daarna logt de beheerder ook in de desktop-app in met wachtwoord en code.

## 5. Webapp op Cloudflare Pages

1. Maak een account op [cloudflare.com](https://dash.cloudflare.com) en kies **Workers & Pages › Create › Pages › Connect to Git**; kies de repository `verenigd`.
2. Instellingen van de build:
   - **Root directory**: `webapp`;
   - **Build command**: `npm run build`;
   - **Build output directory**: `build`;
   - **Environment variables**:
     - `VITE_SUPABASE_URL` = het projectadres (Supabase › Project Settings › API, bijvoorbeeld `https://abcd.supabase.co`);
     - `VITE_SUPABASE_PUBLISHABLE_KEY` = de **publieke** sleutel (*anon* of *publishable*).
3. Na de eerste build staat de webapp op `https://<naam>.pages.dev`. Vul dat adres in bij stap 2.3 en bij `WEBAPP_URL` (stap 3).
4. Leiding kan de webapp op de telefoon installeren via **Toevoegen aan beginscherm**.

## 6. Verenigd koppelen

1. Open in Verenigd **Webapp › Synchroniseren**.
2. Vul het **projectadres**, de **publieke sleutel** en uw **e-mailadres** in en kies **Project opslaan**.
3. Vul uw **wachtwoord** en de **code** uit de authenticator-app in en kies **Inloggen**.
4. Verenigd synchroniseert direct, en daarna bij elke keer dat u de database opent.

## 7. Leiding uitnodigen

1. Controleer bij **Instellingen › Groepen** welke groepen een speltak zijn.
2. Kies **Webapp › Leiding**, vul de naam en het e-mailadres in, vink de speltak(ken) aan en kies **Uitnodigen**.
3. De leider krijgt een e-mail, kiest een wachtwoord en stelt de authenticator-app in. Daarna ziet hij of zij alleen de leden van de eigen speltak.

## Gratis abonnement: pauze na 7 dagen

Een gratis project pauzeert na 7 dagen zonder gebruik (besluit 228). Synchroniseert Verenigd niet, dan meldt de statusregel ⚠ *Geen verbinding met de webapp*. Zet het project dan weer aan via **supabase.com › uw project › Restore**. Opent u de database minstens eens per week, dan pauzeert het project niet, omdat Verenigd dan synchroniseert.

## Wat er waar staat

| Gegevens | Waar |
|---|---|
| Leden, voogden en functies van de speltakken (naam, geboortedatum, telefoon, e-mail) | Supabase (kopie) en de versleutelde database (leidend) |
| Adres, IBAN, betaler, machtiging, contributie, boekhouding | **Alleen** de versleutelde database op de pc |
| Voorstellen van de leiding | Supabase, tot de beheerder ze afhandelt |
| Sessie van de beheerder (vernieuwingssleutel) | De versleutelde database |
| `service_role`-sleutel | Alleen in de Edge Function op Supabase |
