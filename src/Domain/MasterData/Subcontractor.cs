using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.MasterData;

/// <summary>
/// مقاول باطن. Rates and BOQ are owned by the Technical Office; Finance only disburses (spec.md §2).
/// </summary>
/// <remarks>
/// spec.md §5.1: "Kaff retains 5% from every sub extract, released when the project's warranty ends
/// (4 months after handover), zeroable per subcontractor 🟡." The rate lives here so it can be zeroed
/// per subcontractor without touching anything global.
///
/// A subcontractor never logs in — spec.md §9 says "record only, no login". A <c>User</c> row with
/// this role therefore cannot hold a password.
/// </remarks>
public sealed class Subcontractor : Entity
{
    public const int MaxCodeLength = 32;
    public const int MaxNameLength = 200;

    /// <summary>spec.md §5.1 — 5% retention, released at warranty end. 🟡 assumption 19.</summary>
    public static readonly Percentage DefaultRetentionRate = Percentage.FromPercent(5m);

    private Subcontractor()
    {
    }

    private Subcontractor(
        Guid id,
        string code,
        string name,
        PhoneNumber phone,
        Guid? tradeBabId,
        Percentage retentionRate,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        Name = name;
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        TradeBabId = tradeBabId;
        RetentionRate = retentionRate;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string PhoneEntered { get; private set; } = null!;

    public string PhoneNormalised { get; private set; } = null!;

    /// <summary>The باب this subcontractor works in (spec.md §2).</summary>
    public Guid? TradeBabId { get; private set; }

    /// <summary>Retention Kaff holds from each of this subcontractor's extracts (spec.md §5.1).</summary>
    public Percentage RetentionRate { get; private set; }

    public string? TaxRegistrationNumber { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public PhoneNumber Phone => PhoneNumber.FromStorage(PhoneEntered, PhoneNormalised);

    public static Result<Subcontractor> Create(
        string code,
        string name,
        PhoneNumber phone,
        DateTimeOffset createdAt,
        Guid? tradeBabId = null,
        Percentage? retentionRate = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Subcontractor>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Result.Failure<Subcontractor>(MasterDataErrors.NameRequired);
        }

        return Result.Success(new Subcontractor(
            NewId(),
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            phone,
            tradeBabId,
            retentionRate ?? DefaultRetentionRate,
            createdAt));
    }

    /// <summary>spec.md §5.1 allows the retention to be zeroed per subcontractor 🟡.</summary>
    public void SetRetentionRate(Percentage rate) => RetentionRate = rate;

    /// <summary>
    /// Corrects the name, phone and trade باب. KAFF-211. The retention rate is a separate call
    /// (<see cref="SetRetentionRate"/>) so a caller that only means to change one need not restate the
    /// other, and the tax registration number is never touched here — D-147 reaches it only through
    /// <see cref="SetTaxRegistration"/>, gated separately.
    /// </summary>
    public Result Edit(string name, PhoneNumber phone, Guid? tradeBabId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Result.Failure(MasterDataErrors.NameRequired);
        }

        Name = name.Trim();
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        TradeBabId = tradeBabId;
        return Result.Success();
    }

    /// <summary>
    /// Sets or clears the tax registration number. decisions.md D-147 point 4 — renamed from
    /// <c>SetTaxDetails</c> now that the withholding category has moved off this record (D-139 §5,
    /// KAFF-211 rule 5); reached only through <c>PUT /api/subcontractors/{id}/tax-registration</c>,
    /// gated <see cref="Authorization.Permission.SubcontractorTaxRegistrationEdit"/>, never through
    /// <c>SubcontractorManage</c>'s create/edit requests.
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
