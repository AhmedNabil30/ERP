-- =============================================================================================
--  Kaff ERP — database guards
--
--  Every rule in this file is one that MUST hold regardless of which code reaches the database.
--  CLAUDE.md and spec.md §6.1 require several of them to be enforced here rather than in the
--  application, because application code is one session away from being rewritten and a support
--  script run at 2am does not go through it at all.
--
--  This script is idempotent. It is applied after the schema, on every start-up, and re-running it
--  is safe.
--
--  Requires PostgreSQL 15 or later. NULLS NOT DISTINCT (section 4) is 15+; everything else needs 14.
--  Running this on 14 fails the whole script with a syntax error at start-up, which is the correct
--  outcome — a database that cannot hold the guards must not hold the money.
-- =============================================================================================


-- ---------------------------------------------------------------------------------------------
--  1. Append-only tables
--
--  CLAUDE.md: "Never update or delete a posting. Postings are append-only. Corrections are new
--  reversing postings that reference the original through ReversesId. There is no update path and
--  no delete path. Do not add one, not even for admins, not even for 'fixing test data.'"
--
--  The same protection covers audit_records: evidence that can be edited is not evidence.
-- ---------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION kaff_reject_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION
        'KAFF_APPEND_ONLY: % is append-only; % is not permitted. Corrections are new reversing rows.',
        TG_TABLE_NAME, TG_OP
        USING ERRCODE = 'restrict_violation',
              HINT = 'See CLAUDE.md and spec.md 6.1. Insert a reversing posting instead.';
END;
$$;

DROP TRIGGER IF EXISTS trg_postings_append_only ON postings;
CREATE TRIGGER trg_postings_append_only
    BEFORE UPDATE OR DELETE ON postings
    FOR EACH ROW EXECUTE FUNCTION kaff_reject_mutation();

DROP TRIGGER IF EXISTS trg_postings_no_truncate ON postings;
CREATE TRIGGER trg_postings_no_truncate
    BEFORE TRUNCATE ON postings
    FOR EACH STATEMENT EXECUTE FUNCTION kaff_reject_mutation();

DROP TRIGGER IF EXISTS trg_audit_records_append_only ON audit_records;
CREATE TRIGGER trg_audit_records_append_only
    BEFORE UPDATE OR DELETE ON audit_records
    FOR EACH ROW EXECUTE FUNCTION kaff_reject_mutation();

DROP TRIGGER IF EXISTS trg_audit_records_no_truncate ON audit_records;
CREATE TRIGGER trg_audit_records_no_truncate
    BEFORE TRUNCATE ON audit_records
    FOR EACH STATEMENT EXECUTE FUNCTION kaff_reject_mutation();


-- ---------------------------------------------------------------------------------------------
--  2. Posting validity
--
--  Checked before the row lands. Each rule is also implemented in Kaff.Domain.Treasury.Posting so
--  that the user gets a translated message rather than a database error — but the database is the
--  authority. If the two ever disagree, the domain has the bug.
-- ---------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION kaff_postings_validate() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    v_from            accounts%ROWTYPE;
    v_to              accounts%ROWTYPE;
    v_original        postings%ROWTYPE;
    v_account_project uuid;
