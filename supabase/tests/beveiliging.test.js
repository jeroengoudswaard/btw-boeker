// Beveiligingstests van de webapp-database (besluiten 33-35, 229-231).
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { als, alsBlijvend, nieuweDatabase } from './database.js';

const BEHEERDER = '00000000-0000-4000-8000-0000000000b1';
const WELPEN = '00000000-0000-4000-8000-0000000000a1';
const SCOUTS = '00000000-0000-4000-8000-0000000000a2';
const VREEMDE = '00000000-0000-4000-8000-0000000000a9';
const LID_WELP = '10000000-0000-4000-8000-000000000001';
const LID_SCOUT = '10000000-0000-4000-8000-000000000002';
const LID_WEG = '10000000-0000-4000-8000-000000000003';

const lid = (id, voornaam, speltak, extra = {}) => ({
  id,
  lidnummer: 'L-' + id.slice(-3),
  voornaam,
  tussenvoegsel: null,
  achternaam: 'Test',
  geboortedatum: '2016-05-01',
  email: voornaam.toLowerCase() + '@example.nl',
  telefoon: '+31612345678',
  laatst_gewijzigd: '2026-10-31T10:00:00Z',
  voogden: [{ volgnummer: 1, naam: 'Voogd ' + voornaam, email: 'voogd@example.nl', telefoon: null, mobiel: '+31612345678' }],
  functies: [{ speltak, rol: 'Jeugdlid', is_leiding: false }],
  ...extra,
});

const kopie = (leden) => ({ speltakken: ['Welpen', 'Scouts'], leden });

let db;

async function synchroniseer(gegevens) {
  return alsBlijvend(db, BEHEERDER, () => db.query('select public.synchroniseer_leden($1) as r', [JSON.stringify(gegevens)]));
}

async function fout(belofte) {
  try {
    await belofte;
  } catch (e) {
    return e;
  }
  throw new Error('Verwachtte een fout');
}

beforeAll(async () => {
  db = await nieuweDatabase('verenigd_beveiliging');
  await db.query(`insert into auth.users (id, email) values ($1,'b@example.nl'),($2,'w@example.nl'),($3,'s@example.nl'),($4,'x@example.nl')`, [BEHEERDER, WELPEN, SCOUTS, VREEMDE]);
  await db.query('insert into public.beheerders (user_id) values ($1)', [BEHEERDER]);
  await db.query(`insert into public.leiding (user_id, naam, email) values ($1,'Akela','w@example.nl'),($2,'Hathi','s@example.nl')`, [WELPEN, SCOUTS]);
  await synchroniseer(kopie([lid(LID_WELP, 'Welp', 'Welpen'), lid(LID_SCOUT, 'Scout', 'Scouts'), lid(LID_WEG, 'Weg', 'Welpen')]));
  await alsBlijvend(db, BEHEERDER, async () => {
    await db.query(`select public.zet_leiding_speltakken($1, array['Welpen'])`, [WELPEN]);
    await db.query(`select public.zet_leiding_speltakken($1, array['Scouts'])`, [SCOUTS]);
  });
});

afterAll(async () => {
  await db?.end();
});

describe('zonder inloggen of zonder MFA', () => {
  it('anon heeft geen enkel recht op de leden', async () => {
    const e = await als(db, null, () => fout(db.query('select * from public.leden')));
    expect(e.code).toBe('42501');
  });

  it('anon kan de beheerdersfuncties niet aanroepen', async () => {
    const e = await als(db, null, () => fout(db.query(`select public.synchroniseer_leden('{}')`)));
    expect(e.code).toBe('42501');
  });

  it('leiding zonder MFA ziet niets', async () => {
    const r = await als(db, WELPEN, () => db.query('select * from public.leden'), { aal: 'aal1' });
    expect(r.rowCount).toBe(0);
  });

  it('beheerder zonder MFA ziet niets en kan niet synchroniseren', async () => {
    const r = await als(db, BEHEERDER, () => db.query('select * from public.leden'), { aal: 'aal1' });
    expect(r.rowCount).toBe(0);
    const e = await als(db, BEHEERDER, () => fout(db.query(`select public.synchroniseer_leden('{}')`)), { aal: 'aal1' });
    expect(e.code).toBe('42501');
  });

  it('leiding zonder MFA kan geen voorstel indienen', async () => {
    const e = await als(db, WELPEN, () => fout(db.query(`insert into public.voorstellen (lid_id, veld, nieuw) values ($1,'email','a@example.nl')`, [LID_WELP])), { aal: 'aal1' });
    expect(e.code).toBe('42501');
  });
});

