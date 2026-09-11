using Kaff.Domain.MasterData;

namespace Kaff.Api.Features.Employees.CreateEmployee;

/// <summary>
/// What HR (or the Owner, D-129 §1) sends to register one costed person. KAFF-207.
/// </summary>
/// <remarks>
/// <b>There is no <c>Code</c> member.</b> Decisions.md D-130 §6 — the reference number is generated,
/// the same shape as <c>CreateClient</c>'s (<c>C-10001</c>); a code in the body binds to nothing.
/// <b>No field beyond §10's four worker fields and the staff fields already built.</b> Decisions.md
/// D-130 §8 refuses to guess what Karim's paper staff file carries beyond them — this request renders
/// exactly what <c>Employee</c> already holds and adds nothing (AC-207-G).
/// </remarks>
/// <param name="FullName">Required — <c>Employee.Create</c> refuses a blank one.</param>
/// <param name="Phone">Entered form; <c>PhoneNumber</c> normalises it into the deduplication key (spec.md §10).</param>
/// <param name="Kind">Salaried or DayLabour (spec.md §10). Fixed at creation — KAFF-208 rule 3.</param>
/// <param name="BabId">The trade/باب. Required when <paramref name="Kind"/> is <c>DayLabour</c> (spec.md §10).</param>
/// <param name="Specialty">Optional free-text specialty within the trade (spec.md §10).</param>
/// <param name="NationalId">Optional, staff-only field already carried by the entity.</param>
/// <param name="Department">Optional, staff-only field. Free text — decisions.md D-139 §7, D-144 §2.</param>
/// <param name="JobTitle">Optional, staff-only field already carried by the entity.</param>
/// <param name="HiredOn">Optional, staff-only field already carried by the entity.</param>
/// <param name="AcknowledgedDuplicatePhone">
/// "I was shown who already holds this number and I am proceeding anyway." decisions.md D-141 §5 —
/// same shape as <c>CreateClient.Request</c>'s member of the same name. It never lets a salaried phone
/// past another salaried record: that refusal (<c>MasterDataErrors.EmployeePhoneTaken</c>) is checked
/// first and ignores this flag entirely (D-146 point 3).
/// </param>
public sealed record Request(
    string? FullName,
    string? Phone,
    EmployeeKind Kind,
    Guid? BabId,
    string? Specialty,
    string? NationalId,
    string? Department,
    string? JobTitle,
    DateOnly? HiredOn,
    bool AcknowledgedDuplicatePhone);
