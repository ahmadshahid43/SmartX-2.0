-- SmartX — Phase 1 Supabase Go-Live
-- 0002_rls.sql : defense-in-depth only.
--
-- In phase 1 the ASP.NET Core API is the security boundary. The browser NEVER talks to
-- Supabase directly and holds no Supabase key, so PostgREST's auto-generated REST/GraphQL
-- endpoints (reachable with the public anon key) must not expose any application table.
--
-- Strategy:
--   1. Enable Row Level Security on every application table. With NO policies defined, RLS
--      denies all access to roles that are subject to it -- which includes Supabase's
--      `anon` and `authenticated` roles. This is the real protection.
--   2. Additionally revoke table privileges from `anon`/`authenticated` (belt and suspenders).
--
-- The API connects as a dedicated application role that is created WITH BYPASSRLS (see the
-- commented bootstrap at the bottom and docs/go-live/hosting-plan.md), so enabling RLS here
-- does NOT lock out the application. Do not grant the app role to the browser.

begin;

-- 1 + 2: enable RLS and revoke anon/authenticated grants on every table in public.
do $$
declare
    target_table text;
    revoke_roles text := '';
begin
    if exists (select 1 from pg_roles where rolname = 'anon') then
        revoke_roles := 'anon';
    end if;

    if exists (select 1 from pg_roles where rolname = 'authenticated') then
        revoke_roles := case when revoke_roles = '' then 'authenticated'
                             else revoke_roles || ', authenticated' end;
    end if;

    for target_table in
        select tablename
        from pg_tables
        where schemaname = 'public'
    loop
        execute format('alter table public.%I enable row level security;', target_table);
        execute format('alter table public.%I force row level security;', target_table);

        if revoke_roles <> '' then
            execute format('revoke all privileges on public.%I from %s;', target_table, revoke_roles);
        end if;
    end loop;
end $$;

commit;

-- ---------------------------------------------------------------------------
-- App role bootstrap (RUN ONCE PER PROJECT, OUTSIDE version control with a real secret).
-- Kept commented because it needs a password and must not live in a committed migration.
--
--   create role smartx_app with login password '<<set-via-secret>>' bypassrls;
--   grant usage on schema public to smartx_app;
--   grant select, insert, update, delete on all tables in schema public to smartx_app;
--   grant usage, select on all sequences in schema public to smartx_app;
--   alter default privileges in schema public
--       grant select, insert, update, delete on tables to smartx_app;
--   alter default privileges in schema public
--       grant usage, select on sequences to smartx_app;
--
-- Then set Persistence__ConnectionString to connect as smartx_app with sslmode=require.
-- Because smartx_app has BYPASSRLS, the RLS enabled above never blocks the API while it
-- still blocks the public anon/authenticated roles.
-- ---------------------------------------------------------------------------
