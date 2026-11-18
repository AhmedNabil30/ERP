using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Npgsql;

namespace Kaff.Api.Common.Results;

/// <summary>
/// Maps a raw <see cref="PostgresException"/> raised by a Treasury database guard
/// (<c>src/Infrastructure/Persistence/Sql/001_guards.sql</c>) back to the <see cref="TreasuryErrors"/>
/// member that means the same refusal. KAFF-319.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every guard's exception maps here, or it does not map at all.</b> A guard exception this table
/// does not recognise must still reach the generic exception handler as a 500 (AC-319-D) — that is
/// what returning <see langword="null"/> from <see cref="Translate"/> achieves: the caller's own
/// <c>catch ... when</c> clause only fires when this method returns a non-null <see cref="Error"/>.
/// </para>
/// <para>
/// Two guard prefixes in <c>001_guards.sql</c> are absent from <see cref="Map"/> because no request
/// performs the statement they refuse: <c>KAFF_APPEND_ONLY</c> (an update, delete or truncate of a
/// posting or audit record) and <c>KAFF_ACCOUNT_IMMUTABLE</c> (an update of an account's configuration).
/// </para>
/// </remarks>
public static class DatabaseGuardTranslation
{
    /// <summary>Every guard prefix a posting insert can raise, each resolving to its domain error.</summary>
    public static readonly IReadOnlyDictionary<string, Error> Map = new Dictionary<string, Error>(StringComparer.Ordinal)
    {
        ["KAFF_NEGATIVE_BALANCE"] = TreasuryErrors.NegativeBalance,
        ["KAFF_LEDGER_NETTING"] = TreasuryErrors.LedgersMustNotNet,
        ["KAFF_HOLD_DEBIT"] = TreasuryErrors.HoldOnlyGrows,
        ["KAFF_HOLD_REVERSAL"] = TreasuryErrors.HoldPostingNotReversible,
        ["KAFF_HOLD_PARTIAL_RELEASE"] = TreasuryErrors.HoldReleaseMustBeInFull,
        ["KAFF_CLOSED_PERIOD"] = TreasuryErrors.ClosedPeriod,
        ["KAFF_REVERSAL_MISMATCH"] = TreasuryErrors.ReversalMustMirrorOriginal,
        ["KAFF_REVERSAL_OF_REVERSAL"] = TreasuryErrors.ReversalOfReversal,
        ["KAFF_REVERSAL_TARGET_MISSING"] = TreasuryErrors.ReversalTargetNotFound,
        ["KAFF_CROSS_PROJECT"] = TreasuryErrors.CrossProjectPosting,
        ["KAFF_PROJECT_TAG"] = TreasuryErrors.ProjectTagRequired,
        ["KAFF_ACCOUNT_NOT_POSTABLE"] = TreasuryErrors.AccountNotPostable,
        ["KAFF_ACCOUNT_INACTIVE"] = TreasuryErrors.AccountInactive,
        ["KAFF_CURRENCY_MISMATCH"] = TreasuryErrors.CurrencyMismatch,
        ["KAFF_ACCOUNT_MISSING"] = TreasuryErrors.AccountNotFound,
        ["KAFF_POSTING_TYPE_ACCOUNT_MISMATCH"] = TreasuryErrors.PostingTypeAccountMismatch,

        // 003_boq_freeze.sql — a signed BOQ is a frozen copy (spec.md 4.4).
        ["KAFF_BOQ_FROZEN"] = BoqErrors.Frozen,
    };

    /// <summary>
    /// The <see cref="TreasuryErrors"/> member <paramref name="exception"/> maps to, or
    /// <see langword="null"/> when it is not one of <see cref="Map"/>'s prefixes — in which case the
    /// caller must let it propagate (AC-319-D), never swallow it.
    /// </summary>
    public static Error? Translate(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not PostgresException postgres)
            {
                continue;
            }

            foreach ((string prefix, Error error) in Map)
            {
                if (postgres.MessageText.Contains(prefix, StringComparison.Ordinal))
                {
                    return error;
                }
            }

            return null;
        }

        return null;
    }
}
