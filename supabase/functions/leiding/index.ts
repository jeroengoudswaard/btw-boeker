// Edge Function "leiding" (Deno). De logica staat in logica.ts; hier alleen de koppeling met Supabase.
// Omgeving (door Supabase gezet): SUPABASE_URL, SUPABASE_ANON_KEY, SUPABASE_SERVICE_ROLE_KEY.
// Optioneel: WEBAPP_URL, het adres van de webapp voor de link in de uitnodiging.
import { createClient } from 'npm:@supabase/supabase-js@2.45.4';
import { verwerk, type Diensten } from './logica.ts';

const url = Deno.env.get('SUPABASE_URL')!;
const anon = Deno.env.get('SUPABASE_ANON_KEY')!;
const service = Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!;
// Na de uitnodiging kiest de leider eerst een wachtwoord (pagina /wachtwoord van de webapp).
const webapp = Deno.env.get('WEBAPP_URL')?.replace(/\/$/, '');
const naUitnodiging = webapp ? `${webapp}/wachtwoord` : undefined;

const beheer = createClient(url, service, { auth: { persistSession: false, autoRefreshToken: false } });
const alsGebruiker = (token: string) =>
  createClient(url, anon, { global: { headers: { Authorization: `Bearer ${token}` } }, auth: { persistSession: false } });

function aal(token: string): string {
  try {
    const deel = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(deel)).aal ?? '';
  } catch {
    return '';
  }
}

const diensten: Diensten = {
  async gebruiker(token) {
    // getUser controleert het token bij Supabase Auth; pas daarna lezen we de aal-claim.
    const { data, error } = await beheer.auth.getUser(token);
    return error || !data.user ? null : { id: data.user.id, aal: aal(token) };
  },
  async isBeheerder(token) {
    const { data, error } = await alsGebruiker(token).from('beheerders').select('user_id').limit(1);
    return !error && (data?.length ?? 0) > 0;
  },
  async nodigUit(email) {
    const { data, error } = await beheer.auth.admin.inviteUserByEmail(email, { redirectTo: naUitnodiging });
    if (error || !data.user) throw error ?? new Error('Geen gebruiker');
    return data.user.id;
  },
  async voegLeidingToe(id, naam, email) {
    const { error } = await beheer.from('leiding').upsert({ user_id: id, naam, email });
    if (error) throw error;
  },
  async zetSpeltakken(token, id, speltakken) {
    const { error } = await alsGebruiker(token).rpc('zet_leiding_speltakken', { gebruiker: id, speltakken });
    if (error) throw error;
  },
  async verwijder(id) {
    const { error } = await beheer.auth.admin.deleteUser(id);
    if (error) throw error;
  },
};

Deno.serve(async (req) => {
  const token = req.headers.get('Authorization')?.replace(/^Bearer\s+/i, '') ?? null;
  let body: unknown = null;
  try {
    body = req.method === 'POST' ? await req.json() : null;
  } catch {
    body = null;
  }
  const antwoord = await verwerk({ methode: req.method, token, body }, diensten);
  return new Response(JSON.stringify(antwoord.body), {
    status: antwoord.status,
    headers: { 'Content-Type': 'application/json' },
  });
});
