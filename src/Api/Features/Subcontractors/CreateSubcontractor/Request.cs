namespace Kaff.Api.Features.Subcontractors.CreateSubcontractor;

/// <summary>
/// What the Technical Office (or the Owner, D-129 §1) sends to register one مقاول باطن. KAFF-211.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no <c>Code</c> member.</b> Same shape as <c>CreateClient.Request</c> and
/// <c>CreateEmployee.Request</c> — decisions.md D-107 §1/D-130 §6: the code is generated from
/// <c>KaffDbContext.SubcontractorCodeSequence</c>, never typed and never editable.
/// </para>
/// <para>
/// <b>No <c>TaxRegistrationNumber</c> member — D-147 point 3.</b> A member that does not exist cannot
/// be sent; the number is reached only through <c>PUT /api/subcontractors/{id}/tax-registration</c>,
/// gated <c>SubcontractorTaxRegistrationEdit</c>, never through this route. <b>No rate-card field of
/// any kind — D-139 §4, AC-211-H.</b> Rates live on each job's sub-BOQ, not here.
/// </para>
/// </remarks>
/// <param name="Name">Required — <c>Subcontractor.Create</c> refuses a blank one.</param>
/// <param name="Phone">Entered form; <c>PhoneNumber</c> normalises it into the deduplication key.</param>
/// <param name="TradeBabId">Optional — the باب this firm works in.</param>
/// <param name="RetentionRate">
/// Optional. Omitted, it defaults to <see cref="Kaff.Domain.MasterData.Subcontractor.DefaultRetentionRate"/>
/// (5%, spec.md §5.1). A whole percent as entered on the screen, e.g. <c>5</c> for five percent —
/// converted through <c>Percentage.FromPercent</c>, never stored as the bare integer (AC-211-C).
/// </param>
/// <param name="AcknowledgedDuplicatePhone">
/// "I was shown who already holds this number and I am proceeding anyway." decisions.md D-141 §5 —
/// same shape as <c>CreateClient.Request</c>'s member of the same name. Never refused outright: a
/// subcontractor phone is always warn-and-acknowledge (D-139 §1, D-141), unlike a salaried employee's.
/// </param>
public sealed record Request(
    string? Name,
    string? Phone,
    Guid? TradeBabId,
    decimal? RetentionRate,
    bool AcknowledgedDuplicatePhone);
