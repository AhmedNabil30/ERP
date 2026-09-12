namespace Kaff.Api.Features.DayLabour.RegisterFromSite;

/// <summary>
/// What a site engineer sends to register one day labourer from site. KAFF-209.
/// </summary>
/// <remarks>
/// <b>No <c>Kind</c> member.</b> decisions.md D-140 point 4/"Why": leaving it out makes it impossible
/// to send, which is stronger than validating it — a Site Engineer cannot create a salaried record by
/// any body they send. The handler always passes <see cref="Kaff.Domain.MasterData.EmployeeKind.DayLabour"/>.
/// </remarks>
/// <param name="FullName">Required — <c>Employee.Create</c> refuses a blank one.</param>
/// <param name="Phone">Entered form; <c>PhoneNumber</c> normalises it into the deduplication key (spec.md §10).</param>
/// <param name="BabId">The trade/باب. Required for day labour (spec.md §10).</param>
/// <param name="Specialty">Optional free-text specialty within the trade (spec.md §10).</param>
/// <param name="AcknowledgedDuplicatePhone">
/// "I was shown who already holds this number and I am proceeding anyway." decisions.md D-141 §5.
/// Every match on this route is warn-and-acknowledge, including a match against a salaried record
/// (D-140 point 6) — this route can never create a salaried record, so D-146's salaried-to-salaried
/// refusal never applies here.
/// </param>
public sealed record Request(
    string? FullName,
    string? Phone,
    Guid? BabId,
    string? Specialty,
    bool AcknowledgedDuplicatePhone);
