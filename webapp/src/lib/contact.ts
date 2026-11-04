// Besluit 249: bij Bellen, Bericht en E-mail kiest de leiding uit alle mogelijkheden: voogd 1, voogd 2 en het lid zelf.
import { volledigeNaam, type Lid } from './leden.ts';
import { weergaveTelefoon } from './telefoon.ts';

export type Actie = 'bellen' | 'bericht' | 'email';

export interface Contactoptie {
  /** De naam van de persoon (of "Voogd 2" als de naam onbekend is). */
  wie: string;
  /** "Voogd 1", "Voogd 2" of "Lid zelf". */
  rol: string;
  /** "mobiel", "vast", "telefoon" (alleen één nummer bekend) of "e-mail". */
  soort: string;
  /** Zoals getoond: 06 12345678 of het e-mailadres. */
  weergave: string;
  href: string;
}

/** Nederlandse (+316) en Belgische (+324) mobiele nummers; alleen daar kan een bericht naartoe. */
const isMobiel = (nummer: string) => nummer.startsWith('+316') || nummer.startsWith('+324');

interface Persoon {
  wie: string;
  rol: string;
  email: string | null;
  mobiel: string | null;
  vast: string | null;
}

function nummers(p: Persoon): { soort: string; nummer: string }[] {
  if (p.mobiel && p.vast && p.mobiel !== p.vast) {
    return [{ soort: 'mobiel', nummer: p.mobiel }, { soort: 'vast', nummer: p.vast }];
  }

  const enige = p.mobiel ?? p.vast;
  return enige ? [{ soort: p.mobiel && !p.vast ? 'mobiel' : 'telefoon', nummer: enige }] : [];
}

export function contactopties(lid: Lid, actie: Actie): Contactoptie[] {
  const personen: Persoon[] = [
    ...[...lid.voogden].sort((a, b) => a.volgnummer - b.volgnummer).map((v) => ({
      wie: v.naam ?? `Voogd ${v.volgnummer}`, rol: `Voogd ${v.volgnummer}`, email: v.email, mobiel: v.mobiel, vast: v.telefoon,
    })),
    { wie: volledigeNaam(lid), rol: 'Lid zelf', email: lid.email, mobiel: null, vast: lid.telefoon },
  ];

  return personen.flatMap((p): Contactoptie[] => {
    if (actie === 'email') {
      return p.email ? [{ wie: p.wie, rol: p.rol, soort: 'e-mail', weergave: p.email, href: `mailto:${p.email}` }] : [];
    }

    return nummers(p)
      .filter((n) => actie === 'bellen' || isMobiel(n.nummer))
      .map((n) => ({
        wie: p.wie, rol: p.rol, soort: n.soort, weergave: weergaveTelefoon(n.nummer),
        href: `${actie === 'bellen' ? 'tel' : 'sms'}:${n.nummer}`,
      }));
  });
}
