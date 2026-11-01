-- =============================================================================================
--  Kaff ERP — the BOQ freeze guard
--
--  spec.md 4.4 (MUST): at contract signature the BOQ copies catalogue values. A signed BOQ cannot
--  change. A freeze by convention is not a freeze — a support script, a future handler or psql would
--  walk straight through it — so the rule is a trigger, in the style of kaff_reject_mutation in
--  001_guards.sql.
--
--  The marker is boqs.frozen_on, on the header. Freezing is therefore one row updated, however many
--  lines the BOQ has. Once it is set:
--    * no line may be inserted, updated or deleted, EXCEPT quantity_executed, the one column that
--      execution writes (spec.md 4.3: executed quantity is captured from the site daily log);
--    * the header cannot be unfrozen, re-dated or deleted.
--
--  Idempotent. Applied after the schema on every start-up.
-- =============================================================================================

CREATE OR REPLACE FUNCTION kaff_boq_lines_guard() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    v_frozen date;
BEGIN
    IF TG_OP = 'INSERT' THEN
        SELECT frozen_on INTO v_frozen FROM boqs WHERE id = NEW.boq_id;
        IF v_frozen IS NOT NULL THEN
            RAISE EXCEPTION 'KAFF_BOQ_FROZEN: boq % was frozen on %; its lines cannot change.', NEW.boq_id, v_frozen
                USING ERRCODE = 'restrict_violation',
                      HINT = 'spec.md 4.4. A signed BOQ is a frozen copy. Changes go through a change order.';
        END IF;
        RETURN NEW;
    END IF;

    SELECT frozen_on INTO v_frozen FROM boqs WHERE id = OLD.boq_id;

    IF v_frozen IS NOT NULL THEN
        -- Only the executed quantity may move on a frozen line. Everything else, compared as a whole
        -- row so a column added later is covered without editing this function.
        IF TG_OP = 'DELETE'
           OR (to_jsonb(NEW) - 'quantity_executed') IS DISTINCT FROM (to_jsonb(OLD) - 'quantity_executed') THEN
            RAISE EXCEPTION 'KAFF_BOQ_FROZEN: boq % was frozen on %; its lines cannot change.', OLD.boq_id, v_frozen
                USING ERRCODE = 'restrict_violation',
                      HINT = 'spec.md 4.4. A signed BOQ is a frozen copy. Changes go through a change order.';
        END IF;
    END IF;

    -- A line moved into a frozen BOQ is an insert into it.
    IF TG_OP = 'UPDATE' AND NEW.boq_id <> OLD.boq_id THEN
        SELECT frozen_on INTO v_frozen FROM boqs WHERE id = NEW.boq_id;
        IF v_frozen IS NOT NULL THEN
            RAISE EXCEPTION 'KAFF_BOQ_FROZEN: boq % was frozen on %; its lines cannot change.', NEW.boq_id, v_frozen
                USING ERRCODE = 'restrict_violation';
        END IF;
    END IF;

    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END;
$$;

DROP TRIGGER IF EXISTS trg_boq_lines_frozen ON boq_lines;
CREATE TRIGGER trg_boq_lines_frozen
    BEFORE INSERT OR UPDATE OR DELETE ON boq_lines
    FOR EACH ROW EXECUTE FUNCTION kaff_boq_lines_guard();

CREATE OR REPLACE FUNCTION kaff_boq_lines_reject_truncate() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'KAFF_BOQ_FROZEN: boq_lines cannot be truncated; a signed BOQ is a frozen copy.'
        USING ERRCODE = 'restrict_violation';
END;
$$;

DROP TRIGGER IF EXISTS trg_boq_lines_no_truncate ON boq_lines;
CREATE TRIGGER trg_boq_lines_no_truncate
    BEFORE TRUNCATE ON boq_lines
    FOR EACH STATEMENT EXECUTE FUNCTION kaff_boq_lines_reject_truncate();

CREATE OR REPLACE FUNCTION kaff_boqs_guard() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.frozen_on IS NOT NULL THEN
        IF TG_OP = 'DELETE' THEN
            RAISE EXCEPTION 'KAFF_BOQ_FROZEN: boq % was frozen on % and cannot be deleted.', OLD.id, OLD.frozen_on
                USING ERRCODE = 'restrict_violation';
        END IF;

        IF NEW.frozen_on IS DISTINCT FROM OLD.frozen_on
           OR NEW.project_id <> OLD.project_id THEN
            RAISE EXCEPTION 'KAFF_BOQ_FROZEN: boq % was frozen on %; the freeze cannot be undone or changed.', OLD.id, OLD.frozen_on
                USING ERRCODE = 'restrict_violation';
        END IF;
    END IF;

    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END;
$$;

DROP TRIGGER IF EXISTS trg_boqs_frozen ON boqs;
CREATE TRIGGER trg_boqs_frozen
    BEFORE UPDATE OR DELETE ON boqs
    FOR EACH ROW EXECUTE FUNCTION kaff_boqs_guard();
