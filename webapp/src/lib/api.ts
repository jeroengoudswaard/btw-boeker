import { supabase } from './supabase';
import type { Lid } from './leden';
import type { NieuwVoorstel } from './voorstellen';

export interface Voorstel {
  id: string;
  lid_id: string;
  veld: string;
  oud: string | null;
  nieuw: string | null;
  status: 'open' | 'goedgekeurd' | 'afgewezen';
  ingediend_op: string;
  afgehandeld_op: string | null;
  toelichting: string | null;
}

function fout(e: { message: string } | null): never | void {
  if (e) throw new Error('De server gaf een fout. Probeer het later opnieuw.');
}

/** De leden die de beveiligingsregels deze leider laten zien: alleen de eigen speltak(ken). */
export async function haalLeden(): Promise<Lid[]> {
  const { data, error } = await supabase
    .from('leden')
    .select('id,lidnummer,voornaam,tussenvoegsel,achternaam,geboortedatum,email,telefoon,voogden(volgnummer,naam,email,telefoon,mobiel),lid_speltakken(speltak,rol,is_leiding)');
  fout(error);
  return (data ?? []).map(({ lid_speltakken, voogden, ...l }) => ({
    ...l,
    voogden: [...(voogden ?? [])].sort((a, b) => a.volgnummer - b.volgnummer),
    functies: lid_speltakken ?? [],
  })) as Lid[];
}

export async function mijnSpeltakken(): Promise<string[]> {
  const { data, error } = await supabase.from('speltakken').select('naam').order('naam');
  fout(error);
  return (data ?? []).map((s) => s.naam as string);
}

export async function haalVoorstellen(): Promise<Voorstel[]> {
  const { data, error } = await supabase
    .from('voorstellen')
    .select('id,lid_id,veld,oud,nieuw,status,ingediend_op,afgehandeld_op,toelichting')
    .order('ingediend_op', { ascending: false });
  fout(error);
  return (data ?? []) as Voorstel[];
}

/** Alleen de kolommen die de leiding mag zetten; status en indiener zet de server (besluit 34). */
export async function dienIn(voorstellen: NieuwVoorstel[]): Promise<void> {
  if (voorstellen.length === 0) return;
  const { error } = await supabase.from('voorstellen').insert(voorstellen, { defaultToNull: false });
  fout(error);
}
