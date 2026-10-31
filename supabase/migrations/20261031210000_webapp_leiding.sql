-- Verenigd: webapp voor de leiding (fase 7, besluiten 33-38 en 222-233).
--
-- De server bevat alleen een KOPIE van ledengegevens; de pc is leidend (besluit 38).
-- Nooit financiele gegevens: geen IBAN, betaler, machtiging of contributie (besluit 35).
-- Geen adres (besluit 229).
--
-- Beveiliging:
--   * elke regel eist MFA (aal2) (besluit 231);
--   * de leiding ziet alleen leden met een functie in de eigen speltak(ken) (besluit 33);
--   * de leiding wijzigt nooit rechtstreeks, maar dient voorstellen in (besluit 34);
--   * de beheerder schrijft alleen via de functies hieronder (security definer met eigen controle).

-- ===== Tabellen =====

create table public.speltakken (
    naam text primary key check (length(naam) between 1 and 200)
);

create table public.leden (
    id uuid primary key,
    lidnummer text not null,
    voornaam text not null,
    tussenvoegsel text,
    achternaam text not null,
    geboortedatum date,
    email text,
    telefoon text,
    laatst_gewijzigd timestamptz not null
);

create table public.lid_speltakken (
    lid_id uuid not null references public.leden (id) on delete cascade,
    speltak text not null references public.speltakken (naam) on update cascade on delete cascade,
    rol text not null,
    is_leiding boolean not null default false,
    primary key (lid_id, speltak, rol)
);

create table public.voogden (
    lid_id uuid not null references public.leden (id) on delete cascade,
    volgnummer smallint not null check (volgnummer in (1, 2)),
    naam text,
    email text,
    telefoon text,
    mobiel text,
    primary key (lid_id, volgnummer)
);

create table public.beheerders (
    user_id uuid primary key references auth.users (id) on delete cascade
);

create table public.leiding (
    user_id uuid primary key references auth.users (id) on delete cascade,
    naam text not null,
    email text not null
);

create table public.leiding_speltakken (
    user_id uuid not null references public.leiding (user_id) on delete cascade,
    speltak text not null references public.speltakken (naam) on update cascade on delete cascade,
    primary key (user_id, speltak)
);

create table public.voorstellen (
    id uuid primary key default gen_random_uuid(),
    lid_id uuid not null references public.leden (id) on delete cascade,
    veld text not null check (veld in (
        'email', 'telefoon',
        'voogd1_naam', 'voogd1_email', 'voogd1_telefoon', 'voogd1_mobiel',
        'voogd2_naam', 'voogd2_email', 'voogd2_telefoon', 'voogd2_mobiel')),
    oud text,
    nieuw text check (nieuw is null or length(nieuw) <= 320),
    ingediend_door uuid not null default auth.uid() references public.leiding (user_id) on delete cascade,
    ingediend_op timestamptz not null default now(),
    status text not null default 'open' check (status in ('open', 'goedgekeurd', 'afgewezen')),
    afgehandeld_op timestamptz,
    toelichting text check (toelichting is null or length(toelichting) <= 500)
);

create index voorstellen_status on public.voorstellen (status);
create index voorstellen_door on public.voorstellen (ingediend_door);
create index lid_speltakken_speltak on public.lid_speltakken (speltak);

-- ===== Hulpfuncties (security definer, zodat de regels elkaar niet recursief aanroepen) =====

create function public.heeft_mfa() returns boolean
language sql stable set search_path = '' as $$
    select coalesce((auth.jwt() ->> 'aal') = 'aal2', false)
$$;

create function public.is_beheerder() returns boolean
language sql stable security definer set search_path = '' as $$
    select exists (select 1 from public.beheerders b where b.user_id = auth.uid())
$$;

create function public.mijn_speltakken() returns setof text
language sql stable security definer set search_path = '' as $$
    select ls.speltak from public.leiding_speltakken ls where ls.user_id = auth.uid()
$$;

create function public.mag_lid_zien(lid uuid) returns boolean
language sql stable security definer set search_path = '' as $$
    select exists (
        select 1 from public.lid_speltakken s
        join public.leiding_speltakken l on l.speltak = s.speltak
        where s.lid_id = lid and l.user_id = auth.uid())
