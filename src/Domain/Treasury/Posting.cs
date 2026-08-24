using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// One financial event. The only way value moves anywhere in the system.
/// </summary>
/// <remarks>
/// <para>
/// The shape is exactly the one spec.md §6.1 specifies:
/// <c>id · date · fromAccount · toAccount · amount · type · sourceDocument · projectId? ·
/// createdBy · createdAt · reversesId?</c> — nothing added, nothing dropped.
/// </para>
/// <para>
/// <b>Append-only.</b> There is no setter, no mutating method, no <c>Update</c>, no <c>Delete</c>.
/// The database refuses <c>UPDATE</c>, <c>DELETE</c> and <c>TRUNCATE</c> on the table with a trigger,
/// so the rule survives a session that forgets it, a support script written at 2am, and psql. A
/// correction is <see cref="Reverse"/>: a new posting that mirrors the original and points at it
/// through <see cref="ReversesId"/>.
/// </para>
/// <para>
/// <b>Amount is always positive.</b> Direction lives in the pair of accounts, never in the sign. A
/// signed amount plus a from/to pair would give two ways to express the same movement and therefore
/// two ways to get it wrong.
/// </para>
/// </remarks>
public sealed class Posting : Entity
{
    private Posting()
    {
    }

    private Posting(
        Guid id,
        DateOnly postingDate,
        Guid fromAccountId,
        Guid toAccountId,
        Money amount,
        PostingType type,
        SourceDocument sourceDocument,
        Guid? projectId,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        Guid? reversesId)
        : base(id)
    {
        PostingDate = postingDate;
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        Amount = amount;
        Type = type;
        SourceDocumentType = sourceDocument.Type;
        SourceDocumentId = sourceDocument.Id;
        SourceDocumentReference = sourceDocument.Reference;
        ProjectId = projectId;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        ReversesId = reversesId;
    }

    /// <summary>The accounting date. Distinct from <see cref="CreatedAt"/>, which is when it was keyed in.</summary>
    public DateOnly PostingDate { get; private set; }

    public Guid FromAccountId { get; private set; }

    public Guid ToAccountId { get; private set; }

    /// <summary>Always positive. decimal(18,4). spec.md §6.1.</summary>
    public Money Amount { get; private set; }

    public PostingType Type { get; private set; }

    public SourceDocumentType SourceDocumentType { get; private set; }

    public Guid SourceDocumentId { get; private set; }

    public string? SourceDocumentReference { get; private set; }

    /// <summary>The project this movement belongs to, or null for company-level movements (spec.md §6.10).</summary>
    public Guid? ProjectId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Set on a correcting posting. Points at the posting being undone. spec.md §6.1: "Corrections
    /// are new reversing postings referencing the original."
    /// </summary>
    public Guid? ReversesId { get; private set; }

    public SourceDocument SourceDocument =>
        new(SourceDocumentType, SourceDocumentId, SourceDocumentReference);

    public bool IsReversal => ReversesId is not null;

    public PostingNature Nature => PostingTypes.NatureOf(Type);

    /// <summary>
    /// Creates a posting after checking every rule the domain can see. The database re-checks all of
    /// them plus the balance and closed-period rules, which need to see the whole ledger.
    /// </summary>
    public static Result<Posting> Create(
        Account from,
        Account to,
        Money amount,
        PostingType type,
        SourceDocument sourceDocument,
        DateOnly postingDate,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        Guid? projectId = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        Result check = Validate(from, to, amount, type, projectId, isReversal: false);
        if (check.IsFailure)
        {
            return Result.Failure<Posting>(check.Error);
        }

        return Result.Success(new Posting(
            NewId(),
            postingDate,
            from.Id,
            to.Id,
            amount,
            type,
            sourceDocument,
            projectId,
            createdByUserId,
            createdAt,
            reversesId: null));
    }

