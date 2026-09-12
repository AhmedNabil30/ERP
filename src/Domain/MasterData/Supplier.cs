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
/// No withholding category on this record — decisions.md D-139 §5, KAFF-212 rule 4: the rate is set
/// per contract/job, on KAFF-318's ground, not here.
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
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        Name = name;
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string PhoneEntered { get; private set; } = null!;

    public string PhoneNormalised { get; private set; } = null!;

    /// <summary>
    /// Identifies the legal entity; does not vary by job (KAFF-212 rule 6). Entered and managed by
    /// Finance only (D-139 §5) — but <c>SupplierManage</c> is already Finance-and-Owner-only, so unlike
    /// the subcontractor (D-147) no separate permission row or endpoint is needed: this is set directly
    /// on create/edit under <c>SupplierManage</c>.
    /// </summary>
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
        string? address = null,
        string? taxRegistrationNumber = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Supplier>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Result.Failure<Supplier>(MasterDataErrors.NameRequired);
        }

        var supplier = new Supplier(NewId(), code.Trim().ToUpperInvariant(), name.Trim(), phone, createdAt);
        supplier.SetAddress(address);
        supplier.SetTaxRegistration(taxRegistrationNumber);
        return Result.Success(supplier);
    }

    /// <summary>
    /// Corrects the name, phone, address and tax registration number in one call — KAFF-212. Unlike
    /// the subcontractor (D-147), no split: Finance already owns the whole supplier record through
    /// <c>SupplierManage</c>, so one edit, one audit record (AC-212-J).
    /// </summary>
    public Result Edit(string name, PhoneNumber phone, string? address, string? taxRegistrationNumber)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Result.Failure(MasterDataErrors.NameRequired);
        }

        Name = name.Trim();
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        SetAddress(address);
        SetTaxRegistration(taxRegistrationNumber);
        return Result.Success();
    }

    public void SetAddress(string? address) => Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

    /// <summary>
    /// Sets or clears the tax registration number. Renamed from <c>SetTaxDetails</c> — decisions.md
    /// D-139 §5, D-147 point 5 — now that the withholding category never lived here (KAFF-212 rule 4).
    /// </summary>
    public void SetTaxRegistration(string? taxRegistrationNumber)
        => TaxRegistrationNumber = string.IsNullOrWhiteSpace(taxRegistrationNumber) ? null : taxRegistrationNumber.Trim();

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
