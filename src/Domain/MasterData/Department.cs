using Kaff.Domain.Common;

namespace Kaff.Domain.MasterData;

/// <summary>
/// A staff department — Finance, Technical Office, Operations, Procurement, HR and whatever an admin
/// adds later. KAFF-321.
/// </summary>
/// <remarks>
/// <para>
/// Superseded a compile-time <c>enum</c> (decisions.md D-153 §2) once Karim ruled departments change
/// over time and an enum cannot be edited without a deploy (decisions.md D-162, <c>Q85</c>). Shaped
/// exactly like <see cref="Bab"/> and <see cref="Employee"/> — the two other archive-not-delete master
/// records in this codebase — rather than inventing a fourth shape for a fifth master record.
/// </para>
/// <para>
/// <b>Archive, never delete</b> — Nabil's ruling 2026-09-15 (decisions.md), closing the gap D-166
/// flagged: a hard-delete path could remove the HR row and make <c>Role.Hr</c> unholdable forever.
/// There is no delete endpoint. <see cref="Archive"/> and <see cref="Unarchive"/> are the only
/// retirement path, same as <see cref="Bab"/> and <see cref="Employee"/>.
/// </para>
/// </remarks>
public sealed class Department : Entity
{
    public const int MaxNameLength = 200;

    private Department()
    {
    }

    private Department(Guid id, string nameAr, string nameEn)
        : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        IsActive = true;
    }

    public string NameAr { get; private set; } = null!;

    public string NameEn { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public static Result<Department> Create(string nameAr, string nameEn)
        => Create(NewId(), nameAr, nameEn);

    /// <summary>
    /// Creates a department with a caller-chosen id. Used only by the seed, so the five rows Karim
    /// named (decisions.md D-162) carry the fixed ids <see cref="WellKnownDepartments"/> binds business
    /// rules to — <see cref="Identity.User.ValidateDepartment"/>'s HR/Operations checks would have
    /// nothing stable to compare against otherwise, since an admin may rename any row at any time.
    /// </summary>
    public static Result<Department> Create(Guid id, string nameAr, string nameEn)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > MaxNameLength)
        {
            return Result.Failure<Department>(MasterDataErrors.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(nameEn) || nameEn.Length > MaxNameLength)
        {
            return Result.Failure<Department>(MasterDataErrors.NameRequired);
        }

        return Result.Success(new Department(id, nameAr.Trim(), nameEn.Trim()));
    }

    /// <summary>Corrects the Arabic and English names. KAFF-321 rule 2 / AC-321-C.</summary>
    public Result Rename(string nameAr, string nameEn)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > MaxNameLength)
        {
            return Result.Failure(MasterDataErrors.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(nameEn) || nameEn.Length > MaxNameLength)
        {
            return Result.Failure(MasterDataErrors.NameRequired);
        }

        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        return Result.Success();
    }

    /// <summary>AC-321-D — the archive path for a department staff are still assigned to.</summary>
    public Result Archive()
    {
        if (!IsActive)
        {
            return Result.Failure(MasterDataErrors.AlreadyArchived);
        }

        IsActive = false;
        return Result.Success();
    }

    /// <summary>
    /// Brings an archived department back into new assignment. KAFF-321, AC-321-F (revised — Nabil's
    /// ruling 2026-09-15, decisions.md: archive-never-delete for departments).
    /// </summary>
    /// <remarks>
    /// Same shape and reasoning as <see cref="CatalogueItem.Unarchive"/> (KAFF-206, D-130 §4 / Q66):
    /// refuses with <see cref="MasterDataErrors.NotArchived"/> rather than a silent no-op.
    /// </remarks>
    public Result Unarchive()
    {
        if (IsActive)
        {
            return Result.Failure(MasterDataErrors.NotArchived);
        }

        IsActive = true;
        return Result.Success();
    }
}