    /// <summary>
    /// Produces the correcting posting for <paramref name="original"/>.
    /// </summary>
    /// <remarks>
    /// The reversal is a full mirror — same amount, same type, same source document, accounts
    /// swapped. It is not a free-form entry with a reference field, because a "reversal" that could
    /// differ from its original would be an editable posting wearing a disguise. Partial reversals
    /// are therefore impossible; if the business needs them, that is a spec.md change, not a
    /// loosening here. See decisions.md D-007.
    ///
    /// A database unique index on <c>reverses_id</c> allows each posting to be reversed once only.
    /// </remarks>
    public static Result<Posting> Reverse(
        Posting original,
        Account originalFrom,
        Account originalTo,
        DateOnly postingDate,
        Guid createdByUserId,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(originalFrom);
        ArgumentNullException.ThrowIfNull(originalTo);

        if (originalFrom.Id != original.FromAccountId || originalTo.Id != original.ToAccountId)
        {
            return Result.Failure<Posting>(TreasuryErrors.ReversalMustMirrorOriginal);
        }

        // Accounts are swapped, so the "from" side of the reversal is the original's "to" account.
        Result check = Validate(originalTo, originalFrom, original.Amount, original.Type, original.ProjectId, isReversal: true);
        if (check.IsFailure)
        {
            return Result.Failure<Posting>(check.Error);
        }

        return Result.Success(new Posting(
            NewId(),
            postingDate,
            original.ToAccountId,
            original.FromAccountId,
            original.Amount,
            original.Type,
            original.SourceDocument,
            original.ProjectId,
            createdByUserId,
            createdAt,
            reversesId: original.Id));
    }

    private static Result Validate(
        Account from,
        Account to,
        Money amount,
        PostingType type,
        Guid? projectId,
        bool isReversal)
    {
        if (!amount.IsPositive)
        {
            return Result.Failure(TreasuryErrors.AmountMustBePositive);
        }

        if (from.Id == to.Id)
        {
            return Result.Failure(TreasuryErrors.SameAccountBothSides);
        }

        if (!from.IsPostable || !to.IsPostable)
        {
            return Result.Failure(TreasuryErrors.AccountNotPostable);
        }

        if (!from.IsActive || !to.IsActive)
        {
            return Result.Failure(TreasuryErrors.AccountInactive);
        }

        if (from.Currency != to.Currency)
        {
            return Result.Failure(TreasuryErrors.CurrencyMismatch);
        }

        // spec.md §6.4 — the five ledgers never net against each other.
        if (from.LedgerKind is not null && to.LedgerKind is not null && from.LedgerKind != to.LedgerKind)
        {
            return Result.Failure(TreasuryErrors.LedgersMustNotNet);
        }

        // spec.md §5.1 — "The hold posts into its own ledger that only grows during the project.
        // Nothing may be taken out of it mid-project — not a snag, not a debit note, not an
        // adjustment. It releases once, in full, at handover."
        //
        // A reversal is exempt because it corrects an accrual that should never have been made; it
        // is not a withdrawal from the hold. The handover precondition on HoldRelease is a project
        // state check and lives with the handover flow, not here — the ledger cannot see project state.
        if (from.LedgerKind == Treasury.LedgerKind.Hold && type != PostingType.HoldRelease && !isReversal)
        {
            return Result.Failure(TreasuryErrors.HoldOnlyGrows);
        }

        return ValidateProjectTag(from, to, projectId);
    }

    private static Result ValidateProjectTag(Account from, Account to, Guid? projectId)
    {
        Guid? fromProject = from.ProjectId;
        Guid? toProject = to.ProjectId;

        if (fromProject is not null && toProject is not null && fromProject != toProject)
        {
            return Result.Failure(TreasuryErrors.CrossProjectPosting);
        }

        Guid? accountProject = fromProject ?? toProject;

        if (accountProject is null)
        {
            // Both sides are company-level, so the posting is company-level too.
            // spec.md §6.10: tagged project or company, never both, never neither.
            return projectId is null ? Result.Success() : Result.Failure(TreasuryErrors.ProjectTagForbidden);
        }

        return projectId == accountProject
            ? Result.Success()
            : Result.Failure(TreasuryErrors.ProjectTagRequired);
    }
}
