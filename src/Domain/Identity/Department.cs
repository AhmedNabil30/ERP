namespace Kaff.Domain.Identity;

/// <summary>
/// spec.md §9 amendment: "Operations subdivides into Technical (quantities, BOQ, extract gate),
/// Financial (site expenses, عهدة), and Administrative (reports, photos, tasks)."
/// </summary>
/// <remarks>
/// Set only when the user's department is <see cref="WellKnownDepartments.OperationsId"/>. The
/// <c>User</c> entity enforces that invariant.
/// </remarks>
public enum OperationsSubDepartment
{
    /// <summary>Quantities, BOQ, extract gate.</summary>
    Technical = 1,

    /// <summary>Site expenses, عهدة.</summary>
    Financial = 2,

    /// <summary>Reports, photos, tasks.</summary>
    Administrative = 3,
}

/// <summary>
/// The fixed ids of the five departments decisions.md D-162 (<c>Q85</c>) seeds, and the two — HR and
/// Operations — that the fixed rules of <see cref="User.ValidateDepartment"/> bind to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why fixed ids at all, once <c>Department</c> became master data (KAFF-321).</b> A C# enum gave
/// <c>ValidateDepartment</c> a compile-time value to compare against — "the HR department" was
/// <c>Department.Hr</c>, full stop. Master data has no such thing: an admin can rename any row, and two
/// environments seeded independently would mint different ids for "the same" department if nothing
/// pinned them down. Seeding with these fixed ids (<see cref="Kaff.Domain.MasterData.Department.Create(Guid, string, string)"/>)
/// is what lets <c>ValidateDepartment</c> go on asking "is this the HR department" without asking "what
/// is that department named today" — the name is exactly what D-162 lets an admin change.
/// </para>
/// <para>
/// <b>The delete gap D-166 flagged is closed, not guarded.</b> A hard-delete path once let the HR row
/// be removed once no <see cref="User"/> referenced it, which would have made <see cref="Role.Hr"/>
/// unholdable forever. Nabil's ruling 2026-09-15 (decisions.md) removed the delete path entirely —
/// <c>Department</c> only archives and unarchives — so this row can never vanish.
/// </para>
/// </remarks>
public static class WellKnownDepartments
{
    public static readonly Guid FinanceId = Guid.Parse("00000000-0000-0000-0000-000000000201");

    public static readonly Guid TechnicalOfficeId = Guid.Parse("00000000-0000-0000-0000-000000000202");

    public static readonly Guid OperationsId = Guid.Parse("00000000-0000-0000-0000-000000000203");

    public static readonly Guid ProcurementId = Guid.Parse("00000000-0000-0000-0000-000000000204");

    public static readonly Guid HrId = Guid.Parse("00000000-0000-0000-0000-000000000205");
}
