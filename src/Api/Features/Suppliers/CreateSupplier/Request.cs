namespace Kaff.Api.Features.Suppliers.CreateSupplier;

/// <summary>What Finance (or the Owner, D-129 §1) sends to register one supplier. KAFF-212.</summary>
/// <remarks>
/// <para>
/// <b>There is no <c>Code</c> member.</b> Same shape as <c>CreateClient.Request</c>,
/// <c>CreateEmployee.Request</c> and <c>CreateSubcontractor.Request</c> — decisions.md D-107 §1/D-130
/// §6: the code is generated from <c>KaffDbContext.SupplierCodeSequence</c>, never typed and never
/// editable.
/// </para>
/// <para>
/// <b>No withholding-rate or category member of any kind — D-139 §5, KAFF-212 rule 4.</b> It is set
/// per contract/job, on KAFF-318's ground, never here. <b>No project member — KAFF-212 rule 2</b>: a
/// supplier serves many projects from one account.
/// </para>
/// <para>
/// <b><see cref="TaxRegistrationNumber"/> is a member here, unlike the subcontractor's.</b>
/// decisions.md D-147 point 5 confirmed no split is needed: <c>SupplierManage</c> is already
/// Finance-and-Owner-only, so the number is entered and managed on this same route.
/// </para>
/// </remarks>
/// <param name="Name">Required — <c>Supplier.Create</c> refuses a blank one.</param>
/// <param name="Phone">Entered form; <c>PhoneNumber</c> normalises it into the deduplication key.</param>
/// <param name="Address">Optional.</param>
/// <param name="TaxRegistrationNumber">Optional. Identifies the legal entity; does not vary by job.</param>
/// <param name="AcknowledgedDuplicatePhone">
/// "I was shown who already holds this number and I am proceeding anyway." decisions.md D-141 — same
/// shape as <c>CreateClient.Request</c>'s member of the same name. Never refused outright: a supplier
/// phone is always warn-and-acknowledge (D-139 §1, D-141).
/// </param>
public sealed record Request(
    string? Name,
    string? Phone,
    string? Address,
    string? TaxRegistrationNumber,
    bool AcknowledgedDuplicatePhone);
