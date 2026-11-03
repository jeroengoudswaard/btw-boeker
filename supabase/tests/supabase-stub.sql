-- Alleen voor tests: het minimum van Supabase dat de migratie nodig heeft
-- (rollen, auth.users, auth.uid(), auth.jwt() en de standaardrechten van Supabase op het schema public).
-- Op een echt Supabase-project bestaat dit al; dit bestand gaat nooit naar de server.

create role anon nologin;
create role authenticated nologin;
create role service_role nologin bypassrls;

create schema auth;
create table auth.users (id uuid primary key, email text);

create function auth.jwt() returns jsonb language sql stable as $$
    select coalesce(nullif(current_setting('request.jwt.claims', true), ''), '{}')::jsonb
$$;

create function auth.uid() returns uuid language sql stable as $$
    select nullif(auth.jwt() ->> 'sub', '')::uuid
$$;

grant usage on schema auth to anon, authenticated, service_role;
grant execute on all functions in schema auth to anon, authenticated, service_role;
grant usage on schema public to anon, authenticated, service_role;

-- Zoals Supabase: nieuwe tabellen en functies in public zijn standaard voor iedereen.
-- De migratie moet dat zelf intrekken; de tests bewaken dat.
alter default privileges in schema public grant all on tables to anon, authenticated, service_role;
alter default privileges in schema public grant all on functions to anon, authenticated, service_role;
