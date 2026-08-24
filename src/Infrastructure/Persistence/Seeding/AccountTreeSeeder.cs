using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kaff.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates the company-level accounts of spec.md §6.3 that must exist before any money moves.
/// </summary>
/// <remarks>
/// <para>
/// Idempotent and additive: it inserts an account only if its code is absent, and never edits or
/// removes one. Seeding runs against a live database, so it behaves like every other write in this
/// system — it adds, it does not rewrite history.
/// </para>
/// <para>
/// <b>What is deliberately not seeded.</b>
/// Bank accounts, because spec.md §6.3 lists QNB, CIB and الأهلي as examples and the real list is
/// Karim's to give. A VAT payable account, because spec.md §6.7 and assumption 15 leave Kaff's
/// registration status open — seeding one would invite somebody to use it. A loan account, for the
/// same reason under assumption 16. Project accounts and party sub-ledgers, because they are created
/// with the project and the party they belong to.
/// </para>
/// </remarks>
public sealed class AccountTreeSeeder
{
    private readonly KaffDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccountTreeSeeder> _logger;

    public AccountTreeSeeder(KaffDbContext context, TimeProvider timeProvider, ILogger<AccountTreeSeeder> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>The main cash safe — خزنة. Its balance can never go negative (spec.md §6.1).</summary>
    public const string MainSafeCode = "SAFE-MAIN";

    /// <summary>The company / overhead control node (spec.md §6.3).</summary>
    public const string CompanyControlCode = "COMPANY";

    /// <summary>جاري المالك (spec.md §6.4.5).</summary>
    public const string OwnerCurrentAccountCode = "OWNER-CURRENT";

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        List<string> existingCodes = await _context.Accounts
            .Select(account => account.Code)
            .ToListAsync(cancellationToken);

        var existing = new HashSet<string>(existingCodes, StringComparer.Ordinal);

        List<AccountSeed> seeds = BuildSeeds();
        Dictionary<string, Guid> created = [];
        int inserted = 0;

        // Two passes so a child can reference a parent seeded in the same run.
        foreach (AccountSeed seed in seeds.Where(s => s.ParentCode is null))
        {
            inserted += await AddIfMissingAsync(seed, parentId: null, existing, created, today, cancellationToken);
        }

        foreach (AccountSeed seed in seeds.Where(s => s.ParentCode is not null))
        {
            Guid? parentId = await ResolveParentAsync(seed.ParentCode!, created, cancellationToken);
            inserted += await AddIfMissingAsync(seed, parentId, existing, created, today, cancellationToken);
        }

        if (inserted > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} company-level accounts.", inserted);
        }

        return inserted;
    }

    private async Task<int> AddIfMissingAsync(
        AccountSeed seed,
        Guid? parentId,
        HashSet<string> existing,
        Dictionary<string, Guid> created,
        DateOnly openedOn,
        CancellationToken cancellationToken)
    {
        if (existing.Contains(seed.Code))
        {
            return 0;
        }

        Result<Account> result = Account.Create(
            seed.Type,
            seed.Code,
            seed.NameAr,
            seed.NameEn,
            Currency.Egp,
            openedOn,
            parentAccountId: parentId);

        if (result.IsFailure)
        {
            // Seed data is code. A failure here is a defect, not a business outcome.
            throw new InvalidOperationException(
                $"Seed account '{seed.Code}' is invalid: {result.Error.Code}.");
        }

        await _context.Accounts.AddAsync(result.Value, cancellationToken);
        created[seed.Code] = result.Value.Id;
        return 1;
    }

    private async Task<Guid?> ResolveParentAsync(
        string parentCode,
        Dictionary<string, Guid> created,
        CancellationToken cancellationToken)
    {
        if (created.TryGetValue(parentCode, out Guid id))
        {
            return id;
        }

        Account? parent = await _context.Accounts
            .FirstOrDefaultAsync(account => account.Code == parentCode, cancellationToken);

        return parent?.Id;
    }

    private static List<AccountSeed> BuildSeeds() =>
    [
        // ---- Cash (spec.md §6.3) ----
        new(MainSafeCode, AccountType.Safe, "الخزنة الرئيسية", "Main safe", null),

        // ---- Company / overhead (spec.md §6.3) ----
        new(CompanyControlCode, AccountType.CompanyControl, "حساب الشركة", "Company account", null),

        // ---- Owner (spec.md §6.4.5) ----
        new(OwnerCurrentAccountCode, AccountType.OwnerCurrentAccount, "جاري المالك", "Owner current account", null),
        new("OWNER-DRAWINGS", AccountType.OwnerDrawings, "مسحوبات المالك", "Owner drawings", null),

        // ---- Equity (spec.md §6.6) ----
        new("EQUITY-CAPITAL", AccountType.PaidInCapital, "رأس المال المدفوع", "Paid-in capital", null),
        new("EQUITY-RETAINED", AccountType.RetainedEarnings, "أرباح مرحلة", "Retained earnings", null),
        new("EQUITY-CURRENT-YEAR", AccountType.CurrentYearProfit, "أرباح العام الحالي", "Current year profit", null),

        // ---- Assets (spec.md §6.6) ----
        new("ASSET-FIXED", AccountType.FixedAsset, "أصول ثابتة", "Fixed assets", null),
        new("ASSET-ACC-DEPRECIATION", AccountType.AccumulatedDepreciation, "مجمع الإهلاك", "Accumulated depreciation", null),

        // ---- Withholding tax (spec.md §6.7). Two accounts — this is not a tax module. ----
        new("TAX-WHT-RECEIVABLE", AccountType.TaxWithheldAtSource, "ضريبة مخصومة تحت الحساب", "Tax withheld at source", null),
        new("TAX-WHT-PAYABLE", AccountType.TaxWithholdingPayable, "ضريبة خصم واجبة السداد", "Withholding tax payable", null),

        // ---- Overheads (spec.md §6.10) ----
        new("EXP-COMPANY", AccountType.CompanyExpense, "مصروفات الشركة", "Company expenses", CompanyControlCode),
        new("EXP-BANK-CHARGES", AccountType.BankCharge, "مصاريف بنكية", "Bank charges", CompanyControlCode),
        new("EXP-DEPRECIATION", AccountType.DepreciationExpense, "مصروف الإهلاك", "Depreciation expense", CompanyControlCode),
    ];

    private sealed record AccountSeed(string Code, AccountType Type, string NameAr, string NameEn, string? ParentCode);
}
