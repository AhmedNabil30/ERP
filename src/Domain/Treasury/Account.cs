using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// A node in the account tree of spec.md §6.3.
/// </summary>
/// <remarks>
/// <para>
/// There is no balance column on this entity and there must never be one. CLAUDE.md: "Never store a
/// balance. Balances are derived by summing postings, always." Balances come from the
/// <c>account_balances</c> database view — see <see cref="AccountBalance"/>.
/// </para>
/// <para>
/// <see cref="Class"/>, <see cref="NormalBalance"/>, <see cref="LedgerKind"/>,
/// <see cref="IsPostable"/> and <see cref="EnforceNonNegative"/> are copied from
/// <see cref="AccountTypes"/> at creation. They are constants of the account type, not derived
/// business values, and the database triggers read them directly — a trigger that had to re-derive
/// them would be a second copy of the same table written in SQL.
/// </para>
/// </remarks>
public sealed class Account : Entity
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 200;

    private Account()
    {
    }

    private Account(
        Guid id,
        string code,
        string nameAr,
        string nameEn,
        AccountTypeMetadata meta,
        Currency currency,
        Guid? projectId,
        PartyType? partyType,
        Guid? partyId,
        Guid? parentAccountId,
        bool enforceNonNegative,
        DateOnly openedOn)
        : base(id)
    {
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        Type = meta.Type;
        Class = meta.Class;
        NormalBalance = meta.NormalBalance;
        LedgerKind = meta.LedgerKind;
        IsPostable = meta.IsPostable;
        EnforceNonNegative = enforceNonNegative;
        Currency = currency;
        ProjectId = projectId;
        PartyType = partyType;
        PartyId = partyId;
        ParentAccountId = parentAccountId;
        OpenedOn = openedOn;
        IsActive = true;
    }

    /// <summary>Stable human-readable code, e.g. <c>SAFE-MAIN</c> or <c>PRJ-0007-HOLD</c>.</summary>
    public string Code { get; private set; } = null!;

    public string NameAr { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public AccountType Type { get; private set; }

    public AccountClass Class { get; private set; }

    public NormalBalance NormalBalance { get; private set; }

    /// <summary>Set for the five ledgers of spec.md §6.4; null otherwise.</summary>
    public LedgerKind? LedgerKind { get; private set; }

    /// <summary>Structural roll-up nodes are not postable. A posting to one is refused.</summary>
    public bool IsPostable { get; private set; }

    /// <summary>
    /// When true, the database refuses any posting that would drive this account's signed balance
    /// below zero. True for the safe (spec.md §6.1) and for the ledgers that spec.md §15 requires to
    /// bottom out at exactly zero.
    /// </summary>
    public bool EnforceNonNegative { get; private set; }

    public Currency Currency { get; private set; }

    public Guid? ProjectId { get; private set; }

    public PartyType? PartyType { get; private set; }

    /// <summary>Identifier of the Client, Subcontractor, Supplier or Employee this sub-ledger belongs to.</summary>
    public Guid? PartyId { get; private set; }

    public Guid? ParentAccountId { get; private set; }

    public DateOnly OpenedOn { get; private set; }

    public DateOnly? ClosedOn { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<Account> Create(
        AccountType type,
        string code,
        string nameAr,
        string nameEn,
        Currency currency,
        DateOnly openedOn,
        Guid? projectId = null,
        PartyType? partyType = null,
        Guid? partyId = null,
        Guid? parentAccountId = null,
        bool? enforceNonNegative = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Account>(TreasuryErrors.AccountCodeRequired);
        }

        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            return Result.Failure<Account>(TreasuryErrors.AccountNameRequired);
        }

        AccountTypeMetadata meta = AccountTypes.Of(type);

        Result scopeCheck = ValidateScope(meta, projectId);
        if (scopeCheck.IsFailure)
        {
            return Result.Failure<Account>(scopeCheck.Error);
        }

        Result partyCheck = ValidateParty(meta, partyType, partyId);
        if (partyCheck.IsFailure)
        {
            return Result.Failure<Account>(partyCheck.Error);
        }

        // The metadata default may be tightened but never loosened: spec.md §6.1 makes the safe rule
        // a MUST, so an account that defaults to enforced cannot be created unenforced.
        bool enforce = meta.EnforceNonNegative || (enforceNonNegative ?? false);

        return Result.Success(new Account(
            NewId(),
            code.Trim().ToUpperInvariant(),
            nameAr.Trim(),
            nameEn.Trim(),
            meta,
            currency,
            projectId,
            partyType,
            partyId,
            parentAccountId,
            enforce,
            openedOn));
    }

    public Result Rename(string nameAr, string nameEn)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            return Result.Failure(TreasuryErrors.AccountNameRequired);
        }

        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Closes the account to new postings. History is untouched — closing is not deleting, and the
    /// account keeps answering balance queries for every period it was open.
    /// </summary>
    public Result Close(DateOnly closedOn)
    {
        if (!IsActive)
        {
            return Result.Failure(TreasuryErrors.AccountAlreadyClosed);
        }

        IsActive = false;
        ClosedOn = closedOn;
        return Result.Success();
    }

    public Result Reopen()
    {
        if (IsActive)
        {
            return Result.Failure(TreasuryErrors.AccountNotClosed);
        }

        IsActive = true;
        ClosedOn = null;
        return Result.Success();
    }

    private static Result ValidateScope(AccountTypeMetadata meta, Guid? projectId) => meta.Scope switch
    {
        AccountScope.ProjectRequired when projectId is null => Result.Failure(TreasuryErrors.AccountRequiresProject),
        AccountScope.CompanyWide when projectId is not null => Result.Failure(TreasuryErrors.AccountMustNotCarryProject),
        _ => Result.Success(),
    };

    private static Result ValidateParty(AccountTypeMetadata meta, PartyType? partyType, Guid? partyId)
    {
        if (meta.RequiredParty is null)
        {
            return partyType is null && partyId is null
                ? Result.Success()
                : Result.Failure(TreasuryErrors.AccountMustNotCarryParty);
        }

        if (partyType is null || partyId is null)
        {
            return Result.Failure(TreasuryErrors.AccountRequiresParty);
        }

        return partyType == meta.RequiredParty
            ? Result.Success()
            : Result.Failure(TreasuryErrors.AccountPartyTypeMismatch);
    }
}
