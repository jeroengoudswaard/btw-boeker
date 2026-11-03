// Leden zoals de server ze levert (de kopie van de desktop-app, besluiten 38 en 229).

export interface Voogd {
  volgnummer: number;
  naam: string | null;
  email: string | null;
  telefoon: string | null;
  mobiel: string | null;
}

export interface Functie {
  speltak: string;
  rol: string;
  is_leiding: boolean;
}

export interface Lid {
  id: string;
  lidnummer: string;
  voornaam: string;
  tussenvoegsel: string | null;
  achternaam: string;
  geboortedatum: string | null;
  email: string | null;
  telefoon: string | null;
  voogden: Voogd[];
  functies: Functie[];
}

export interface Ledengroep {
  titel: string;
  leden: Lid[];
}

const zonderAccent = (t: string) => t.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

export function volledigeNaam(lid: Pick<Lid, 'voornaam' | 'tussenvoegsel' | 'achternaam'>): string {
  return [lid.voornaam, lid.tussenvoegsel, lid.achternaam].filter((d) => d && d.trim()).join(' ');
}

export function initialen(lid: Pick<Lid, 'voornaam' | 'achternaam'>): string {
  return ((lid.voornaam[0] ?? '') + (lid.achternaam[0] ?? '')).toUpperCase();
}

/** Vaste pastelkleur per lid (zelfde ID = zelfde kleur); paren met voldoende contrast. */
export const KLEUREN = [
  { vlak: '#DCEEDD', inkt: '#1F5E23' },
  { vlak: '#DCE8F7', inkt: '#1B4A80' },
  { vlak: '#F1E0F5', inkt: '#6A2380' },
  { vlak: '#FBE4D5', inkt: '#8A3A0B' },
  { vlak: '#EAF0DA', inkt: '#4A5A1B' },
] as const;

export function kleurVoor(id: string): (typeof KLEUREN)[number] {
  let h = 0;
  for (const c of id) h = (h * 31 + c.charCodeAt(0)) >>> 0;
  return KLEUREN[h % KLEUREN.length];
}

export function leeftijd(geboortedatum: string | null, vandaag = new Date()): number | null {
  if (!geboortedatum) return null;
  const [j, m, d] = geboortedatum.split('-').map(Number);
  let jaren = vandaag.getFullYear() - j;
  if (vandaag.getMonth() + 1 < m || (vandaag.getMonth() + 1 === m && vandaag.getDate() < d)) jaren--;
  return jaren;
}

export function isLeiding(lid: Lid, speltak: string): boolean {
  return lid.functies.some((f) => f.speltak === speltak && f.is_leiding);
}

/** Leiding bovenaan, daarna jeugdleden per beginletter van de voornaam (besluit 222). */
export function groepeer(leden: Lid[], speltak: string, zoek = ''): Ledengroep[] {
  const term = zonderAccent(zoek.trim());
  const passend = leden
    .filter((l) => l.functies.some((f) => f.speltak === speltak))
    .filter((l) => !term || zonderAccent(volledigeNaam(l)).includes(term))
    .sort((a, b) => zonderAccent(volledigeNaam(a)).localeCompare(zonderAccent(volledigeNaam(b)), 'nl'));

  const groepen: Ledengroep[] = [];
  const leiding = passend.filter((l) => isLeiding(l, speltak));
  if (leiding.length) groepen.push({ titel: 'Leiding', leden: leiding });
  for (const l of passend.filter((l) => !isLeiding(l, speltak))) {
    const letter = zonderAccent(l.voornaam[0] ?? '#').toUpperCase();
    const laatste = groepen.at(-1);
    if (laatste && laatste.titel === letter) laatste.leden.push(l);
    else groepen.push({ titel: letter, leden: [l] });
  }
  return groepen;
}
