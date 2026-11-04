import { describe, expect, it } from 'vitest';
import { contactopties } from './contact.ts';
import type { Lid } from './leden.ts';

// Besluit 249: bij Bellen, Bericht en E-mail alle mogelijkheden (voogd 1, voogd 2, het lid zelf; mobiel en vast apart).
const lid = (overrides: Partial<Lid> = {}): Lid => ({
  id: 'l1', lidnummer: '1', voornaam: 'Bram', tussenvoegsel: 'de', achternaam: 'Jong', geboortedatum: null,
  email: 'bram@example.org', telefoon: '+31633333333', functies: [],
  voogden: [
    { volgnummer: 1, naam: 'Petra Jong', email: 'p@example.org', telefoon: '+31201234567', mobiel: '+31611111111' },
    { volgnummer: 2, naam: null, email: null, telefoon: '+31622222222', mobiel: null },
  ],
  ...overrides,
});

describe('contactopties', () => {
  it('bellen: voogd 1 mobiel en vast, voogd 2, dan het lid zelf', () => {
    const opties = contactopties(lid(), 'bellen');
    expect(opties.map((o) => [o.wie, o.rol, o.soort, o.href])).toEqual([
      ['Petra Jong', 'Voogd 1', 'mobiel', 'tel:+31611111111'],
      ['Petra Jong', 'Voogd 1', 'vast', 'tel:+31201234567'],
      ['Voogd 2', 'Voogd 2', 'telefoon', 'tel:+31622222222'],
      ['Bram de Jong', 'Lid zelf', 'telefoon', 'tel:+31633333333'],
    ]);
  });

  it('bericht: alleen mobiele nummers', () => {
    expect(contactopties(lid(), 'bericht').map((o) => o.href)).toEqual(['sms:+31611111111', 'sms:+31622222222', 'sms:+31633333333']);
  });

  it('e-mail: elk adres, met het lid zelf', () => {
    expect(contactopties(lid(), 'email').map((o) => o.href)).toEqual(['mailto:p@example.org', 'mailto:bram@example.org']);
  });

  it('hetzelfde nummer als mobiel en vast is één regel', () => {
    const l = lid({ voogden: [{ volgnummer: 1, naam: 'Petra', email: null, telefoon: '+31611111111', mobiel: '+31611111111' }], telefoon: null });
    expect(contactopties(l, 'bellen')).toHaveLength(1);
  });

  it('zonder gegevens geen opties', () => {
    expect(contactopties(lid({ voogden: [], email: null, telefoon: null }), 'email')).toEqual([]);
  });
});
