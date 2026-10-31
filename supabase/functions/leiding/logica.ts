// Logica van de Edge Function "leiding", los van Deno en Supabase zodat hij te testen is.
// Uitnodigen en intrekken van leiding mag alleen de beheerder met MFA (besluiten 37 en 230).
// De service_role-sleutel zit alleen in de Edge Function, nooit op de pc.

export interface Gebruiker {
  id: string;
  aal: string;
}

export interface Diensten {
  /** De gebruiker achter het token (gecontroleerd door Supabase Auth), of null. */
  gebruiker(token: string): Promise<Gebruiker | null>;
  /** Of deze gebruiker beheerder is (gelezen met zijn eigen token, dus via de beveiligingsregels). */
  isBeheerder(token: string): Promise<boolean>;
  /** Stuurt de uitnodiging; geeft de ID van het nieuwe account. */
  nodigUit(email: string): Promise<string>;
  voegLeidingToe(id: string, naam: string, email: string): Promise<void>;
  /** Via public.zet_leiding_speltakken met het token van de beheerder. */
  zetSpeltakken(token: string, id: string, speltakken: string[]): Promise<void>;
  verwijder(id: string): Promise<void>;
}

export interface Verzoek {
  methode: string;
  token: string | null;
  body: unknown;
}

export interface Antwoord {
  status: number;
  body: Record<string, unknown>;
}

const emailPatroon = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const idPatroon = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const fout = (status: number, melding: string): Antwoord => ({ status, body: { fout: melding } });

export async function verwerk(verzoek: Verzoek, d: Diensten): Promise<Antwoord> {
  if (verzoek.methode !== 'POST') return fout(405, 'Alleen POST.');
  if (!verzoek.token) return fout(401, 'Niet ingelogd.');

  const gebruiker = await d.gebruiker(verzoek.token);
  if (!gebruiker) return fout(401, 'Niet ingelogd.');
  if (gebruiker.aal !== 'aal2') return fout(403, 'Log in met de code uit de authenticator-app.');
  if (!(await d.isBeheerder(verzoek.token))) return fout(403, 'Alleen de beheerder.');

  const body = (verzoek.body ?? {}) as Record<string, unknown>;
  try {
    switch (body.actie) {
      case 'uitnodigen': {
        const email = typeof body.email === 'string' ? body.email.trim().toLowerCase() : '';
        const naam = typeof body.naam === 'string' ? body.naam.trim() : '';
        const speltakken = body.speltakken;
        if (!emailPatroon.test(email) || email.length > 320) return fout(400, 'Ongeldig e-mailadres.');
        if (naam.length === 0 || naam.length > 200) return fout(400, 'De naam is verplicht (maximaal 200 tekens).');
        if (!Array.isArray(speltakken) || !speltakken.every((s) => typeof s === 'string')) return fout(400, 'Speltakken ontbreken.');

        const id = await d.nodigUit(email);
        await d.voegLeidingToe(id, naam, email);
        await d.zetSpeltakken(verzoek.token, id, speltakken as string[]);
        return { status: 200, body: { id } };
      }
      case 'intrekken': {
        const id = typeof body.id === 'string' ? body.id : '';
        if (!idPatroon.test(id)) return fout(400, 'Ongeldige ID.');
        if (id === gebruiker.id) return fout(400, 'U kunt uw eigen account niet intrekken.');
        await d.verwijder(id);
        return { status: 200, body: {} };
      }
      default:
        return fout(400, 'Onbekende actie.');
    }
  } catch {
    // Geen details van de server terug (en niets loggen met persoonsgegevens).
    return fout(502, 'Supabase gaf een fout. Probeer het later opnieuw.');
  }
}