describe('leiding ziet alleen de eigen speltak (besluit 33)', () => {
  it('Welpen-leiding ziet alleen Welpen-leden', async () => {
    const r = await als(db, WELPEN, () => db.query('select voornaam from public.leden order by voornaam'));
    expect(r.rows.map((x) => x.voornaam)).toEqual(['Weg', 'Welp']);
  });

  it('Scouts-leiding ziet het Welpen-lid niet, ook niet op ID', async () => {
    const r = await als(db, SCOUTS, () => db.query('select * from public.leden where id = $1', [LID_WELP]));
    expect(r.rowCount).toBe(0);
  });

  it('voogden, speltakken en functies alleen van de eigen speltak', async () => {
    const r = await als(db, SCOUTS, async () => ({
      voogden: (await db.query('select lid_id from public.voogden')).rows.map((x) => x.lid_id),
      speltakken: (await db.query('select naam from public.speltakken')).rows.map((x) => x.naam),
      functies: (await db.query('select speltak from public.lid_speltakken')).rows.map((x) => x.speltak),
    }));
    expect(r).toEqual({ voogden: [LID_SCOUT], speltakken: ['Scouts'], functies: ['Scouts'] });
  });

  it('een ingelogde gebruiker zonder leidingrol ziet niets', async () => {
    const r = await als(db, VREEMDE, () => db.query('select * from public.leden'));
    expect(r.rowCount).toBe(0);
  });

  it('leiding ziet de beheerderslijst en andere leiding niet', async () => {
    const r = await als(db, WELPEN, async () => ({
      beheerders: (await db.query('select * from public.beheerders')).rowCount,
      leiding: (await db.query('select user_id from public.leiding')).rows.map((x) => x.user_id),
    }));
    expect(r).toEqual({ beheerders: 0, leiding: [WELPEN] });
  });

  it('de beheerder met MFA ziet alles', async () => {
    const r = await als(db, BEHEERDER, () => db.query('select * from public.leden'));
    expect(r.rowCount).toBe(3);
  });
});

describe('leiding wijzigt nooit rechtstreeks (besluit 34)', () => {
  it.each([
    ['update', `update public.leden set email = 'x@example.nl'`],
    ['insert', `insert into public.leden (id, lidnummer, voornaam, achternaam, laatst_gewijzigd) values (gen_random_uuid(), '1', 'a', 'b', now())`],
    ['delete', 'delete from public.leden'],
    ['voogd wijzigen', `update public.voogden set naam = 'x'`],
    ['zichzelf een speltak geven', `insert into public.leiding_speltakken (user_id, speltak) values ('${WELPEN}', 'Scouts')`],
    ['zichzelf beheerder maken', `insert into public.beheerders (user_id) values ('${WELPEN}')`],
  ])('%s geeft geen toegang', async (_, sql) => {
    const e = await als(db, WELPEN, () => fout(db.query(sql)));
    expect(e.code).toBe('42501');
  });

  it.each([
    ['synchroniseer_leden', `select public.synchroniseer_leden('{}')`],
    ['handel_voorstel_af', `select public.handel_voorstel_af(gen_random_uuid(), 'goedgekeurd')`],
    ['zet_leiding_speltakken', `select public.zet_leiding_speltakken('${WELPEN}', array['Scouts'])`],
  ])('leiding kan %s niet aanroepen', async (_, sql) => {
    const e = await als(db, WELPEN, () => fout(db.query(sql)));
    expect(e.code).toBe('42501');
  });
});