$$;

-- ===== Rechten: niets voor anon; authenticated alleen lezen en voorstellen indienen =====

revoke all on all tables in schema public from anon, authenticated;
grant select on all tables in schema public to authenticated;
grant insert (lid_id, veld, oud, nieuw) on public.voorstellen to authenticated;

revoke all on function public.heeft_mfa(), public.is_beheerder(), public.mijn_speltakken(), public.mag_lid_zien(uuid) from public, anon;
grant execute on function public.heeft_mfa(), public.is_beheerder(), public.mijn_speltakken(), public.mag_lid_zien(uuid) to authenticated;

alter table public.speltakken enable row level security;
alter table public.leden enable row level security;
alter table public.lid_speltakken enable row level security;
alter table public.voogden enable row level security;
alter table public.beheerders enable row level security;
alter table public.leiding enable row level security;
alter table public.leiding_speltakken enable row level security;
alter table public.voorstellen enable row level security;

create policy speltakken_lezen on public.speltakken for select to authenticated
    using (public.heeft_mfa() and (public.is_beheerder() or naam in (select public.mijn_speltakken())));

create policy leden_lezen on public.leden for select to authenticated
    using (public.heeft_mfa() and (public.is_beheerder() or public.mag_lid_zien(id)));

create policy lid_speltakken_lezen on public.lid_speltakken for select to authenticated
    using (public.heeft_mfa() and (public.is_beheerder() or speltak in (select public.mijn_speltakken())));

create policy voogden_lezen on public.voogden for select to authenticated
    using (public.heeft_mfa() and (public.is_beheerder() or public.mag_lid_zien(lid_id)));

create policy beheerders_lezen on public.beheerders for select to authenticated
    using (public.heeft_mfa() and user_id = auth.uid());

create policy leiding_lezen on public.leiding for select to authenticated
    using (public.heeft_mfa() and (public.is_beheerder() or user_id = auth.uid()));

create policy leiding_speltakken_lezen on public.leiding_speltakken for select to authenticated
    using (public.heeft_mfa() and (public.is_beheerder() or user_id = auth.uid()));

create policy voorstellen_lezen on public.voorstellen for select to authenticated
    using (public.heeft_mfa() and (public.is_beheerder() or ingediend_door = auth.uid()));

create policy voorstellen_indienen on public.voorstellen for insert to authenticated
    with check (
        public.heeft_mfa()
        and ingediend_door = auth.uid()
        and status = 'open'
        and afgehandeld_op is null
        and public.mag_lid_zien(lid_id));

-- ===== Functies voor de beheerder (desktop-app) =====

create function public.controleer_beheerder() returns void
language plpgsql stable security definer set search_path = '' as $$
begin
    if not public.heeft_mfa() or not public.is_beheerder() then
        raise exception 'Alleen de beheerder met MFA' using errcode = '42501';
    end if;
end
$$;

-- Zet de ledenkopie op de server (besluiten 38 en 232). Leden die niet meer meekomen
-- verdwijnen (met hun voorstellen); speltakken zonder leden en zonder leiding verdwijnen.
-- gegevens: {"speltakken": ["Welpen", ...], "leden": [{"id", "lidnummer", "voornaam", "tussenvoegsel",
--   "achternaam", "geboortedatum", "email", "telefoon", "laatst_gewijzigd",
--   "voogden": [{"volgnummer", "naam", "email", "telefoon", "mobiel"}],
--   "functies": [{"speltak", "rol", "is_leiding"}]}]}
create function public.synchroniseer_leden(gegevens jsonb) returns jsonb
language plpgsql volatile security definer set search_path = '' as $$
declare
    ids uuid[];
    verwijderd integer;
