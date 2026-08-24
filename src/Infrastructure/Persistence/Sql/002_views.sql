-- =============================================================================================
--  Kaff ERP — derived balances
--
--  CLAUDE.md: "Never store a balance. Balances are derived by summing postings, always. If you find
--  yourself adding a Balance column, stop — that's the bug."
--
--  This view is the derivation. There is no balance column anywhere in the schema, so there is
--  nothing for it to drift from, and no code path that can write a balance.
--
--  raw_balance    inflow − outflow, ignoring the account's direction.
--  signed_balance raw_balance in the account's own direction, so a liability with money owed on it
--                 reads positive. This is what the non-negative guard checks and what reports show.
--
--  Idempotent: dropped and recreated, because CREATE OR REPLACE VIEW refuses a changed column list.
-- =============================================================================================

DROP VIEW IF EXISTS account_balances;

CREATE VIEW account_balances AS
SELECT
    a.id                                                            AS account_id,
    a.code                                                          AS account_code,
    a.name_ar                                                       AS name_ar,
    a.name_en                                                       AS name_en,
    a.type                                                          AS type,
    a.class                                                         AS class,
    a.normal_balance                                                AS normal_balance,
    a.ledger_kind                                                   AS ledger_kind,
    a.project_id                                                    AS project_id,
    a.party_type                                                    AS party_type,
    a.party_id                                                      AS party_id,
    a.currency                                                      AS currency,

    COALESCE(inflow.total, 0)::numeric(18, 4)                       AS inflow,
    COALESCE(outflow.total, 0)::numeric(18, 4)                      AS outflow,

    (COALESCE(inflow.total, 0) - COALESCE(outflow.total, 0))::numeric(18, 4)
                                                                    AS raw_balance,

    ((COALESCE(inflow.total, 0) - COALESCE(outflow.total, 0))
        * CASE WHEN a.normal_balance = 'Debit' THEN 1 ELSE -1 END)::numeric(18, 4)
                                                                    AS signed_balance,

    (COALESCE(inflow.count, 0) + COALESCE(outflow.count, 0))::int   AS posting_count,

    -- GREATEST ignores NULLs in PostgreSQL, so an account with movement on one side only still
    -- reports its last posting date.
    GREATEST(inflow.last_date, outflow.last_date)                   AS last_posting_date

FROM accounts a

LEFT JOIN (
    SELECT to_account_id AS account_id,
           SUM(amount)   AS total,
           COUNT(*)      AS count,
           MAX(posting_date) AS last_date
    FROM postings
    GROUP BY to_account_id
) inflow ON inflow.account_id = a.id

LEFT JOIN (
    SELECT from_account_id AS account_id,
           SUM(amount)     AS total,
           COUNT(*)        AS count,
           MAX(posting_date) AS last_date
    FROM postings
    GROUP BY from_account_id
) outflow ON outflow.account_id = a.id;


COMMENT ON VIEW account_balances IS
    'Derived balances. spec.md 6.1 forbids storing a balance; this view is the only way to read one.';
