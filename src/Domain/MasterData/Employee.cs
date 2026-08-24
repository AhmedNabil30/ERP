using Kaff.Domain.Common;

namespace Kaff.Domain.MasterData;

/// <summary>
/// Which of the two costed populations a person belongs to.
/// </summary>
/// <remarks>
/// spec.md §10: "Two populations, one source each: day labour (يومية) costed from the daily log,
/// salaried staff from timesheets. Nobody appears in both." The kind is immutable after creation —
/// a person who moves from day labour onto the payroll needs a decision from HR about their history,
/// not a silent field edit.
/// </remarks>
public enum EmployeeKind
{
    /// <summary>Salaried staff, costed from timesheets (spec.md §10).</summary>
    Salaried = 1,

    /// <summary>يومية — day labour, costed from the daily log. The "worker registry" of spec.md §10.</summary>
    DayLabour = 2,
}

/// <summary>
/// Every costed person. Owned by HR (spec.md §2), exactly one record each.
/// </summary>
/// <remarks>
/// spec.md §2 names this entity "Employee / Worker" and requires "every costed person, exactly one
/// record". It is therefore one table with a <see cref="Kind"/>, not two tables that would let the
/// same person exist twice. The worker registry of spec.md §10 is this entity filtered to
/// <see cref="EmployeeKind.DayLabour"/>.
///
/// OPEN QUESTION — see decisions.md D-016. If Karim wants Worker and Employee kept as visibly
/// separate registers, that is a spec.md clarification, not a schema preference.
///
/// Engagement history and per-engagement ratings (spec.md §10) are not modelled here; they belong to
/// the HR slice.
/// </remarks>
public sealed class Employee : Entity
{
    public const int MaxCodeLength = 32;
    public const int MaxNameLength = 200;

    private Employee()
    {
    }

    private Employee(
        Guid id,
        string code,
        string fullName,
        PhoneNumber phone,
        EmployeeKind kind,
        Guid? babId,
        string? specialty,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        FullName = fullName;
        PhoneEntered = phone.Entered;
        PhoneNormalised = phone.Normalised;
        Kind = kind;
        BabId = babId;
        Specialty = specialty;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public string Code { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string PhoneEntered { get; private set; } = null!;

    /// <summary>Digits-only national form. spec.md §10 deduplicates workers by phone.</summary>
    public string PhoneNormalised { get; private set; } = null!;

    public EmployeeKind Kind { get; private set; }

    /// <summary>Trade / باب. Required for day labour (spec.md §10).</summary>
    public Guid? BabId { get; private set; }

    /// <summary>Free-text specialty within the trade (spec.md §10).</summary>
    public string? Specialty { get; private set; }

    public string? NationalId { get; private set; }

    public string? JobTitle { get; private set; }

    public DateOnly? HiredOn { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public PhoneNumber Phone => PhoneNumber.FromStorage(PhoneEntered, PhoneNormalised);

    public static Result<Employee> Create(
        string code,
        string fullName,
        PhoneNumber phone,
        EmployeeKind kind,
        DateTimeOffset createdAt,
        Guid? babId = null,
        string? specialty = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Employee>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > MaxNameLength)
        {
            return Result.Failure<Employee>(MasterDataErrors.NameRequired);
        }

        if (kind == EmployeeKind.DayLabour && babId is null)
        {
            // spec.md §10: workers are registered with a trade / باب.
            return Result.Failure<Employee>(MasterDataErrors.DayLabourRequiresTrade);
        }

        return Result.Success(new Employee(
            NewId(),
            code.Trim().ToUpperInvariant(),
            fullName.Trim(),
            phone,
            kind,
            babId,
            string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim(),
            createdAt));
    }

    public void SetStaffDetails(string? nationalId, string? jobTitle, DateOnly? hiredOn)
    {
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        HiredOn = hiredOn;
    }

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