describe('voorstellen', () => {
  it('leiding dient een voorstel in voor een lid van de eigen speltak', async () => {
    const r = await als(db, WELPEN, () =>
      db.query(`insert into public.voorstellen (lid_id, veld, oud, nieuw) values ($1,'voogd1_mobiel','+31612345678','+31687654321') returning ingediend_door, status`, [LID_WELP]));
    expect(r.rows[0]).toEqual({ ingediend_door: WELPEN, status: 'open' });
  });

  it('geen voorstel voor een lid van een andere speltak', async () => {
    const e = await als(db, SCOUTS, () => fout(db.query(`insert into public.voorstellen (lid_id, veld, nieuw) values ($1,'email','a@example.nl')`, [LID_WELP])));
    expect(e.code).toBe('42501');
  });

  it.each([
    ['een status meegeven', `insert into public.voorstellen (lid_id, veld, nieuw, status) values ('${LID_WELP}','email','a','goedgekeurd')`],
    ['namens een ander indienen', `insert into public.voorstellen (lid_id, veld, nieuw, ingediend_door) values ('${LID_WELP}','email','a','${SCOUTS}')`],
  ])('leiding kan niet %s', async (_, sql) => {
    const e = await als(db, WELPEN, () => fout(db.query(sql)));
    expect(e.code).toBe('42501');
  });

  it('alleen de toegestane velden', async () => {
    const e = await als(db, WELPEN, () => fout(db.query(`insert into public.voorstellen (lid_id, veld, nieuw) values ($1,'geboortedatum','2010-01-01')`, [LID_WELP])));
    expect(e.code).toBe('23514');
  });

  it('een gebruiker zonder leidingrol kan geen voorstel indienen', async () => {
    const e = await als(db, VREEMDE, () => fout(db.query(`insert into public.voorstellen (lid_id, veld, nieuw) values ($1,'email','a@example.nl')`, [LID_WELP])));
    expect(['42501', '23503']).toContain(e.code);
  });

  it('leiding ziet alleen eigen voorstellen en kan ze niet zelf goedkeuren', async () => {
    await alsBlijvend(db, WELPEN, () => db.query(`insert into public.voorstellen (lid_id, veld, nieuw) values ($1,'email','nieuw@example.nl')`, [LID_WELP]));
    const scouts = await als(db, SCOUTS, () => db.query('select * from public.voorstellen'));
    expect(scouts.rowCount).toBe(0);
    const e = await als(db, WELPEN, () => fout(db.query(`update public.voorstellen set status = 'goedgekeurd'`)));
    expect(e.code).toBe('42501');
  });

  it('de beheerder keurt een voorstel eenmaal goed', async () => {
    const { rows } = await als(db, BEHEERDER, () => db.query(`select id from public.voorstellen where status = 'open' limit 1`));
    const id = rows[0].id;
    await alsBlijvend(db, BEHEERDER, () => db.query(`select public.handel_voorstel_af($1, 'goedgekeurd')`, [id]));
    const status = await als(db, WELPEN, () => db.query('select status, afgehandeld_op is not null as klaar from public.voorstellen where id = $1', [id]));
    expect(status.rows[0]).toEqual({ status: 'goedgekeurd', klaar: true });
    const e = await als(db, BEHEERDER, () => fout(db.query(`select public.handel_voorstel_af($1, 'afgewezen')`, [id])));
    expect(e.code).toBe('P0002');
  });
});

describe('synchroniseren (besluit 38)', () => {
  it('werkt gegevens bij, verwijdert vertrokken leden en houdt de voorstellen van blijvende leden', async () => {
    await alsBlijvend(db, WELPEN, () => db.query(`insert into public.voorstellen (lid_id, veld, nieuw) values ($1,'telefoon','+31600000000')`, [LID_WELP]));
    await alsBlijvend(db, WELPEN, () => db.query(`insert into public.voorstellen (lid_id, veld, nieuw) values ($1,'telefoon','+31600000001')`, [LID_WEG]));

    const r = await synchroniseer(kopie([lid(LID_WELP, 'Welp', 'Welpen', { email: 'gewijzigd@example.nl' }), lid(LID_SCOUT, 'Scout', 'Scouts')]));
    expect(r.rows[0].r).toEqual({ leden: 2, verwijderd: 1 });

    const welp = await als(db, WELPEN, async () => ({
      leden: (await db.query('select email from public.leden')).rows,
      voorstellen: (await db.query(`select lid_id from public.voorstellen where status = 'open'`)).rows.map((x) => x.lid_id),
    }));
    expect(welp.leden).toEqual([{ email: 'gewijzigd@example.nl' }]);
    expect(welp.voorstellen).toEqual([LID_WELP]);
  });

  it('een speltak met leiding blijft bestaan, ook zonder leden', async () => {
    await synchroniseer({ speltakken: ['Scouts'], leden: [lid(LID_SCOUT, 'Scout', 'Scouts')] });
    const r = await als(db, BEHEERDER, () => db.query('select naam from public.speltakken order by naam'));
    expect(r.rows.map((x) => x.naam)).toEqual(['Scouts', 'Welpen']);
  });
});

describe('gegevensminimalisatie (besluiten 35 en 229)', () => {
  it('de server heeft geen kolom voor financiele gegevens of adres', async () => {
    const r = await db.query(`select table_name, column_name from information_schema.columns
      where table_schema = 'public' and column_name ~* '(iban|betaler|machtig|mandaat|contributie|bedrag|straat|huisnummer|postcode|plaats|adres)'`);
    expect(r.rows).toEqual([]);
  });

  it('elke tabel in public heeft Row-Level Security aan', async () => {
    const r = await db.query(`select relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
      where n.nspname = 'public' and c.relkind = 'r' and not c.relrowsecurity`);
    expect(r.rows).toEqual([]);
  });
});
