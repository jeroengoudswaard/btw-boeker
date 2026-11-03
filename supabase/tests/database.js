// Hulp voor de beveiligingstests: een verse database met de stub en alle migraties,
// en "als(gebruiker)" om een opdracht uit te voeren zoals Supabase dat doet (rol authenticated + JWT-claims).
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import pg from 'pg';

const hier = dirname(fileURLToPath(import.meta.url));
const migraties = join(hier, '..', 'migrations');

export async function nieuweDatabase(naam) {
  const url = process.env.PG_URL;
  if (!url) throw new Error('PG_URL ontbreekt: start de tests met supabase/tests/lokaal.sh of zet PG_URL.');
  const beheer = new pg.Client({ connectionString: url });
  await beheer.connect();
  await beheer.query(`drop database if exists ${naam} with (force)`);
  await beheer.query(`create database ${naam}`);
  await beheer.end();

  const doel = new URL(url);
  doel.pathname = '/' + naam;
  const db = new pg.Client({ connectionString: doel.toString() });
  await db.connect();
  // De rollen bestaan per cluster: alleen aanmaken als ze er nog niet zijn.
  const stub = readFileSync(join(hier, 'supabase-stub.sql'), 'utf8').replace(
    /create role (\w+) nologin( bypassrls)?;/g,
    (_, rol, extra) => `do $$ begin create role ${rol} nologin${extra ?? ''}; exception when duplicate_object then null; end $$;`,
  );
  await db.query(stub);
  for (const bestand of readdirSync(migraties).filter((b) => b.endsWith('.sql')).sort()) {
    await db.query(readFileSync(join(migraties, bestand), 'utf8'));
  }
  return db;
}

/** Voert fn uit als ingelogde gebruiker; aal 'aal2' = met MFA. Alles wordt teruggedraaid. */
export async function als(db, gebruiker, fn, { aal = 'aal2' } = {}) {
  await db.query('begin');
  try {
    if (gebruiker) {
      await db.query(`select set_config('request.jwt.claims', $1, true)`, [JSON.stringify({ sub: gebruiker, aal, role: 'authenticated' })]);
      await db.query('set local role authenticated');
    } else {
      await db.query('set local role anon');
    }
    return await fn();
  } finally {
    await db.query('rollback');
  }
}

/** Zoals als(), maar de wijzigingen blijven staan (voor opbouw van testgegevens via de beheerdersfuncties). */
export async function alsBlijvend(db, gebruiker, fn) {
  await db.query('begin');
  try {
    await db.query(`select set_config('request.jwt.claims', $1, true)`, [JSON.stringify({ sub: gebruiker, aal: 'aal2', role: 'authenticated' })]);
    await db.query('set local role authenticated');
    const uitkomst = await fn();
    await db.query('commit');
    return uitkomst;
  } catch (fout) {
    await db.query('rollback');
    throw fout;
  }
}
