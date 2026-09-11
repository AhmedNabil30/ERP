using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Employees.GetEmployee;

/// <summary>
/// One employee's whole editable file, including the four staff fields <c>EmployeeSummary</c> omits.
/// </summary>
/// <remarks>
/// <b>Every field <c>EditEmployee.Request</c> takes, so the full-body <c>PUT</c> round-trips without
/// loss.</b> Frontend found (<c>31b049f</c>) that <c>GET /api/employees</c> does not return
/// <c>NationalId</c>, <c>Department</c>, <c>JobTitle</c> or <c>HiredOn</c>, and no by-id read existed — so an edit screen
/// could not load them, and every <c>PUT</c> (which applies its whole body, per
/// <c>EditEmployee.Handler</c>) silently wiped them. This is the <c>GetClient</c> precedent applied to
/// employees: the read that exists so the edit form can load what the edit form is about to save back.
/// </remarks>
/// <param name="Id">The employee.</param>
/// <param name="Code">Generated. Rendered read-only.</param>
/// <param name="FullName">As stored — trimmed.</param>
/// <param name="Phone">The entered form.</param>
/// <param name="Kind">Salaried or DayLabour. Immutable after creation (KAFF-208 rule 3).</param>
/// <param name="BabId">The trade/باب. Required when <see cref="Kind"/> is <c>DayLabour</c>.</param>
/// <param name="Specialty">Optional.</param>
/// <param name="NationalId">Optional, staff-only field.</param>
/// <param name="Department">Optional, staff-only field. Free text — decisions.md D-139 §7, D-144 §2.</param>
/// <param name="JobTitle">Optional, staff-only field.</param>
/// <param name="HiredOn">Optional, staff-only field.</param>
/// <param name="IsActive">False for an archived employee.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    EmployeeKind Kind,
    Guid? BabId,
    string? Specialty,
    string? NationalId,
    string? Department,
    string? JobTitle,
    DateOnly? HiredOn,
    bool IsActive);