BEGIN
    SELECT * INTO v_from FROM accounts WHERE id = NEW.from_account_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'KAFF_ACCOUNT_MISSING: from_account_id % does not exist.', NEW.from_account_id
            USING ERRCODE = 'foreign_key_violation';
    END IF;

    SELECT * INTO v_to FROM accounts WHERE id = NEW.to_account_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'KAFF_ACCOUNT_MISSING: to_account_id % does not exist.', NEW.to_account_id
            USING ERRCODE = 'foreign_key_violation';
    END IF;

    -- Structural roll-up nodes are not postable (spec.md 6.3).
    IF NOT v_from.is_postable OR NOT v_to.is_postable THEN
        RAISE EXCEPTION 'KAFF_ACCOUNT_NOT_POSTABLE: % or % is a roll-up node, not a postable account.',
            v_from.code, v_to.code
            USING ERRCODE = 'restrict_violation';
    END IF;

    IF NOT v_from.is_active OR NOT v_to.is_active THEN
        RAISE EXCEPTION 'KAFF_ACCOUNT_INACTIVE: % or % is closed.', v_from.code, v_to.code
            USING ERRCODE = 'restrict_violation';
    END IF;

    -- spec.md 1: a currency field exists, conversion logic does not. Two sides, one currency.
    IF v_from.currency <> v_to.currency THEN
        RAISE EXCEPTION 'KAFF_CURRENCY_MISMATCH: % is % and % is %.',
            v_from.code, v_from.currency, v_to.code, v_to.currency
            USING ERRCODE = 'restrict_violation';
    END IF;

    -- spec.md 6.4 / CLAUDE.md: "The five ledgers never net against each other: client advance, hold,
    -- firm advance, عهدة, owner current account. No calculation may offset one against another."
    IF v_from.ledger_kind IS NOT NULL
       AND v_to.ledger_kind IS NOT NULL
       AND v_from.ledger_kind <> v_to.ledger_kind THEN
        RAISE EXCEPTION 'KAFF_LEDGER_NETTING: cannot move value from the % ledger to the % ledger.',
            v_from.ledger_kind, v_to.ledger_kind
            USING ERRCODE = 'restrict_violation',
                  HINT = 'spec.md 6.4 keeps the five ledgers separate. Route the movement through a cash or party account.';
    END IF;

    -- spec.md 5.1 / CLAUDE.md: "The hold only grows. Nothing comes out of it mid-project — no snag,
    -- no debit note, no adjustment. It releases once, in full, at handover."
    --
    -- A reversal is exempt: it corrects an accrual that should never have existed, which is not the
    -- same thing as taking money out of the hold.
    IF v_from.ledger_kind = 'Hold' AND NEW."type" <> 'HoldRelease' AND NEW.reverses_id IS NULL THEN
        RAISE EXCEPTION 'KAFF_HOLD_DEBIT: posting type % cannot take value out of the hold ledger.', NEW."type"
            USING ERRCODE = 'restrict_violation',
                  HINT = 'spec.md 5.1. The hold releases once, in full, at handover, as a HoldRelease posting.';
    END IF;

    -- spec.md 6.10: every movement is tagged project or company, never both, never neither.
    IF v_from.project_id IS NOT NULL
       AND v_to.project_id IS NOT NULL
       AND v_from.project_id <> v_to.project_id THEN
        RAISE EXCEPTION 'KAFF_CROSS_PROJECT: % belongs to project % and % to project %.',
            v_from.code, v_from.project_id, v_to.code, v_to.project_id
            USING ERRCODE = 'restrict_violation';
    END IF;

    v_account_project := COALESCE(v_from.project_id, v_to.project_id);

    IF NEW.project_id IS DISTINCT FROM v_account_project THEN
        RAISE EXCEPTION 'KAFF_PROJECT_TAG: posting names project % but its accounts belong to %.',
            NEW.project_id, v_account_project
            USING ERRCODE = 'restrict_violation',
                  HINT = 'spec.md 6.10: tagged project or company, never both, never neither.';
    END IF;

    -- spec.md 6.6: "Month-end close — a closed period is immutable."
    IF EXISTS (
        SELECT 1 FROM accounting_periods ap
        WHERE ap.status = 'Closed'
          AND NEW.posting_date BETWEEN ap.starts_on AND ap.ends_on
    ) THEN
        RAISE EXCEPTION 'KAFF_CLOSED_PERIOD: % falls inside a closed accounting period.', NEW.posting_date
            USING ERRCODE = 'restrict_violation',
                  HINT = 'Date the correcting posting in an open period.';
    END IF;

    -- spec.md 6.1: "Corrections are new reversing postings referencing the original."
    -- A reversal mirrors its original exactly. A reversal that could differ from its original would
    -- be an editable posting wearing a disguise.
    IF NEW.reverses_id IS NOT NULL THEN
        SELECT * INTO v_original FROM postings WHERE id = NEW.reverses_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'KAFF_REVERSAL_TARGET_MISSING: posting % does not exist.', NEW.reverses_id
                USING ERRCODE = 'foreign_key_violation';
        END IF;

        IF NEW.amount <> v_original.amount
           OR NEW.from_account_id <> v_original.to_account_id
           OR NEW.to_account_id <> v_original.from_account_id
           OR NEW."type" <> v_original."type" THEN
            RAISE EXCEPTION 'KAFF_REVERSAL_MISMATCH: a reversal must mirror its original exactly.'
                USING ERRCODE = 'restrict_violation',
                      HINT = 'Same amount, same type, accounts swapped. Partial reversals are not supported.';
        END IF;

        -- A reversal cannot itself be reversed. The unique index on reverses_id stops the same
        -- posting being reversed twice, but without this a chain — accrue, reverse, reverse the
        -- reversal, reverse that — walks around the hold rule above indefinitely, because every
        -- reversal is exempt from it. One correction per posting, and the correction is final.
        IF v_original.reverses_id IS NOT NULL THEN
            RAISE EXCEPTION 'KAFF_REVERSAL_OF_REVERSAL: posting % is already a correction.', NEW.reverses_id
                USING ERRCODE = 'restrict_violation',
                      HINT = 'Re-post the correct entry instead of reversing the reversal.';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_postings_validate ON postings;
