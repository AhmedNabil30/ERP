-- =============================================================================================
--  Kaff ERP — what the application's own role may do
--
--  qa/slice-3/verification-2026-11-18.md F-1. The append-only triggers bind every role except one
--  that can switch them off: a superuser (session_replication_role = replica) or the owner of the
--  table (ALTER TABLE ... DISABLE TRIGGER). The running application therefore connects as kaff_app,
--  which owns nothing and is no superuser, and migrations and these scripts run as the owner
--  (ConnectionStrings:KaffDatabaseOwner).
--
--  kaff_app is created outside this script, because creating a role needs a password and the
--  password is a secret: scripts/db/dev-app-role.sql on a developer machine, the operator step in
--  deploy/README.md on staging. This script grants, and only if the role exists.
--
--  The grants, derived from what EF writes (decisions.md, the F-1 entry):
--    * every table: SELECT and INSERT. That is the whole of postings and audit_records, and of every
--      record that is never changed once written (gate records, collection records, daily-log
--      entries, change-order lines, the extract's change-order lines and quantity lines, BOQ base
--      lines).
--    * UPDATE only on the tables whose entities have behaviour that changes a written row.
--    * DELETE only on the three child tables EF removes rows from: a draft BOQ's lines
--      (Boq.RemoveLine), a BOQ base's lines (UpdateBoqBase) and an extract's figures (Extract
--      recomputes them).
--    * no TRUNCATE, REFERENCES or TRIGGER anywhere; nothing on the migrations history.
--    * USAGE on the sequences, which is what nextval needs.
--
--  Idempotent and convergent: everything is revoked and granted again in one statement, so a grant
--  added by hand does not survive the next start-up. Default privileges give a table created by a
--  later migration SELECT and INSERT before this script next runs; UPDATE and DELETE on a new table
--  are a line added here.
-- =============================================================================================

DO $$
DECLARE
    v_table text;
    v_updatable text[] := ARRAY[
        'Departments', 'accounting_periods', 'accounts', 'adjustments', 'babs', 'boq_bases',
        'boq_lines', 'boqs', 'catalogue_items', 'change_orders', 'cheques', 'clients', 'daily_logs',
        'employees', 'engagements', 'extract_figures', 'extracts', 'hold_release_requests', 'ncrs',
        'opportunities', 'project_assignments', 'projects', 'snags', 'subcontractors', 'suppliers',
        'users', 'warranty_callbacks'];
    v_deletable text[] := ARRAY['boq_lines', 'boq_base_lines', 'extract_figures'];
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kaff_app') THEN
        RETURN;
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO kaff_app', current_database());
    GRANT USAGE ON SCHEMA public TO kaff_app;
    REVOKE CREATE ON SCHEMA public FROM kaff_app;

    REVOKE ALL ON ALL TABLES IN SCHEMA public FROM kaff_app;
    REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM kaff_app;

    GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA public TO kaff_app;
    IF to_regclass('public.__ef_migrations_history') IS NOT NULL THEN
        REVOKE ALL ON TABLE __ef_migrations_history FROM kaff_app;
    END IF;
    GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO kaff_app;

    FOREACH v_table IN ARRAY v_updatable LOOP
        IF to_regclass(format('public.%I', v_table)) IS NOT NULL THEN
            EXECUTE format('GRANT UPDATE ON TABLE public.%I TO kaff_app', v_table);
        END IF;
    END LOOP;

    FOREACH v_table IN ARRAY v_deletable LOOP
        IF to_regclass(format('public.%I', v_table)) IS NOT NULL THEN
            EXECUTE format('GRANT DELETE ON TABLE public.%I TO kaff_app', v_table);
        END IF;
    END LOOP;

    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT ON TABLES TO kaff_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE ON SEQUENCES TO kaff_app;
END;
$$;
