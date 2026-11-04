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

/**
 * Besluiten 239 en 243: hetzelfde palet als de desktop-app (Onderdeelkleuren). De tint is de kleur voor 16 % dekkend,
 * zodat hij in licht en donker klopt; elke kleur haalt minstens 3 : 1 op de tint en de grond van beide thema's.
 */
export const PALET = [
  { naam: 'Koraal', inkt: '#CF5A33' },
  { naam: 'Amber', inkt: '#AE7209' },
  { naam: 'Indigo', inkt: '#6A77D7' },
  { naam: 'Violet', inkt: '#9E63D2' },
  { naam: 'Blauw', inkt: '#2786B9' },
  { naam: 'Groenblauw', inkt: '#148D7F' },
  { naam: 'Grijs', inkt: '#7F7F78' },
] as const;

export type Paletkleur = (typeof PALET)[number];

/** De tint achter een pictogram of initialen: de kleur voor 16 % dekkend. */
export function tint(kleur: Paletkleur): string {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(kleur.inkt.slice(i, i + 2), 16));
  return `rgba(${r}, ${g}, ${b}, 0.16)`;
}

/** Vaste kleur per groep, gelijk aan Onderdeelkleuren.VoorGroep in de desktop-app; zonder groep grijs. */
export function kleurVoorGroep(groep: string | null | undefined): Paletkleur {
  if (!groep?.trim()) return PALET[6];
  let h = 0;
  for (let i = 0; i < groep.length; i++) h = (Math.imul(h, 31) + groep.charCodeAt(i)) >>> 0;
  return PALET[h % 6];
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