CREATE TRIGGER trg_postings_validate
    BEFORE INSERT ON postings
    FOR EACH ROW EXECUTE FUNCTION kaff_postings_validate();


-- ---------------------------------------------------------------------------------------------
--  3. The balance floor
--
--  spec.md 6.1: "The safe balance MUST NOT go negative. A payment that would breach this fails and
--  prompts an owner injection instead. Enforce in the database, not only in application code."
--
--  spec.md 15 extends the same requirement to the client advance ledger: "Advance ledger reaches
--  exactly zero, never negative." One mechanism serves both — accounts.enforce_non_negative marks
--  which accounts are floored, and the check is on the signed balance so it reads correctly for
--  liabilities as well as assets.
--
--  WHICH accounts are floored is data, not code: Karim's ruling of 2026-08-20 sets it at exactly
--  three types — Safe, ClientAdvance and PettyCashAdvance. Hold, FirmAdvance and MaterialAdvance
--  were floored before that ruling and are not now. Nothing in this function changed; the flag on
--  the account rows did. See Domain/Treasury/AccountTypeMetadata.cs and decisions.md D-044.
--
--  Note that existing rows keep the flag they were created with, and guard 3c makes account
--  configuration immutable. A database seeded before 2026-08-20 therefore keeps the old floors.
--
--  Deferred to commit, so a multi-posting transaction is judged on its final state rather than on
--  the order the postings happen to be inserted in.
--
--  Concurrency: two transactions could each see a sufficient balance and together overdraw it. The
--  advisory lock serialises the check per account, and the accounts are locked in identifier order
--  so two transactions touching the same pair cannot deadlock.
-- ---------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION kaff_check_non_negative_balance() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    v_account  RECORD;
    v_inflow   numeric(18,4);
    v_outflow  numeric(18,4);
    v_signed   numeric(18,4);
BEGIN
    FOR v_account IN
        SELECT a.id, a.code, a.normal_balance
        FROM accounts a
        WHERE a.id IN (NEW.from_account_id, NEW.to_account_id)
          AND a.enforce_non_negative
        ORDER BY a.id
    LOOP
        PERFORM pg_advisory_xact_lock(hashtextextended(v_account.id::text, 0));

        SELECT COALESCE(SUM(p.amount), 0) INTO v_inflow
          FROM postings p WHERE p.to_account_id = v_account.id;

        SELECT COALESCE(SUM(p.amount), 0) INTO v_outflow
          FROM postings p WHERE p.from_account_id = v_account.id;

        v_signed := (v_inflow - v_outflow)
                  * CASE WHEN v_account.normal_balance = 'Debit' THEN 1 ELSE -1 END;

        IF v_signed < 0 THEN
            RAISE EXCEPTION 'KAFF_NEGATIVE_BALANCE: account % would fall to %.', v_account.code, v_signed
                USING ERRCODE = 'check_violation',
                      HINT = 'spec.md 6.1. Record an owner injection before making this payment.';
        END IF;
    END LOOP;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_postings_non_negative_balance ON postings;
CREATE CONSTRAINT TRIGGER trg_postings_non_negative_balance
    AFTER INSERT ON postings
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION kaff_check_non_negative_balance();


-- ---------------------------------------------------------------------------------------------
--  3b. The hold releases once, in full
--
--  spec.md 5.1: "It releases once, in full, at handover, even with minor snags open."
--
--  The handover precondition is a project-state check and lives with the handover flow — the ledger
--  cannot see project state without coupling the treasury to projects. But "in full" IS expressible
--  here, and it is the half that stops a partial drain: after any HoldRelease the hold must be
--  exactly zero. That closes both a partial release and a second one.
-- ---------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION kaff_hold_releases_in_full() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    v_hold_account uuid;
    v_inflow       numeric(18,4);
    v_outflow      numeric(18,4);
BEGIN
    SELECT a.id INTO v_hold_account
    FROM accounts a
    WHERE a.id = NEW.from_account_id AND a.ledger_kind = 'Hold';

    IF NOT FOUND THEN
        RETURN NULL;
    END IF;

    SELECT COALESCE(SUM(p.amount), 0) INTO v_inflow
      FROM postings p WHERE p.to_account_id = v_hold_account;

    SELECT COALESCE(SUM(p.amount), 0) INTO v_outflow
      FROM postings p WHERE p.from_account_id = v_hold_account;

    IF (v_inflow - v_outflow) <> 0 THEN
        RAISE EXCEPTION
            'KAFF_HOLD_PARTIAL_RELEASE: the hold must be released in full; % remains.',
            (v_inflow - v_outflow)
            USING ERRCODE = 'restrict_violation',
                  HINT = 'spec.md 5.1: the hold releases once, in full, at handover.';
    END IF;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_postings_hold_release_in_full ON postings;
