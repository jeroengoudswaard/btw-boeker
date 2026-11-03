// Wijzigingen van de leiding zijn voorstellen per veld (besluit 34).
// Dezelfde velden staan in de controle op public.voorstellen.veld en in Leidingveld (desktop-app).
import type { Lid } from './leden';
import { normaliseerTelefoon } from './telefoon';

export const VELDEN = [
  'email', 'telefoon',
  'voogd1_naam', 'voogd1_email', 'voogd1_telefoon', 'voogd1_mobiel',
  'voogd2_naam', 'voogd2_email', 'voogd2_telefoon', 'voogd2_mobiel',
] as const;

export type Veld = (typeof VELDEN)[number];

export interface NieuwVoorstel {
  lid_id: string;
  veld: Veld;
  oud: string | null;
  nieuw: string | null;
}

export function veldOmschrijving(veld: string): string {
  if (veld === 'email') return 'E-mail';
  if (veld === 'telefoon') return 'Telefoon';
  const m = /^voogd([12])_(naam|email|telefoon|mobiel)$/.exec(veld);
  if (!m) return veld;
  const deel = { naam: 'naam', email: 'e-mail', telefoon: 'telefoon', mobiel: 'mobiel' }[m[2] as 'naam'];
  return `Voogd ${m[1]} · ${deel}`;
}

export function huidigeWaarde(lid: Lid, veld: Veld): string | null {
  if (veld === 'email') return lid.email;
  if (veld === 'telefoon') return lid.telefoon;
  const [, n, deel] = /^voogd([12])_(\w+)$/.exec(veld)!;
  const voogd = lid.voogden.find((v) => v.volgnummer === Number(n));
  return voogd ? (voogd[deel as 'naam' | 'email' | 'telefoon' | 'mobiel'] ?? null) : null;
}

const isTelefoon = (veld: Veld) => veld === 'telefoon' || veld.endsWith('_telefoon') || veld.endsWith('_mobiel');

/** Maakt van het formulier de voorstellen: alleen velden die echt anders zijn. */
export function voorstellenUit(lid: Lid, formulier: Partial<Record<Veld, string>>): NieuwVoorstel[] {
  const uit: NieuwVoorstel[] = [];
  for (const veld of VELDEN) {
    if (!(veld in formulier)) continue;
    const ruw = formulier[veld]?.trim() ?? '';
    const nieuw = ruw === '' ? null : isTelefoon(veld) ? normaliseerTelefoon(ruw).waarde : ruw;
    const oud = huidigeWaarde(lid, veld);
    if ((oud ?? null) !== nieuw) uit.push({ lid_id: lid.id, veld, oud: oud ?? null, nieuw });
  }
  return uit;
}
