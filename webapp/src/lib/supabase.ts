import { createClient } from '@supabase/supabase-js';

// Alleen de publieke sleutel: alle afscherming zit in de beveiligingsregels van de database (besluit 33).
const url = import.meta.env.VITE_SUPABASE_URL ?? 'http://localhost:54321';
const sleutel = import.meta.env.VITE_SUPABASE_PUBLISHABLE_KEY ?? 'ontbreekt';

export const supabase = createClient(url, sleutel, {
  auth: { persistSession: true, autoRefreshToken: true, detectSessionInUrl: true },
});

export type Toegang = 'uit' | 'wachtwoord' | 'mfa-instellen' | 'mfa' | 'klaar';

/** Waar staat de gebruiker in het inloggen? Pas bij 'klaar' (aal2) geeft de database gegevens (besluit 231). */
export async function toegang(): Promise<Toegang> {
  try {
    const { data } = await supabase.auth.getSession();
    if (!data.session) return 'uit';
    const { data: niveau } = await supabase.auth.mfa.getAuthenticatorAssuranceLevel();
    if (niveau?.currentLevel === 'aal2') return 'klaar';
    return niveau?.nextLevel === 'aal2' ? 'mfa' : 'mfa-instellen';
  } catch {
    // Een beschadigde of verlopen sessie: opnieuw inloggen.
    await supabase.auth.signOut({ scope: 'local' }).catch(() => undefined);
    return 'uit';
  }
}

export function routeVoor(stap: Toegang): string {
  return { uit: '/inloggen', wachtwoord: '/wachtwoord', 'mfa-instellen': '/mfa/instellen', mfa: '/mfa', klaar: '/' }[stap];
}
