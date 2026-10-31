// Tests van de Edge Function "leiding": uitnodigen en intrekken mag alleen de beheerder met MFA (besluiten 37 en 230).
import { describe, expect, it, vi } from 'vitest';
import { verwerk, type Diensten } from '../functions/leiding/logica.ts';

const beheerder = { id: 'b1', aal: 'aal2' };

function diensten(over: Partial<Diensten> = {}): Diensten {
  return {
    gebruiker: vi.fn(async () => beheerder),
    isBeheerder: vi.fn(async () => true),
    nodigUit: vi.fn(async () => 'nieuw-id'),
    voegLeidingToe: vi.fn(async () => {}),
    zetSpeltakken: vi.fn(async () => {}),
    verwijder: vi.fn(async () => {}),
    ...over,
  };
}

const uitnodiging = { actie: 'uitnodigen', email: 'akela@example.nl', naam: 'Akela', speltakken: ['Welpen'] };

describe('toegang', () => {
  it('zonder token: 401', async () => {
    const d = diensten();
    expect((await verwerk({ methode: 'POST', token: null, body: uitnodiging }, d)).status).toBe(401);
    expect(d.nodigUit).not.toHaveBeenCalled();
  });

  it('ongeldig token: 401', async () => {
    const d = diensten({ gebruiker: vi.fn(async () => null) });
    expect((await verwerk({ methode: 'POST', token: 'x', body: uitnodiging }, d)).status).toBe(401);
  });

  it('zonder MFA: 403', async () => {
    const d = diensten({ gebruiker: vi.fn(async () => ({ id: 'b1', aal: 'aal1' })) });
    expect((await verwerk({ methode: 'POST', token: 't', body: uitnodiging }, d)).status).toBe(403);
    expect(d.nodigUit).not.toHaveBeenCalled();
  });

  it('leiding (geen beheerder): 403', async () => {
    const d = diensten({ isBeheerder: vi.fn(async () => false) });
    expect((await verwerk({ methode: 'POST', token: 't', body: uitnodiging }, d)).status).toBe(403);
    expect(d.nodigUit).not.toHaveBeenCalled();
  });

  it('alleen POST', async () => {
    expect((await verwerk({ methode: 'GET', token: 't', body: null }, diensten())).status).toBe(405);
  });
});

describe('uitnodigen', () => {
  it('nodigt uit, legt de leider vast en zet de speltakken', async () => {
    const d = diensten();
    const r = await verwerk({ methode: 'POST', token: 't', body: uitnodiging }, d);
    expect(r).toEqual({ status: 200, body: { id: 'nieuw-id' } });
    expect(d.nodigUit).toHaveBeenCalledWith('akela@example.nl');
    expect(d.voegLeidingToe).toHaveBeenCalledWith('nieuw-id', 'Akela', 'akela@example.nl');
    expect(d.zetSpeltakken).toHaveBeenCalledWith('t', 'nieuw-id', ['Welpen']);
  });

  it.each([
    ['ongeldig e-mailadres', { ...uitnodiging, email: 'geen-adres' }],
    ['lege naam', { ...uitnodiging, naam: ' ' }],
    ['te lange naam', { ...uitnodiging, naam: 'x'.repeat(201) }],
    ['speltakken geen lijst', { ...uitnodiging, speltakken: 'Welpen' }],
    ['onbekende actie', { actie: 'wissen' }],
  ])('%s: 400', async (_, body) => {
    const d = diensten();
    expect((await verwerk({ methode: 'POST', token: 't', body }, d)).status).toBe(400);
    expect(d.nodigUit).not.toHaveBeenCalled();
  });

  it('een fout van Supabase geeft 502 zonder details van de server', async () => {
    const d = diensten({ nodigUit: vi.fn(async () => { throw new Error('geheim detail'); }) });
    const r = await verwerk({ methode: 'POST', token: 't', body: uitnodiging }, d);
    expect(r.status).toBe(502);
    expect(JSON.stringify(r.body)).not.toContain('geheim');
  });
});

describe('intrekken', () => {
  it('verwijdert het account van de leider', async () => {
    const d = diensten();
    const r = await verwerk({ methode: 'POST', token: 't', body: { actie: 'intrekken', id: '00000000-0000-4000-8000-000000000001' } }, d);
    expect(r.status).toBe(200);
    expect(d.verwijder).toHaveBeenCalledWith('00000000-0000-4000-8000-000000000001');
  });

  it('de beheerder kan zichzelf niet intrekken', async () => {
    const d = diensten({ gebruiker: vi.fn(async () => ({ id: '00000000-0000-4000-8000-000000000001', aal: 'aal2' })) });
    const r = await verwerk({ methode: 'POST', token: 't', body: { actie: 'intrekken', id: '00000000-0000-4000-8000-000000000001' } }, d);
    expect(r.status).toBe(400);
    expect(d.verwijder).not.toHaveBeenCalled();
  });

  it('ongeldige ID: 400', async () => {
    const d = diensten();
    expect((await verwerk({ methode: 'POST', token: 't', body: { actie: 'intrekken', id: 'x' } }, d)).status).toBe(400);
  });
});
