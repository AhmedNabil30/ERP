using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Employees.EditEmployee;

/// <summary>
/// What HR (or the Owner) sends to correct an employee's file. KAFF-207, KAFF-208.
/// </summary>
/// <remarks>
/// <para>
/// <b>PUT, not PATCH — the body states the whole editable record.</b> Same precedent as
/// <c>EditClient.Request</c>.
/// </para>
/// <para>
/// <b>There is no <c>Code</c> member.</b> Decisions.md D-130 §6 — generated, never editable.
/// </para>
/// <para>
/// <b><see cref="Kind"/> is here to be refused, not to be applied.</b> KAFF-208 rule 3/D-130 §7: a
/// population is fixed at creation. <c>Handler</c> compares this to the stored value and returns
/// <c>errors.master.employee_kind_immutable</c> (<c>MasterDataErrors.EmployeeKindIsImmutable</c>)
/// before any field is touched when they differ — <c>AC-208-C</c>.
/// </para>
/// </remarks>
/// <param name="FullName">Required, correctable.</param>
/// <param name="Phone">The phone. Changing it re-runs the same uniqueness the database enforces (<c>ux_employees_phone</c>).</param>
/// <param name="Kind">Must equal the stored value — see remarks.</param>
/// <param name="BabId">The trade/باب. Required when the stored <see cref="Kind"/> is <c>DayLabour</c>.</param>
/// <param name="Specialty">Optional.</param>
/// <param name="NationalId">Optional, staff-only field already carried by the entity.</param>
/// <param name="Department">Optional, staff-only field. Free text — decisions.md D-139 §7, D-144 §2.</param>
/// <param name="JobTitle">Optional, staff-only field already carried by the entity.</param>
/// <param name="HiredOn">Optional, staff-only field already carried by the entity.</param>
public sealed record Request(
    string? FullName,
    string? Phone,
    EmployeeKind Kind,
    Guid? BabId,
    string? Specialty,
    string? NationalId,
    string? Department,
    string? JobTitle,
    DateOnly? HiredOn);
