import { describe, expect, it } from 'vitest';
import { normaliseerTelefoon, weergaveTelefoon } from './telefoon';
import { groepeer, initialen, kleurVoor, volledigeNaam, leeftijd, type Lid } from './leden';
import { voorstellenUit, veldOmschrijving, VELDEN } from './voorstellen';

// Dezelfde gevallen als TelefoonnummerTests in de desktop-app (besluit 32).
describe('telefoon', () => {
  it.each([
    ['0612345678', '+31612345678'],
    ['06-12345678', '+31612345678'],
    ['06 12 34 56 78', '+31612345678'],
    ['+31 6 12345678', '+31612345678'],
    ['+31 (0)6 12345678', '+31612345678'],
    ['0031612345678', '+31612345678'],
    ['612345678', '+31612345678'],
    ['070-1234567', '+31701234567'],
    ['+32 470 12 34 56', '+32470123456'],
    ['0032 2 123 45 67', '+3221234567'],
    ['+49 30 1234567', '+49301234567'],
  ])('normaliseert %s', (invoer, verwacht) => {
    expect(normaliseerTelefoon(invoer)).toEqual({ waarde: verwacht, herkend: true });
  });

  it.each(['12345', '06-1234', 'onbekend', '0612345678 of 0701234567'])('herkent %s niet en laat het staan', (invoer) => {
    expect(normaliseerTelefoon(invoer)).toEqual({ waarde: invoer, herkend: false });
  });

  it.each([
    ['+31612345678', '06 12345678'],
    ['+31701234567', '0701234567'],
    ['+32470123456', '+32 470123456'],
    ['12345', '12345'],
  ])('toont %s als %s', (opgeslagen, verwacht) => {
    expect(weergaveTelefoon(opgeslagen)).toBe(verwacht);
  });
});

const lid = (voornaam: string, achternaam: string, isLeiding = false, extra: Partial<Lid> = {}): Lid => ({
  id: voornaam,
  lidnummer: '1',
  voornaam,
  tussenvoegsel: null,
  achternaam,
  geboortedatum: '2016-05-01',
  email: null,
  telefoon: null,
  voogden: [],
  functies: [{ speltak: 'Welpen', rol: isLeiding ? 'Leiding' : 'Jeugdlid', is_leiding: isLeiding }],
  ...extra,
});

describe('leden', () => {
  it('groepeert leiding apart en jeugdleden op beginletter van de voornaam', () => {
    const groepen = groepeer([lid('Cas', 'Visser'), lid('Akela', 'Test', true), lid('Anouk', 'Bakker'), lid('bram', 'Jong')], 'Welpen');
    expect(groepen.map((g) => [g.titel, g.leden.map((l) => l.voornaam)])).toEqual([
      ['Leiding', ['Akela']],
      ['A', ['Anouk']],
      ['B', ['bram']],
      ['C', ['Cas']],
    ]);
  });

  it('zoekt op naam, zonder hoofdletters en accenten', () => {
    const groepen = groepeer([lid('Anouk', 'Bakker'), lid('Zoë', 'de Wit')], 'Welpen', 'zoe');
    expect(groepen.flatMap((g) => g.leden.map((l) => l.voornaam))).toEqual(['Zoë']);
  });

  it('naam, initialen, vaste kleur en leeftijd', () => {
    const l = lid('Bram', 'Jong', false, { tussenvoegsel: 'de' });
    expect(volledigeNaam(l)).toBe('Bram de Jong');
    expect(initialen(l)).toBe('BJ');
    expect(kleurVoor(l.id)).toBe(kleurVoor('Bram'));
    expect(leeftijd('2016-05-01', new Date('2026-10-31'))).toBe(10);
    expect(leeftijd('2016-11-01', new Date('2026-10-31'))).toBe(9);
    expect(leeftijd(null, new Date('2026-10-31'))).toBeNull();
  });
});

describe('voorstellen (besluit 34)', () => {
  const basis = lid('Bram', 'Jong', false, {
    email: 'bram@example.org',
    telefoon: '+31612345678',
    voogden: [{ volgnummer: 1, naam: 'Petra', email: 'p@example.org', telefoon: null, mobiel: '+31611111111' }],
  });

  it('alleen gewijzigde velden worden een voorstel, telefoon genormaliseerd vergeleken', () => {
    const voorstellen = voorstellenUit(basis, {
      email: 'bram@example.org',
      telefoon: '06 12345678', // zelfde nummer, andere notatie: geen voorstel
      voogd1_mobiel: '06 87654321',
      voogd2_email: 'twee@example.org',
    });
    expect(voorstellen).toEqual([
      { lid_id: 'Bram', veld: 'voogd1_mobiel', oud: '+31611111111', nieuw: '+31687654321' },
      { lid_id: 'Bram', veld: 'voogd2_email', oud: null, nieuw: 'twee@example.org' },
    ]);
  });

  it('leegmaken is ook een voorstel', () => {
    expect(voorstellenUit(basis, { email: '  ' })).toEqual([{ lid_id: 'Bram', veld: 'email', oud: 'bram@example.org', nieuw: null }]);
  });

  it('de velden zijn dezelfde als op de server en in de desktop-app', () => {
    expect(VELDEN).toEqual([
      'email', 'telefoon',
      'voogd1_naam', 'voogd1_email', 'voogd1_telefoon', 'voogd1_mobiel',
      'voogd2_naam', 'voogd2_email', 'voogd2_telefoon', 'voogd2_mobiel',
    ]);
    expect(veldOmschrijving('voogd2_mobiel')).toBe('Voogd 2 · mobiel');
  });
});
