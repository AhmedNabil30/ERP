-- Development only. Creates the least-privilege role the API connects as (ConnectionStrings:KaffDatabase in
-- src/Api/appsettings.Development.json), with the development password kaff_app.
--
-- Mounted into docker-compose.yml's /docker-entrypoint-initdb.d, so a fresh kaff-db-data volume gets it on first
-- start, and run by scripts/dev-db-least-privilege.ps1 on a volume that already holds data. Idempotent: an
-- existing kaff_app keeps its password and is stripped of any attribute it must not hold. The table grants are
-- 005_app_role.sql's, applied by the API at every start-up; the ps1 also applies them at once.
--
-- Staging and production never run this file: their password is a secret. See deploy/README.md.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kaff_app') THEN
        CREATE ROLE kaff_app LOGIN PASSWORD 'kaff_app';
    END IF;
END;
$$;

ALTER ROLE kaff_app WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT;
