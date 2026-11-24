-- =============================================================================================
--  Kaff ERP — a flow's posting is reversed by that flow
--
--  D-236 "Still open". An extract's collection, accrual and recoveries, an adjustment's note, the
--  handover's hold release and a subcontractor retention release are written by their flow, which
--  moves its document in the same transaction. A generic reversal of one of them would take the
--  money back and leave the document saying it is still there: an extract still Collected with no
--  collection, an adjustment still Approved with its note undone.
--
--  A posting belongs to a flow when its source document type is one a flow writes AND that document
--  exists. A movement posted by hand may carry the same type with an id that names nothing; it
--  stays reversible as a movement. Mirrored by Kaff.Domain.Treasury.SourceDocument.HasOwningFlow and
--  Posting.ReverseMovement (FlowPostingNotReversible).
--
--  What each flow's own reversal leaves behind, checked at commit (deferred, because EF does not
--  promise to insert the document's change before the posting):
--    * Extract: a gate record of that extract, Rejected or Reversed, stamped at the same instant as
--      the reversal. Extract.ReverseCollection and Extract.Reject stamp both with one clock read.
--    * Adjustment: the adjustment is Reversed, decided at the same instant. Adjustment.Reverse.
--    * SubcontractorRetentionRelease: an Approved subcontractor_retention_reversal_requests row for that posting,
--      decided at the same instant (KAFF-806 AC-806-D, Owner-approved).
--    * ProjectHandover: no flow reverses it, so nothing may.
--
--  Idempotent. Applied after the schema on every start-up.
-- =============================================================================================

CREATE OR REPLACE FUNCTION kaff_flow_reversal_owned() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    v_original RECORD;
    v_owned    boolean;
    v_moved    boolean;
BEGIN
    SELECT p.id, p.source_document_type, p.source_document_id INTO v_original
      FROM postings p WHERE p.id = NEW.reverses_id;

    IF NOT FOUND THEN
        RETURN NULL;   -- KAFF_REVERSAL_TARGET_MISSING has already refused it.
    END IF;

    v_owned := CASE v_original.source_document_type
        WHEN 'Extract' THEN EXISTS (SELECT 1 FROM extracts WHERE id = v_original.source_document_id)
        WHEN 'Adjustment' THEN EXISTS (SELECT 1 FROM adjustments WHERE id = v_original.source_document_id)
        WHEN 'ProjectHandover' THEN EXISTS (SELECT 1 FROM projects WHERE id = v_original.source_document_id)
        WHEN 'SubcontractorRetentionRelease' THEN EXISTS (SELECT 1 FROM subcontractors WHERE id = v_original.source_document_id)
        ELSE false
    END;

    IF NOT v_owned THEN
        RETURN NULL;
    END IF;

    v_moved := CASE v_original.source_document_type
        WHEN 'Extract' THEN EXISTS (
            SELECT 1 FROM extract_gate_records g
             WHERE g.extract_id = v_original.source_document_id
               AND g.outcome IN ('Rejected', 'Reversed')
               AND g.occurred_at = NEW.created_at)
        WHEN 'Adjustment' THEN EXISTS (
            SELECT 1 FROM adjustments a
             WHERE a.id = v_original.source_document_id
               AND a.status = 'Reversed'
               AND a.decided_at = NEW.created_at)
        WHEN 'SubcontractorRetentionRelease' THEN EXISTS (
            SELECT 1 FROM subcontractor_retention_reversal_requests r
             WHERE r.original_posting_id = v_original.id
               AND r.status = 'Approved'
               AND r.decided_at = NEW.created_at)
        ELSE false
    END;

    IF NOT v_moved THEN
        RAISE EXCEPTION 'KAFF_FLOW_REVERSAL: posting % belongs to % %; only that flow reverses it.',
            v_original.id, v_original.source_document_type, v_original.source_document_id
            USING ERRCODE = 'restrict_violation',
                  HINT = 'D-236. Reverse the collection, reject the extract or reverse the adjustment instead.';
    END IF;

    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS trg_postings_flow_reversal ON postings;
CREATE CONSTRAINT TRIGGER trg_postings_flow_reversal
    AFTER INSERT ON postings
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    WHEN (NEW.reverses_id IS NOT NULL)
    EXECUTE FUNCTION kaff_flow_reversal_owned();
