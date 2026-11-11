-- =============================================================================================
--  Kaff ERP - one project name per client
--
--  D-217 item 34 (KAFF-419): the same project name is refused for the same client. Names are stored
--  trimmed (Project.Create), so lower(name) is the whole comparison: case-insensitive, and every
--  project of the client counts, archived, stopped or closed. An expression index cannot be written
--  in the EF model, so it is a guard script, applied after the schema on every start-up like the
--  others (the test schema is built from the model, so a migration would never reach it).
--
--  Idempotent. Fails at start-up if a client already holds two projects whose names differ only by
--  case: the owner renames one first.
-- =============================================================================================

CREATE UNIQUE INDEX IF NOT EXISTS ux_projects_client_name ON projects (client_id, lower(name));
