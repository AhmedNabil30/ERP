using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.MasterData;

/// <summary>
/// A supplier. Owned by Finance (spec.md §2): "one account, serves many projects."
/// </summary>
/// <remarks>
/// The single account is why <see cref="AccountType.SupplierPayable"/> is company-scoped rather than
/// project-scoped. Project attribution happens on the cost side of the posting, not on the supplier's
/// sub-ledger.
///
/// Supplier bidding, RFQ and quote comparison are out of scope (spec.md §1) and must not be added here.
/// </remarks>
public sealed class Supplier : Entity
{
    public const int MaxCodeLength = 32;
    public const int MaxNameLength = 200;

    private Supplier()
    {
    }

    private Supplier(
        Guid id,
        string code,
        string name,
        PhoneNumber phone,
        WithholdingCategory withholdingCategory,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        Name = name;
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        WithholdingCategory = withholdingCategory;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string PhoneEntered { get; private set; } = null!;

    public string PhoneNormalised { get; private set; } = null!;

    /// <summary>Kaff withholds tax when paying suppliers and carries the liability (spec.md §6.7).</summary>
    public WithholdingCategory WithholdingCategory { get; private set; }

    public string? TaxRegistrationNumber { get; private set; }

    public string? Address { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public PhoneNumber Phone => PhoneNumber.FromStorage(PhoneEntered, PhoneNormalised);

    public static Result<Supplier> Create(
        string code,
        string name,
        PhoneNumber phone,
        DateTimeOffset createdAt,
        WithholdingCategory withholdingCategory = WithholdingCategory.ContractingAndSupplies)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Supplier>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Result.Failure<Supplier>(MasterDataErrors.NameRequired);
        }

        return Result.Success(new Supplier(
            NewId(),
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            phone,
            withholdingCategory,
            createdAt));
    }

    public void SetTaxDetails(WithholdingCategory category, string? taxRegistrationNumber)
    {
        WithholdingCategory = category;
        TaxRegistrationNumber = string.IsNullOrWhiteSpace(taxRegistrationNumber) ? null : taxRegistrationNumber.Trim();
    }

    public void SetAddress(string? address) => Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

    public Result Archive()
    {
        if (!IsActive)
        {
            return Result.Failure(MasterDataErrors.AlreadyArchived);
        }

        IsActive = false;
        return Result.Success();
    }
}