begin
    perform public.controleer_beheerder();

    insert into public.speltakken (naam)
    select s from jsonb_array_elements_text(coalesce(gegevens -> 'speltakken', '[]')) s
    on conflict do nothing;

    select coalesce(array_agg((l ->> 'id')::uuid), '{}') into ids
    from jsonb_array_elements(coalesce(gegevens -> 'leden', '[]')) l;

    delete from public.leden where id <> all (ids);
    get diagnostics verwijderd = row_count;

    insert into public.leden (id, lidnummer, voornaam, tussenvoegsel, achternaam, geboortedatum, email, telefoon, laatst_gewijzigd)
    select (l ->> 'id')::uuid, l ->> 'lidnummer', l ->> 'voornaam', l ->> 'tussenvoegsel', l ->> 'achternaam',
           (l ->> 'geboortedatum')::date, l ->> 'email', l ->> 'telefoon', (l ->> 'laatst_gewijzigd')::timestamptz
    from jsonb_array_elements(coalesce(gegevens -> 'leden', '[]')) l
    on conflict (id) do update set
        lidnummer = excluded.lidnummer, voornaam = excluded.voornaam, tussenvoegsel = excluded.tussenvoegsel,
        achternaam = excluded.achternaam, geboortedatum = excluded.geboortedatum, email = excluded.email,
        telefoon = excluded.telefoon, laatst_gewijzigd = excluded.laatst_gewijzigd;

    delete from public.voogden where lid_id is not null; -- met WHERE: Supabase blokkeert DELETE zonder WHERE (safeupdate)
    insert into public.voogden (lid_id, volgnummer, naam, email, telefoon, mobiel)
    select (l ->> 'id')::uuid, (v ->> 'volgnummer')::smallint, v ->> 'naam', v ->> 'email', v ->> 'telefoon', v ->> 'mobiel'
    from jsonb_array_elements(coalesce(gegevens -> 'leden', '[]')) l,
         jsonb_array_elements(coalesce(l -> 'voogden', '[]')) v;

    delete from public.lid_speltakken where lid_id is not null;
    insert into public.lid_speltakken (lid_id, speltak, rol, is_leiding)
    select distinct (l ->> 'id')::uuid, f ->> 'speltak', f ->> 'rol', coalesce((f ->> 'is_leiding')::boolean, false)
    from jsonb_array_elements(coalesce(gegevens -> 'leden', '[]')) l,
         jsonb_array_elements(coalesce(l -> 'functies', '[]')) f
    on conflict do nothing;

    delete from public.speltakken s
    where s.naam not in (select jsonb_array_elements_text(coalesce(gegevens -> 'speltakken', '[]')))
      and not exists (select 1 from public.leiding_speltakken l where l.speltak = s.naam);

    return jsonb_build_object('leden', cardinality(ids), 'verwijderd', verwijderd);
end
$$;

-- Keurt een voorstel goed of af (besluit 34); de desktop-app past een goedgekeurd voorstel lokaal toe.
create function public.handel_voorstel_af(voorstel uuid, nieuwe_status text, uitleg text default null) returns void
language plpgsql volatile security definer set search_path = '' as $$
begin
    perform public.controleer_beheerder();
    if nieuwe_status not in ('goedgekeurd', 'afgewezen') then
        raise exception 'Ongeldige status %', nieuwe_status using errcode = '22023';
    end if;

    update public.voorstellen
    set status = nieuwe_status, afgehandeld_op = now(), toelichting = uitleg
    where id = voorstel and status = 'open';

    if not found then
        raise exception 'Voorstel niet gevonden of al afgehandeld' using errcode = 'P0002';
    end if;
end
$$;

-- Legt vast voor welke speltakken een leider toegang heeft (besluit 230).
create function public.zet_leiding_speltakken(gebruiker uuid, speltakken text[]) returns void
language plpgsql volatile security definer set search_path = '' as $$
begin
    perform public.controleer_beheerder();
    if not exists (select 1 from public.leiding where user_id = gebruiker) then
        raise exception 'Onbekende leider' using errcode = 'P0002';
    end if;

    delete from public.leiding_speltakken where user_id = gebruiker;
    insert into public.leiding_speltakken (user_id, speltak)
    select gebruiker, s from unnest(speltakken) s
    where exists (select 1 from public.speltakken p where p.naam = s);
end
$$;

revoke all on function public.controleer_beheerder(), public.synchroniseer_leden(jsonb),
    public.handel_voorstel_af(uuid, text, text), public.zet_leiding_speltakken(uuid, text[]) from public, anon;
grant execute on function public.synchroniseer_leden(jsonb), public.handel_voorstel_af(uuid, text, text),
    public.zet_leiding_speltakken(uuid, text[]) to authenticated;
