// Gedeelde stand van de app: één keer laden per sessie, opnieuw na een voorstel.
import { goto } from '$app/navigation';
import { haalLeden, haalVoorstellen, mijnSpeltakken, type Voorstel } from './api';
import type { Lid } from './leden';
import { pad } from './pad.ts';
import { routeVoor, toegang } from './supabase';

export const stand = $state({
  leden: [] as Lid[],
  speltakken: [] as string[],
  speltak: '',
  voorstellen: [] as Voorstel[],
  geladen: false,
  fout: null as string | null,
});

/** Stuurt naar de juiste inlogstap als de gebruiker (nog) geen MFA-sessie heeft. */
export async function bewaak(): Promise<boolean> {
  const stap = await toegang();
  if (stap !== 'klaar') {
    await goto(pad(routeVoor(stap)), { replaceState: true });
    return false;
  }
  return true;
}

export async function laad(opnieuw = false): Promise<void> {
  if (stand.geladen && !opnieuw) return;
  stand.fout = null;
  try {
    const [leden, speltakken, voorstellen] = await Promise.all([haalLeden(), mijnSpeltakken(), haalVoorstellen()]);
    stand.leden = leden;
    stand.speltakken = speltakken;
    stand.voorstellen = voorstellen;
    if (!speltakken.includes(stand.speltak)) stand.speltak = speltakken[0] ?? '';
    stand.geladen = true;
  } catch (e) {
    stand.fout = e instanceof Error ? e.message : 'Laden mislukt.';
  }
}

export function openVoorstellen(lidId?: string): Voorstel[] {
  return stand.voorstellen.filter((v) => v.status === 'open' && (!lidId || v.lid_id === lidId));
}