CREATE CONSTRAINT TRIGGER trg_postings_hold_release_in_full
    AFTER INSERT ON postings
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    WHEN (NEW."type" = 'HoldRelease')
    EXECUTE FUNCTION kaff_hold_releases_in_full();


-- ---------------------------------------------------------------------------------------------
--  3c. Account configuration is immutable
--
--  Every guard above derives its authority from a row in `accounts`: the non-negative check reads
--  enforce_non_negative and normal_balance, the netting rule reads ledger_kind, the balances view
--  reads normal_balance. All of it was freely mutable — one UPDATE could switch off the safe floor
--  spec.md 6.1 makes a MUST, or invert the sign of every balance in the system at once.
--
--  Account.Create refuses to loosen the floor at creation; this stops it being loosened a second
--  later. Renaming, closing and reopening stay legal, because those change no rule.
-- ---------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION kaff_accounts_configuration_is_immutable() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW."type"              IS DISTINCT FROM OLD."type"
    OR NEW.class               IS DISTINCT FROM OLD.class
    OR NEW.normal_balance      IS DISTINCT FROM OLD.normal_balance
    OR NEW.ledger_kind         IS DISTINCT FROM OLD.ledger_kind
    OR NEW.is_postable         IS DISTINCT FROM OLD.is_postable
    OR NEW.enforce_non_negative IS DISTINCT FROM OLD.enforce_non_negative
    OR NEW.project_id          IS DISTINCT FROM OLD.project_id
    OR NEW.party_type          IS DISTINCT FROM OLD.party_type
    OR NEW.party_id            IS DISTINCT FROM OLD.party_id
    OR NEW.currency            IS DISTINCT FROM OLD.currency
    OR NEW.code                IS DISTINCT FROM OLD.code THEN
        RAISE EXCEPTION
            'KAFF_ACCOUNT_IMMUTABLE: account % configuration cannot be changed after creation.', OLD.code
            USING ERRCODE = 'restrict_violation',
                  HINT = 'Close the account and open a new one. Renaming and closing remain permitted.';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_accounts_configuration_immutable ON accounts;
CREATE TRIGGER trg_accounts_configuration_immutable
    BEFORE UPDATE ON accounts
    FOR EACH ROW EXECUTE FUNCTION kaff_accounts_configuration_is_immutable();


-- ---------------------------------------------------------------------------------------------
--  4. Uniqueness the model cannot express
--
--  spec.md 6.3: "Two dimensions only: project × party." One account of each type per point in that
--  grid — a project cannot end up with two hold ledgers for the same client, which would let a
--  balance be true and a report be wrong at the same time.
--
--  PostgreSQL treats NULLs as distinct in a unique index unless told otherwise, so a party-less
--  account would slip past a plain unique index. NULLS NOT DISTINCT closes that (PostgreSQL 15+).
--
--  Deliberately partial. Cash accounts are not constrained: Kaff may hold several bank accounts
--  (spec.md 6.3 names QNB, CIB and الأهلي) and may want more than one safe.
-- ---------------------------------------------------------------------------------------------

DROP INDEX IF EXISTS ux_accounts_dimension;

DROP INDEX IF EXISTS ux_accounts_project_dimension;
CREATE UNIQUE INDEX ux_accounts_project_dimension
    ON accounts (type, project_id, party_type, party_id)
    NULLS NOT DISTINCT
    WHERE project_id IS NOT NULL;

-- spec.md 6.4.5 speaks of "the owner current account" in the singular. One, company-wide.
DROP INDEX IF EXISTS ux_accounts_company_ledger;
CREATE UNIQUE INDEX ux_accounts_company_ledger
    ON accounts (type)
    WHERE ledger_kind IS NOT NULL AND project_id IS NULL;


-- ---------------------------------------------------------------------------------------------
--  5. Least privilege
--
--  The triggers above stop mistakes. Revoking the grants stops the operation from being attempted
--  at all, including from psql. Applied only if the application role exists, so a developer machine
--  connecting as the owner is unaffected.
-- ---------------------------------------------------------------------------------------------

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kaff_app') THEN
        EXECUTE 'REVOKE UPDATE, DELETE, TRUNCATE ON postings FROM kaff_app';
        EXECUTE 'REVOKE UPDATE, DELETE, TRUNCATE ON audit_records FROM kaff_app';
    END IF;
END;
$$;
